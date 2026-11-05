using System.Threading.Channels;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

public enum JobStatus { Queued, Running, Done, Failed, Cancelled }

/// <summary>
/// 작업 제목의 재료. 문장은 화면이 언어에 맞춰 만든다 (conventions localization.md).
/// <paramref name="FirstName"/>은 첫 항목 이름이고 <paramref name="Count"/>가 1이면 그 이름만 보인다.
/// <paramref name="Target"/>은 복사·이동의 대상 폴더.
/// </summary>
public sealed record JobLabel(FileOperationKind Kind, int Count, string FirstName, string? Target = null, bool ToTrash = false);

public sealed record OperationJob(long Id, FileOperationKind Kind, JobLabel Label)
{
    public JobStatus Status { get; init; } = JobStatus.Queued;
    public OperationProgress? Progress { get; init; }

    /// <summary>일부 항목이 실패했을 때의 목록.</summary>
    public IReadOnlyList<OperationFailure> Failures { get; init; } = [];

    /// <summary>작업 전체가 실패한 오류 (예상하지 못한 예외).</summary>
    public FileError? Error { get; init; }

    public bool IsFinished => Status is JobStatus.Done or JobStatus.Failed or JobStatus.Cancelled;
}

/// <summary>UI가 답해야 하는 이름 충돌. <see cref="OperationQueue.ResolveConflict"/>로 답한다.</summary>
public sealed class ConflictRequest
{
    internal ConflictRequest(string source, string target, TaskCompletionSource<ConflictDecision> answer)
    {
        Source = source;
        Target = target;
        Answer = answer;
    }

    public string Source { get; }
    public string Target { get; }
    internal TaskCompletionSource<ConflictDecision> Answer { get; }
}

/// <summary>
/// 파일 작업 큐 (원본의 전송 큐 개념). 작업은 하나씩 순서대로 실행되고, 진행률은 <see cref="Jobs"/>로 흘러나온다.
/// 실행 중인 작업과 대기 중인 작업은 <see cref="Cancel"/>로 취소할 수 있다. 이름 충돌이 나면 <see cref="Conflict"/>에 요청이 올라오고
/// 작업은 <see cref="ResolveConflict"/>가 불릴 때까지 멈춘다.
/// <para>
/// <see cref="JobsChanged"/>·<see cref="ConflictChanged"/>는 작업 스레드에서 불릴 수 있다 — 화면은 UI 스레드로 옮겨서 받는다.
/// <c>onJobFinished</c>는 작업이 끝날 때마다(성공·실패·취소 모두) 불린다. 패널 새로고침용.
/// </para>
/// </summary>
public sealed class OperationQueue : IDisposable
{
    delegate Task<OperationResult> Block(Action<OperationProgress> report, CancellationToken ct);

    sealed record Work(long Id, Block Run);

    readonly FileOperations _ops;
    readonly Func<Task>? _onJobFinished;
    readonly TimeSpan _autoDismiss;
    readonly object _gate = new();
    readonly Channel<Work> _channel = Channel.CreateUnbounded<Work>();
    readonly Dictionary<long, CancellationTokenSource> _running = [];
    readonly CancellationTokenSource _lifetime = new();
    readonly ConflictResolver _resolver;
    long _nextId;
    IReadOnlyList<OperationJob> _jobs = [];
    ConflictRequest? _conflict;

    /// <param name="autoDismiss">오류 없이 끝난 작업을 목록에서 빼기까지의 시간 (기본 2.5초).</param>
    public OperationQueue(FileOperations ops, Func<Task>? onJobFinished = null, TimeSpan? autoDismiss = null)
    {
        _ops = ops;
        _onJobFinished = onJobFinished;
        _autoDismiss = autoDismiss ?? TimeSpan.FromMilliseconds(2500);
        _resolver = ResolveAsync;
        _ = Task.Run(LoopAsync);
    }

    public IReadOnlyList<OperationJob> Jobs { get { lock (_gate) return _jobs; } }
    public ConflictRequest? Conflict { get { lock (_gate) return _conflict; } }

    public event Action? JobsChanged;
    public event Action? ConflictChanged;

    public long EnqueueCopy(IReadOnlyList<string> sources, string targetDir) =>
        Enqueue(FileOperationKind.Copy, LabelOf(FileOperationKind.Copy, sources, target: targetDir),
            (report, ct) => _ops.CopyAsync(sources, targetDir, _resolver, report, ct));

    public long EnqueueMove(IReadOnlyList<string> sources, string targetDir) =>
        Enqueue(FileOperationKind.Move, LabelOf(FileOperationKind.Move, sources, target: targetDir),
            (report, ct) => _ops.MoveAsync(sources, targetDir, _resolver, report, ct));

