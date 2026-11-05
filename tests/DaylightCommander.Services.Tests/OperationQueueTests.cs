using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public sealed class OperationQueueTests : IDisposable
{
    sealed class FakeTrash : ITrash
    {
        public bool IsSupported => true;
        public void MoveToTrash(string path) { }
    }

    readonly string _root = Directory.CreateTempSubdirectory("dc-queue-test").FullName;
    readonly string _src;
    readonly string _dst;
    readonly OperationQueue _queue;
    int _finished;

    public OperationQueueTests()
    {
        _src = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
        _dst = Directory.CreateDirectory(Path.Combine(_root, "dst")).FullName;
        _queue = new OperationQueue(new FileOperations(new FakeTrash()),
            onJobFinished: () => { Interlocked.Increment(ref _finished); return Task.CompletedTask; },
            autoDismiss: TimeSpan.FromMilliseconds(200));
    }

    public void Dispose()
    {
        _queue.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    static string File_(string dir, string name, string text)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    string S(string name) => Path.Combine(_src, name);
    string D(string name) => Path.Combine(_dst, name);

    OperationJob Job(long id) => _queue.Jobs.First(j => j.Id == id);

    static async Task<T> Until<T>(Func<T?> probe) where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (probe() is { } value) return value;
            await Task.Delay(5);
        }
        throw new TimeoutException("조건이 10초 안에 만족되지 않았다");
    }

    Task<OperationJob> AwaitFinished(long id) => Until(() => _queue.Jobs.FirstOrDefault(j => j.Id == id && j.IsFinished));

    Task<ConflictRequest> AwaitConflict() => Until(() => _queue.Conflict);

    [Fact]
    public async Task 복사_작업은_완료되고_끝났다고_알린다()
    {
        File_(_src, "a.txt", "A");
        var id = _queue.EnqueueCopy([S("a.txt")], _dst);
        var done = await AwaitFinished(id);
        Assert.Equal(JobStatus.Done, done.Status);
        Assert.Equal("A", File.ReadAllText(D("a.txt")));
        await Until(() => _finished == 1 ? "ok" : null);
    }

    [Fact]
    public async Task 이동과_삭제_작업()
    {
        File_(_src, "m.txt", "M");
        File_(_src, "d.txt", "D");
        var move = _queue.EnqueueMove([S("m.txt")], _dst);
        var delete = _queue.EnqueueDelete([S("d.txt")], toTrash: false);
        Assert.Equal(JobStatus.Done, (await AwaitFinished(move)).Status);
        Assert.Equal(JobStatus.Done, (await AwaitFinished(delete)).Status);
        Assert.True(File.Exists(D("m.txt")));
        Assert.False(File.Exists(S("m.txt")));
        Assert.False(File.Exists(S("d.txt")));
    }

    [Fact]
    public async Task 실패한_작업은_Failed와_설명을_가진다()
    {
        var id = _queue.EnqueueCopy([S("missing.txt")], _dst);
        var result = await AwaitFinished(id);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Single(result.Failures);
        Assert.Contains("missing.txt", result.Failures[0].Path);
        Assert.Equal(new FileError.NotFound("missing.txt"), result.Failures[0].Error);
    }

    [Fact]
    public async Task 충돌이_나면_요청이_올라오고_답하면_이어진다()
    {
        File_(_src, "a.txt", "new");
        File_(_dst, "a.txt", "old");
        var id = _queue.EnqueueCopy([S("a.txt")], _dst);

        var request = await AwaitConflict();
        Assert.Equal(S("a.txt"), request.Source);
        Assert.Equal(D("a.txt"), request.Target);
        Assert.Equal(JobStatus.Running, Job(id).Status);

        _queue.ResolveConflict(new ConflictDecision(ConflictChoice.Overwrite));
        Assert.Equal(JobStatus.Done, (await AwaitFinished(id)).Status);
        Assert.Equal("new", File.ReadAllText(D("a.txt")));
        Assert.Null(_queue.Conflict);
    }

    [Fact]
    public async Task 충돌_질문에서_취소를_고르면_작업이_취소된다()
    {
        File_(_src, "a.txt", "new");
        File_(_dst, "a.txt", "old");
        var id = _queue.EnqueueCopy([S("a.txt")], _dst);
        await AwaitConflict();
        _queue.ResolveConflict(new ConflictDecision(ConflictChoice.Cancel));
        Assert.Equal(JobStatus.Cancelled, (await AwaitFinished(id)).Status);
        Assert.Equal("old", File.ReadAllText(D("a.txt")));
    }

    [Fact]
    public async Task 질문을_기다리는_중에_작업을_취소하면_질문도_사라진다()
    {
        File_(_src, "a.txt", "new");
        File_(_dst, "a.txt", "old");
        var id = _queue.EnqueueCopy([S("a.txt")], _dst);
        await AwaitConflict();
        _queue.Cancel(id);
        Assert.Equal(JobStatus.Cancelled, (await AwaitFinished(id)).Status);
        await Until(() => _queue.Conflict is null ? "gone" : null);
    }

    [Fact]
    public async Task 작업은_순서대로_하나씩_실행되고_대기_중인_작업은_취소할_수_있다()
    {
        File_(_src, "a.txt", "new");
        File_(_dst, "a.txt", "old");
        File_(_src, "b.txt", "B");
        File_(_src, "c.txt", "C");
        var first = _queue.EnqueueCopy([S("a.txt")], _dst);
        var second = _queue.EnqueueCopy([S("b.txt")], _dst);
        var third = _queue.EnqueueCopy([S("c.txt")], _dst);

        await AwaitConflict(); // 첫 작업이 질문에서 멈춰 있다
        Assert.Equal(JobStatus.Queued, Job(second).Status);
        Assert.Equal(JobStatus.Queued, Job(third).Status);

        _queue.Cancel(second);
        Assert.Equal(JobStatus.Cancelled, Job(second).Status);

        _queue.ResolveConflict(new ConflictDecision(ConflictChoice.Skip));
        Assert.Equal(JobStatus.Done, (await AwaitFinished(first)).Status);
        Assert.Equal(JobStatus.Done, (await AwaitFinished(third)).Status);
        Assert.False(File.Exists(D("b.txt")));
        Assert.Equal("C", File.ReadAllText(D("c.txt")));
    }

    [Fact]
    public async Task 완료_항목_지우기는_끝난_작업만_뺀다()
    {
        File_(_src, "a.txt", "new");
        File_(_dst, "a.txt", "old");
        File_(_src, "b.txt", "B");
        var running = _queue.EnqueueCopy([S("a.txt")], _dst);
        var failed = _queue.EnqueueCopy([S("missing")], _dst);
        await AwaitConflict();
        _queue.Cancel(running); // 취소되면 다음 작업이 이어서 실행된다
        await AwaitFinished(failed);

        _queue.DismissFinished();
        Assert.DoesNotContain(_queue.Jobs, j => j.IsFinished);
    }

    [Fact]
    public async Task 오류_없이_끝난_작업은_잠시_뒤_목록에서_사라진다()
    {
        File_(_src, "a.txt", "A");
        var id = _queue.EnqueueCopy([S("a.txt")], _dst);
        await AwaitFinished(id);
        await Until(() => _queue.Jobs.All(j => j.Id != id) ? "gone" : null);
    }

    [Fact]
    public async Task 진행_중_작업의_제목_재료를_가진다()
    {
        File_(_src, "a.txt", "A");
        File_(_src, "b.txt", "B");
        var id = _queue.EnqueueCopy([S("a.txt"), S("b.txt")], _dst);
        var label = Job(id).Label;
        Assert.Equal(FileOperationKind.Copy, label.Kind);
        Assert.Equal(2, label.Count);
        Assert.Equal("a.txt", label.FirstName);
        Assert.Equal(_dst, label.Target);
        await AwaitFinished(id);
    }
}
