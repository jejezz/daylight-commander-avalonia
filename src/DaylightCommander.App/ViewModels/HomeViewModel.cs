using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.ViewModels;

/// <summary>
/// 메인 화면: 좌우 패널·활성 패널·파일 작업 큐·대화상자 상태는 <see cref="HomeState"/>(UI 비종속)가 가지고,
/// 이 클래스는 화면이 바인딩할 속성을 만든다. 창마다 하나씩 가진다.
/// </summary>
public sealed partial class HomeViewModel : ObservableObject, IDisposable
{
    public HomeViewModel(string leftPath, string rightPath, ListLocalDirectory list, SettingsController settings)
    {
        _settings = settings;
        State = new HomeState(leftPath, rightPath, (path, ct) => list.InvokeAsync(path, ct),
            runOnUi: work => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(work),
            settings: settings);
        Left = new PaneViewModel(State.Left, settings)
        {
            ActivateRequested = () => State.Activate(PaneSide.Left),
            OpenFileRequested = path => State.OpenFile(path, PaneSide.Left),
        };
        Right = new PaneViewModel(State.Right, settings)
        {
            ActivateRequested = () => State.Activate(PaneSide.Right),
            OpenFileRequested = path => State.OpenFile(path, PaneSide.Right),
        };
        Left.Actions = ActionsFor(PaneSide.Left);
        Right.Actions = ActionsFor(PaneSide.Right);
        Left.DiffLookup = DiffOf(PaneSide.Left);
        Right.DiffLookup = DiffOf(PaneSide.Right);
        Tree = new DirectoryTreeViewModel(State, path => _settings.ToggleBookmark(path));
        Operations = new OperationsViewModel(State.Queue);
        Left.IsActive = true;
        State.ActiveChanged += side =>
        {
            Left.IsActive = side == PaneSide.Left;
            Right.IsActive = side == PaneSide.Right;
            if (State.SinglePane) NotifyLayout(); // 1패널 모드에서는 활성 패널이 펼쳐진 쪽이다
            RefreshHasTarget();
        };
        State.Left.StateChanged += _ => { RefreshHasTarget(); RefreshCompare(); };
        State.Right.StateChanged += _ => { RefreshHasTarget(); RefreshCompare(); };
        State.CompareChanged += () => { OnPropertyChanged(nameof(CompareMode)); RefreshCompare(); };
        State.LayoutChanged += () =>
        {
            NotifyLayout();
            RefreshCompare(); // 동기화 막대의 → ←는 화면 위치 기준이다
        };
        // 편집기 설정이 바뀌면 F4 버튼의 사용 가능 여부도 바뀐다.
        settings.Changed += OnSettingsChanged;
    }

    readonly SettingsController _settings;

    public DirectoryTreeViewModel Tree { get; }

    // --- 패널 배치 ---

    /// <summary>본문 격자에서 각 패널이 놓이는 칸 (0 = 왼쪽, 2 = 오른쪽; 1은 두 패널 사이의 경로 맞추기 단추).</summary>
    public int LeftColumn => State.Swapped ? 2 : 0;
    public int RightColumn => State.Swapped ? 0 : 2;

    [RelayCommand] void SwapPanes() => State.ToggleSwap();
    [RelayCommand] void TogglePaneMode() => State.ToggleSinglePane();

    public bool SinglePane => State.SinglePane;
    public bool DualPane => !State.SinglePane;

    /// <summary>1패널 모드에서 이 패널이 펼쳐져 있는가 (아니면 가장자리 막대로 접혀 있다).</summary>
    public bool LeftExpanded => !State.SinglePane || State.Active == PaneSide.Left;
    public bool RightExpanded => !State.SinglePane || State.Active == PaneSide.Right;
    public bool LeftIsRail => !LeftExpanded;
    public bool RightIsRail => !RightExpanded;

    static readonly GridLength Star = new(1, GridUnitType.Star);
    static readonly GridLength RailWidth = new(36);

