using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaylightCommander.Services;

namespace DaylightCommander.App.ViewModels;

/// <summary>작업 목록 한 줄 (진행 중이거나 막 끝난 파일 작업).</summary>
public sealed class JobItem(OperationJob job, Action<long> cancel)
{
    public long Id { get; } = job.Id;
    public string Title { get; } = Strings.Current.JobTitle(job.Label);
    public string StatusText { get; } = StatusOf(job);
    static string StatusOf(OperationJob job) => job.Status switch
    {
        JobStatus.Queued => Strings.Current.JobQueued,
        JobStatus.Running => job.Progress is { Total: > 0 } p ? $"{(int)(p.Fraction * 100)}%" : Strings.Current.JobRunning,
        JobStatus.Done => Strings.Current.JobDone,
        JobStatus.Failed => Strings.Current.JobFailed,
        _ => Strings.Current.JobCancelled,
    };

    public bool IsFailed { get; } = job.Status == JobStatus.Failed;
    public bool IsRunning { get; } = job.Status == JobStatus.Running;
    public bool CanCancel { get; } = !job.IsFinished;

    /// <summary>진행 막대: 전체 크기를 모르면 값 없이 흐르는 막대.</summary>
    public bool IsIndeterminate { get; } = job.Progress is null or { Total: <= 0 };

    public double Percent { get; } = (job.Progress?.Fraction ?? 0) * 100;
    public string Current { get; } = job.Progress?.Current ?? "";
    public bool HasCurrent { get; } = job.Status == JobStatus.Running && !string.IsNullOrEmpty(job.Progress?.Current);
    public string FailureText { get; } = job.Status is JobStatus.Failed or JobStatus.Cancelled or JobStatus.Done
        ? Strings.Current.JobFailureSummary(job.Failures, job.Error) : "";
    public bool HasFailure => FailureText.Length > 0;
    public IRelayCommand CancelCommand { get; } = new RelayCommand(() => cancel(job.Id));
}

/// <summary>진행 중이거나 막 끝난 파일 작업 목록. 큐의 이벤트는 작업 스레드에서 오므로 UI 스레드로 옮겨 받는다.</summary>
public sealed partial class OperationsViewModel : ObservableObject
{
    readonly OperationQueue _queue;
    int _posted;

    public OperationsViewModel(OperationQueue queue)
    {
        _queue = queue;
        _queue.JobsChanged += () =>
        {
            // 진행률은 자주 오므로 UI 스레드에 쌓이지 않게 하나만 예약한다.
            if (Interlocked.Exchange(ref _posted, 1) == 1) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Interlocked.Exchange(ref _posted, 0);
                Refresh();
            }, Avalonia.Threading.DispatcherPriority.Background);
        };
    }

    [ObservableProperty] public partial IReadOnlyList<JobItem> Jobs { get; set; } = [];
    [ObservableProperty] public partial bool HasJobs { get; set; }
    [ObservableProperty] public partial bool HasFinished { get; set; }

    void Refresh()
    {
        var jobs = _queue.Jobs;
        Jobs = jobs.Select(j => new JobItem(j, _queue.Cancel)).ToList();
        HasJobs = jobs.Count > 0;
        HasFinished = jobs.Any(j => j.IsFinished);
    }

    [RelayCommand] void DismissFinished() => _queue.DismissFinished();
}
