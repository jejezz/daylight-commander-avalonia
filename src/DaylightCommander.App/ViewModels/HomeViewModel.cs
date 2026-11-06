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
        Operations = new OperationsViewModel(State.Queue);
        Left.IsActive = true;
        State.ActiveChanged += side =>
        {
            Left.IsActive = side == PaneSide.Left;
            Right.IsActive = side == PaneSide.Right;
            RefreshHasTarget();
        };
        State.Left.StateChanged += _ => RefreshHasTarget();
        State.Right.StateChanged += _ => RefreshHasTarget();
        // 편집기 설정이 바뀌면 F4 버튼의 사용 가능 여부도 바뀐다.
        _settings = settings;
        settings.Changed += OnSettingsChanged;
    }

    readonly SettingsController _settings;

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

    public void RefreshHasTarget()
    {
        HasTarget = State.ActiveController.TargetEntries().Count > 0;
        var cursorIsFile = State.ActiveController.State.CursorEntry is { IsDirectory: false };
        CanView = cursorIsFile;
        CanEdit = cursorIsFile; // 외부 편집기를 골랐으면 그것으로, 아니면 내장 에디터로 연다
    }

    // 하단 기능 키 버튼과 단축키가 같은 명령을 쓴다.
    [RelayCommand] void Rename() => State.RequestName(NameMode.Rename);
    [RelayCommand] void View() => State.RequestView();
    [RelayCommand] void Edit() => State.RequestEdit();
    [RelayCommand] void Copy() => State.RequestTransfer(FileOperationKind.Copy);
    [RelayCommand] void Move() => State.RequestTransfer(FileOperationKind.Move);
    [RelayCommand] void NewFolder() => State.RequestName(NameMode.NewFolder);
    [RelayCommand] void NewFile() => State.RequestName(NameMode.NewFile);
    [RelayCommand] void Delete() => State.RequestDelete();
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