    GridLength WidthAt(PaneSide side) => !State.SinglePane || State.Active == side ? Star : RailWidth;

    /// <summary>본문 격자의 세 칸: 화면 왼쪽 패널 · 두 패널 사이 · 화면 오른쪽 패널. 접힌 패널의 칸은 막대 폭이 된다.</summary>
    public GridLength ColAWidth => WidthAt(State.Leftmost);
    public GridLength ColBWidth => WidthAt(State.Rightmost);
    public GridLength GapWidth => State.SinglePane ? new GridLength(10) : new GridLength(28);

    /// <summary>패널 모드 단추는 바꿔 갈 모드를 보여 준다.</summary>
    public string PaneModeIcon => State.SinglePane ? "dual-panel" : "single-panel";
    public string PaneModeTooltip => State.SinglePane ? Strings.Current.DualPaneTooltip : Strings.Current.SinglePaneTooltip;

    void NotifyLayout()
    {
        foreach (var name in new[]
        {
            nameof(LeftColumn), nameof(RightColumn), nameof(SinglePane), nameof(DualPane), nameof(LeftExpanded), nameof(RightExpanded),
            nameof(LeftIsRail), nameof(RightIsRail), nameof(ColAWidth), nameof(ColBWidth), nameof(GapWidth), nameof(PaneModeIcon), nameof(PaneModeTooltip),
        })
            OnPropertyChanged(name);
    }
    [RelayCommand] Task SyncPathRight() => State.SyncPathRightAsync();
    [RelayCommand] Task SyncPathLeft() => State.SyncPathLeftAsync();

    // --- 폴더 비교 ---

    Func<string, Services.FileDiffStatus?> DiffOf(PaneSide side) => path =>
        State.Comparison is { } c && (side == PaneSide.Left ? c.Left : c.Right).TryGetValue(path, out var status) ? status : null;

    /// <summary>폴더 비교 모드 (앱 바의 토글 단추). 켜면 아래에 동기화 막대가 나타난다.</summary>
    public bool CompareMode
    {
        get => State.CompareMode;
        set => State.SetCompare(value);
    }

    [ObservableProperty] public partial string SyncRightText { get; set; } = "";
    [ObservableProperty] public partial string SyncLeftText { get; set; } = "";
    [ObservableProperty] public partial bool CanSyncRight { get; set; }
    [ObservableProperty] public partial bool CanSyncLeft { get; set; }
    [ObservableProperty] public partial bool CanSyncBoth { get; set; }

    /// <summary>두 패널의 행 색과 동기화 막대의 개수·사용 가능 여부를 현재 비교 결과에 맞춘다.</summary>
    void RefreshCompare()
    {
        Left.RefreshDiff();
        Right.RefreshDiff();
        var c = State.Comparison;
        // 화면의 왼쪽 패널 기준: swap하면 Left/Right가 뒤바뀐다.
        var leftmostIsLeft = State.Leftmost == PaneSide.Left;
        var left = (leftmostIsLeft ? c?.Left.Count : c?.Right.Count) ?? 0;
        var right = (leftmostIsLeft ? c?.Right.Count : c?.Left.Count) ?? 0;
        SyncRightText = Strings.Current.SyncDiffToRight(left);
        SyncLeftText = Strings.Current.SyncDiffToLeft(right);
        CanSyncRight = left > 0;
        CanSyncLeft = right > 0;
        CanSyncBoth = left > 0 || right > 0;
    }

    [RelayCommand] void SyncToRight() => State.SyncFolders(State.Leftmost);
    [RelayCommand] void SyncToLeft() => State.SyncFolders(State.Rightmost);
    [RelayCommand] void SyncBoth() => State.SyncBidirectional();