    public long EnqueueDelete(IReadOnlyList<string> paths, bool toTrash) =>
        Enqueue(FileOperationKind.Delete, LabelOf(FileOperationKind.Delete, paths, toTrash: toTrash),
            (report, ct) => _ops.DeleteAsync(paths, toTrash, report, ct));

    /// <summary>충돌 요청에 답한다. 요청이 없으면 아무 일도 없다.</summary>
    public void ResolveConflict(ConflictDecision decision) => Conflict?.Answer.TrySetResult(decision);

    /// <summary>실행 중이면 중단하고, 대기 중이면 건너뛴다. 이미 끝났으면 아무 일도 없다.</summary>
    public void Cancel(long id)
    {
        CancellationTokenSource? cts;
        lock (_gate) _running.TryGetValue(id, out cts);
        if (cts is not null)
        {
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
        }
        else
        {
            Update(id, j => j.Status == JobStatus.Queued ? j with { Status = JobStatus.Cancelled } : j);
        }
    }

    public void DismissFinished()
    {
        lock (_gate) _jobs = _jobs.Where(j => !j.IsFinished).ToList();
        JobsChanged?.Invoke();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _channel.Writer.TryComplete();
    }

    long Enqueue(FileOperationKind kind, JobLabel label, Block run)
    {
        var id = Interlocked.Increment(ref _nextId);
        lock (_gate) _jobs = [.. _jobs, new OperationJob(id, kind, label)];
        JobsChanged?.Invoke();
        _channel.Writer.TryWrite(new Work(id, run));
        return id;
    }

    async Task LoopAsync()
    {
        try
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(_lifetime.Token)) await ExecuteAsync(work);
        }
        catch (OperationCanceledException)
        {
            // 큐를 닫는다
        }
    }

    async Task ExecuteAsync(Work work)
    {
        if (StatusOf(work.Id) == JobStatus.Cancelled) return;

        // 시작 전에 등록해 두어야 그 사이에 온 Cancel도 이 작업에 닿는다.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        lock (_gate) _running[work.Id] = cts;
        Update(work.Id, j => j with { Status = JobStatus.Running });
        try
        {
            var result = await work.Run(p => Update(work.Id, j => j with { Progress = p }), cts.Token);
            Update(work.Id, j => result.Failures.Count == 0
                ? j with { Status = JobStatus.Done }
                : j with { Status = JobStatus.Failed, Failures = result.Failures });
            if (result.Failures.Count == 0) AutoDismiss(work.Id);
        }
        catch (OperationCanceledException)
        {
            Update(work.Id, j => j with { Status = JobStatus.Cancelled });
        }
        catch (Exception e)
        {
            Update(work.Id, j => j with { Status = JobStatus.Failed, Error = FileErrors.From(e) });
        }
        finally
        {
            lock (_gate) _running.Remove(work.Id);
        }

        if (_onJobFinished is not null)
        {
            try { await _onJobFinished(); }
            catch (Exception) { /* 새로고침 실패가 큐를 멈추게 해서는 안 된다. */ }
        }
    }

    /// <summary>오류 없이 끝난 작업은 잠시 보여준 뒤 목록에서 뺀다.</summary>
    void AutoDismiss(long id) => _ = Task.Delay(_autoDismiss, _lifetime.Token).ContinueWith(_ =>
    {
        lock (_gate) _jobs = _jobs.Where(j => !(j.Id == id && j.Status == JobStatus.Done)).ToList();
        JobsChanged?.Invoke();
    }, TaskContinuationOptions.NotOnCanceled);

    async Task<ConflictDecision> ResolveAsync(string source, string target, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<ConflictDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetConflict(new ConflictRequest(source, target, answer));
        using var registration = ct.Register(() => answer.TrySetCanceled(ct));
        try
        {
            return await answer.Task;
        }
        finally
        {
            SetConflict(null);
        }
    }

    void SetConflict(ConflictRequest? request)
    {
        lock (_gate) _conflict = request;
        ConflictChanged?.Invoke();
    }

    JobStatus? StatusOf(long id)
    {
        lock (_gate) return _jobs.FirstOrDefault(j => j.Id == id)?.Status;
    }

    void Update(long id, Func<OperationJob, OperationJob> change)
    {
        lock (_gate) _jobs = _jobs.Select(j => j.Id == id ? change(j) : j).ToList();
        JobsChanged?.Invoke();
    }

    static JobLabel LabelOf(FileOperationKind kind, IReadOnlyList<string> paths, string? target = null, bool toTrash = false) =>
        new(kind, paths.Count,
            paths.Count == 0 ? "" : Path.GetFileName(paths[0].TrimEnd('\\', '/')) is { Length: > 0 } n ? n : paths[0],
            target, toTrash);
}