    /// <summary>패널 화면이 부르는 동작. 각 동작은 먼저 그 패널을 활성 패널로 만든 뒤 홈 상태에 맡긴다.</summary>
    PaneActions ActionsFor(PaneSide side)
    {
        Action On(Action action) => () => { State.Activate(side); action(); };
        Action<string> OnPath(Action<string> action) => path => { State.Activate(side); action(path); };
        return new PaneActions(
            OpenWithDefaultApp: path => State.OpenFile(path, side),
            View: On(State.RequestView),
            Edit: On(State.RequestEdit),
            Rename: On(() => State.RequestName(NameMode.Rename)),
            Copy: On(() => State.RequestTransfer(FileOperationKind.Copy)),
            Move: On(() => State.RequestTransfer(FileOperationKind.Move)),
            Delete: On(() => State.RequestDelete()),
            Duplicate: On(State.DuplicateInPlace),
            Properties: On(State.RequestProperties),
            Compress: On(State.RequestCompress),
            Extract: On(() => _ = State.ExtractAsync()),
            Reveal: path => State.RevealInFileManager(path, side),
            ToggleBookmark: OnPath(_settings.ToggleBookmark),
            Drop: (paths, destination, mode) => State.Drop(paths, destination, mode, side));
    }

    void OnSettingsChanged(DaylightCommander.Domain.Settings.AppSettings _) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(RefreshHasTarget);

    public HomeState State { get; }
    public PaneViewModel Left { get; }
    public PaneViewModel Right { get; }
    public OperationsViewModel Operations { get; }

    public PaneViewModel ActivePane => State.Active == PaneSide.Left ? Left : Right;

    /// <summary>활성 패널에 작업할 항목(커서 또는 선택)이 있는지. F 키 버튼의 사용 가능 여부.</summary>
    [ObservableProperty] public partial bool HasTarget { get; set; }

    /// <summary>F3으로 볼 수 있는지: 커서가 파일일 때.</summary>
    [ObservableProperty] public partial bool CanView { get; set; }

    /// <summary>F4로 열 수 있는지: 커서가 파일일 때 (외부 편집기를 골랐으면 그것으로, 아니면 내장 에디터로).</summary>
    [ObservableProperty] public partial bool CanEdit { get; set; }

    /// <summary>압축풀기: 커서가 zip 파일일 때.</summary>
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ExtractCommand))] public partial bool CanExtract { get; set; }

    public void RefreshHasTarget()
    {
        HasTarget = State.ActiveController.TargetEntries().Count > 0;
        var cursorIsFile = State.ActiveController.State.CursorEntry is { IsDirectory: false };
        CanView = cursorIsFile;
        CanExtract = State.ActiveController.State.CursorEntry is { IsDirectory: false } z && ArchiveService.IsZip(z.Name);
        CanEdit = cursorIsFile; // 외부 편집기를 골랐으면 그것으로, 아니면 내장 에디터로 연다
    }

    // 하단 기능 키 버튼과 단축키가 같은 명령을 쓴다.
    [RelayCommand] void Rename() => State.RequestName(NameMode.Rename);
    [RelayCommand] void View() => State.RequestView();
    [RelayCommand] void Edit() => State.RequestEdit();
    [RelayCommand] void Copy() => State.RequestTransfer(FileOperationKind.Copy);
    [RelayCommand] void Move() => State.RequestTransfer(FileOperationKind.Move);
    [RelayCommand] void NewFolder() => State.RequestName(NameMode.NewFolder);
    [RelayCommand] void Compress() => State.RequestCompress();
    [RelayCommand(CanExecute = nameof(CanExtract))] Task Extract() => State.ExtractAsync();
    [RelayCommand] void NewFile() => State.RequestName(NameMode.NewFile);
    [RelayCommand] void Delete() => State.RequestDelete();
    [RelayCommand] void Duplicate() => State.DuplicateInPlace();
    [RelayCommand] void Properties() => State.RequestProperties();

    public Task StartAsync() => Task.WhenAll(Left.StartAsync(), Right.StartAsync());

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        Left.Dispose();
        Right.Dispose();
        State.Dispose();
    }
}
