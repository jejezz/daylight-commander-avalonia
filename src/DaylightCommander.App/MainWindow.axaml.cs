using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DaylightCommander.App.Dialogs;
using DaylightCommander.App.ViewModels;
using DaylightCommander.Domain;
using DaylightCommander.Domain.Window;
using DaylightCommander.Services;
using DaylightCommander.App.Views;

namespace DaylightCommander.App;

public partial class MainWindow : Window
{
    static int _count;

    readonly HomeViewModel _home;

    // --- 대화상자 ---
    // 홈 상태(HomeState)가 "어떤 대화상자가 열려야 하는지"를 가지고, 이 창이 그것을 모달 창으로 그린다.
    // 대화상자는 독립 창이 아니라 이 창에 묶인 모달 질문이다 (창 정책: 짧은 모달 질문은 대화상자).

    DialogBase? _dialogWindow;
    HomeDialog? _shownDialog;
    ConflictDialog? _conflictWindow;

    static bool SameDialog(HomeDialog? a, HomeDialog b) => (a, b) switch
    {
        (HomeDialog.Transfer x, HomeDialog.Transfer y) => x.Kind == y.Kind && x.Sources.SequenceEqual(y.Sources),
        (HomeDialog.Delete x, HomeDialog.Delete y) => x.Paths.SequenceEqual(y.Paths),
        (HomeDialog.NameInput x, HomeDialog.NameInput y) => x.Mode == y.Mode && x.OriginalPath == y.OriginalPath,
        (HomeDialog.SyncConflict x, HomeDialog.SyncConflict y) => x.Left.Path == y.Left.Path,
        (HomeDialog.PatternSelect, HomeDialog.PatternSelect) => true,
        (HomeDialog.Connect, HomeDialog.Connect) => true,
        _ => false,
    };

    void SyncDialog(HomeDialog? dialog)
    {
        if (dialog is null)
        {
            var closing = _dialogWindow;
            _dialogWindow = null;
            _shownDialog = null;
            closing?.CloseFromState();
            return;
        }
        // 같은 대화상자의 상태만 바뀌었으면(확정 실패로 오류가 생김) 창은 두고 오류만 보여 준다.
        if (_dialogWindow is not null && SameDialog(_shownDialog, dialog))
        {
            _shownDialog = dialog;
            if (_dialogWindow is ConnectDialog connectWindow && dialog is HomeDialog.Connect connectState)
            {
                connectWindow.Update(connectState);
                return;
            }
            if (_dialogWindow is TextPromptDialog prompt)
            {
                var error = dialog switch
                {
                    HomeDialog.Transfer t => t.Error,
                    HomeDialog.NameInput n => n.Error,
                    _ => null,
                };
                prompt.SetError(error is null ? null : Strings.Current.FileErrorText(error));
            }
            return;
        }

        _dialogWindow?.CloseFromState();
        _shownDialog = dialog;
        var window = CreateDialog(dialog);
        _dialogWindow = window;
        window.Dismissed += () =>
        {
            if (!ReferenceEquals(_dialogWindow, window)) return;
            _dialogWindow = null; // 이미 닫힌 창을 다시 닫지 않게
            _home.State.CloseDialog();
        };
        _ = window.ShowDialog(this);
    }

    static string Modified(FileEntry e) =>
        e.ModifiedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "--";

    DialogBase CreateDialog(HomeDialog dialog)
    {
        var state = _home.State;
        switch (dialog)
        {
            case HomeDialog.Transfer t:
            {
                var verb = t.Kind == FileOperationKind.Copy ? Strings.Current.TransferCopyTitle : Strings.Current.TransferMoveTitle;
                var what = Strings.Current.ItemsWhat(t.Sources.Count, NameOf(t.Sources[0]));
                var window = new TextPromptDialog(verb, Strings.Current.TransferPrompt(t.Kind, what), t.Target, t.Target.Length, t.Target.Length, verb);
                window.Confirmed += text => state.ConfirmTransfer(text);
                return window;
            }
            case HomeDialog.Delete d:
            {
                var window = new DeleteDialog(d.Paths, d.ToTrash);
                window.Confirmed += state.ConfirmDelete;
                return window;
            }
            case HomeDialog.NameInput n:
            {
                var title = n.Mode switch
                {
                    NameMode.Rename => Strings.Current.DialogRenameTitle,
                    NameMode.NewFolder => Strings.Current.DialogNewFolderTitle,
                    NameMode.Compress => Strings.Current.FnCompress,
                    _ => Strings.Current.DialogNewFileTitle,
                };
                // 이름 변경은 확장자를 뺀 이름만 먼저 선택해 둔다.
                var dot = n.InitialText.LastIndexOf('.');
                var end = n.Mode is NameMode.Rename or NameMode.Compress && dot > 0 ? dot : n.InitialText.Length;
                var window = new TextPromptDialog(title, null, n.InitialText, 0, end, Strings.Current.CommonConfirm);
                window.Confirmed += text => _ = state.ConfirmNameAsync(text);
                return window;
            }
            case HomeDialog.Connect c:
            {
                var window = new ConnectDialog(c.Profile, c.Scheme);
                window.Confirmed += (host, port, user, password, save, scheme, path) => _ = state.ConfirmConnectAsync(host, port, user, password, save, scheme, path);
                return window;
            }
            case HomeDialog.PatternSelect:
            {
                var window = new PatternSelectDialog();
                window.Confirmed += (pattern, add) => state.ConfirmPatternSelect(pattern, add);
                return window;
            }
            case HomeDialog.SyncConflict c:
            {
                var s = Strings.Current;
                var info = s.SyncConflictLeft(BytesFormat.Format(c.Left.SizeBytes), Modified(c.Left)) + "\n"
                         + s.SyncConflictRight(BytesFormat.Format(c.Right.SizeBytes), Modified(c.Right));
                // 단추 순서: 취소 · 건너뛰기 · 왼쪽 · 오른쪽 (원본). 창을 닫으면 취소.
                var window = MessageDialog.Create(s.SyncConflictTitle(c.Left.Name), info,
                    [s.CommonCancel, s.ConflictSkip, s.SyncUseLeft, s.SyncUseRight], defaultIndex: 2, cancelIndex: 0);
                window.Chosen += index => state.ResolveSyncConflict((SyncChoice)index);
                return window;
            }
            default:
                throw new InvalidOperationException("알 수 없는 대화상자");
        }
    }

    static string NameOf(string path) =>
        Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : path;

    /// <summary>이름 충돌 질문. 작업이 답을 기다리는 동안 떠 있고, 답하면(또는 작업이 취소되면) 닫힌다.</summary>
    void SyncConflict()
    {
        var request = _home.State.Queue.Conflict;
        if (request is null)
        {
            var closing = _conflictWindow;
            _conflictWindow = null;
            closing?.CloseFromState();
            return;
        }
        if (_conflictWindow is not null) return;
        var window = new ConflictDialog(request);
        _conflictWindow = window;
        window.Decided += decision => _home.State.Queue.ResolveConflict(decision);
        window.Dismissed += () =>
        {
            if (!ReferenceEquals(_conflictWindow, window)) return;
            _conflictWindow = null;
            _home.State.Queue.ResolveConflict(new ConflictDecision(ConflictChoice.Cancel)); // 창을 그냥 닫으면 취소
        };
        _ = window.ShowDialog(this);
    }

    /// <summary>한 번에 넘기는 페이지 이동 칸 수 (원본의 PAGE_ROWS).</summary>
    const int PageRows = 10;

    /// <summary>
    /// 홈 화면 단축키 (원본 handleHomeKey). 경로 표시줄 같은 글 입력 칸에 초점이 있으면 건드리지 않는다.
    /// tunnel로 먼저 받아서, 처리한 키는 목록 컨트롤의 기본 동작(방향키 선택 등)이 따로 실행되지 않게 막는다.
    /// F2~F8(파일 작업)은 4단계, Ctrl+N·Ctrl+,는 4·5단계에서 더한다.
    /// </summary>
    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (_home.State.ModalOpen) return;

        var c = _home.ActivePane.Controller;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var handled = true;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            switch (e.Key)
            {
                case Key.Left: _ = c.GoBackAsync(); break;
                case Key.Right: _ = c.GoForwardAsync(); break;
                case Key.Up: _ = c.GoUpAsync(); break;
                case Key.Enter: _home.State.RequestProperties(); break; // 속성
                default: handled = false; break;
            }
        }
        else if (Mods.Primary(e.KeyModifiers))
        {
            switch (e.Key)
            {
                case Key.A: c.ToggleSelectAll(); break;
                case Key.H: c.ToggleShowHidden(); break;
                case Key.R: _ = c.RefreshAsync(); break;
                case Key.N: _home.State.RequestName(NameMode.NewFile); break;
                case Key.D: _home.State.DuplicateInPlace(); break; // 여기에 복제
                case Key.OemComma: SettingsWindow.Open(this); break; // 설정 (ui-ux.md §7)
                case Key.T: TreeButton.IsChecked = TreeButton.IsChecked != true; break; // 디렉터리 트리
                case Key.U: _home.State.ToggleSwap(); break; // 좌우 패널 교환
                case Key.OemTilde: _home.State.OpenTerminal(_home.State.Active); break; // 터미널 열기 (Ctrl+`)
                case Key.OemPipe: case Key.OemBackslash: _home.State.ToggleSinglePane(); break; // 1패널/2패널 전환 (Ctrl+\)
                case Key.Right when shift: _ = _home.State.SyncPathRightAsync(); break; // 왼쪽 패널 경로를 오른쪽으로
                case Key.Left when shift: _ = _home.State.SyncPathLeftAsync(); break;
                case Key.W: Close(); break;
                default: handled = false; break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Tab: _home.State.SwitchActive(); break;
                case Key.Down: c.MoveCursor(1, shift); break;
                case Key.Up: c.MoveCursor(-1, shift); break;
                case Key.PageDown: c.MoveCursor(PageRows, shift); break;
                case Key.PageUp: c.MoveCursor(-PageRows, shift); break;
                case Key.Home: c.CursorToStart(shift); break;
                case Key.End: c.CursorToEnd(shift); break;
                case Key.Space: case Key.Insert: c.ToggleSelectAtCursor(); break;
                case Key.Enter: _home.ActivePane.OpenCursorCommand.Execute(null); break; // 폴더는 들어가고 파일은 기본 앱으로
                case Key.Back: if (!c.BackspaceFilter()) _ = c.GoUpAsync(); break;
                case Key.Escape: if (!c.ClearFilter()) c.ClearSelection(); break;
                case Key.F2: _home.State.RequestName(NameMode.Rename); break;
                case Key.F3: _home.State.RequestView(); break;
                case Key.F4: _home.State.RequestEdit(); break;
                case Key.F5: _home.State.RequestTransfer(FileOperationKind.Copy); break;
                case Key.F6: _home.State.RequestTransfer(FileOperationKind.Move); break;
                case Key.F7: _home.State.RequestName(NameMode.NewFolder); break;
                case Key.F8: case Key.Delete: _home.State.RequestDelete(permanent: shift); break;
                default: handled = false; break;
            }
        }
        e.Handled = handled;
    }

    /// <summary>
    /// 글자는 빠른 필터. 키 이벤트가 아니라 글자 입력 이벤트로 받는다 — 입력기(IME)가 확정한 글자가 이 경로로 온다.
    /// </summary>
    void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (string.IsNullOrEmpty(e.Text)) return;
        // 공백(선택 토글)·제어 문자는 필터가 아니다.
        var text = new string(e.Text.Where(ch => ch > ' ' && ch != '\u007f').ToArray());
        if (text.Length == 0) return;
        _home.ActivePane.Controller.AppendFilter(text);
        e.Handled = true;
    }

    /// <summary>폴더로 남아 있는 경로만 돌려준다. 저장해 둔 경로가 지워졌거나 외장 드라이브가 빠졌을 수 있다.</summary>
    static string? ExistingDirectory(string? path)
    {
        try { return path is not null && Directory.Exists(path) ? path : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    // --- 창 위치·크기 (window-placement.md) ---

    const double DefaultWidth = 1200, DefaultHeight = 720;
    bool _trackPlacement;

    IReadOnlyList<DisplayArea> DisplayAreas() =>
        Screens.All.Select(sc => new DisplayArea(
            new RectD(sc.WorkingArea.X, sc.WorkingArea.Y, sc.WorkingArea.Width, sc.WorkingArea.Height),
            sc.Scaling, sc.IsPrimary)).ToList();

    /// <summary>
    /// 저장한 위치로 연다. 기억한 곳이 없거나 그 모니터가 없어졌으면 주 모니터 가운데에 연다. 보이기 전에 정한다.
    /// 최대화는 위치를 복원했을 때만 되살린다 (Avalonia는 창을 보이기 전에 상태를 정하면 작은 창을 거치지 않고 바로 최대화로 뜬다).
    /// </summary>
    void RestorePlacement()
    {
        var displays = DisplayAreas();
        var saved = App.Settings.Value.Window;
        var restored = saved is null ? null : PlacementMath.Restore(saved, displays);
        var target = restored ?? (PlacementMath.PrimaryOf(displays) is { } d ? PlacementMath.CenterOn(d, DefaultWidth, DefaultHeight) : (PlacementTarget?)null);
        if (target is not { } t) return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint((int)Math.Round(t.X), (int)Math.Round(t.Y));
        Width = t.Width;
        Height = t.Height;
        if (restored is not null && saved!.Maximized) WindowState = WindowState.Maximized;
    }

    /// <summary>창이 움직이거나 크기·상태가 바뀌면 (모아서) 설정에 반영한다. 최소화·전체 화면 중에는 저장하지 않는다.</summary>
    void SavePlacement()
    {
        if (!_trackPlacement || WindowState is WindowState.Minimized or WindowState.FullScreen) return;
        var next = PlacementMath.Updated(
            App.Settings.Value.Window, Position.X, Position.Y, ClientSize.Width, ClientSize.Height, RenderScaling,
            maximized: WindowState == WindowState.Maximized);
        if (next is not null && next != App.Settings.Value.Window) App.Settings.Update(s => s with { Window = next });
    }

    /// <summary>왼쪽 디렉터리 트리: 열림·폭을 설정에서 복원하고, 바뀌면 저장한다 (첫 창만 저장).</summary>
    void SetupTreeDock(bool primary, SettingsController settings)
    {
        void Apply(bool open, double width)
        {
            var col = Body.ColumnDefinitions;
            col[0].MinWidth = open ? DaylightCommander.Domain.Settings.AppSettings.MinTreeDockWidth : 0;
            col[0].MaxWidth = open ? DaylightCommander.Domain.Settings.AppSettings.MaxTreeDockWidth : 0;
            col[0].Width = new GridLength(open ? width : 0);
            col[1].Width = new GridLength(open ? 8 : 0);
            TreeCard.IsVisible = TreeSplitter.IsVisible = open;
            if (open) _home.Tree.Refresh();
        }

        var s = settings.Value;
        TreeButton.IsChecked = primary && s.TreeDockOpen;
        Apply(TreeButton.IsChecked == true, s.TreeDockWidth);

        TreeButton.IsCheckedChanged += (_, _) =>
        {
            var open = TreeButton.IsChecked == true;
            var width = settings.Value.TreeDockWidth;
            Apply(open, width);
            if (primary) settings.Update(x => x with { TreeDockOpen = open });
        };
        TreeSplitter.DragCompleted += (_, _) =>
        {
            var width = Math.Round(Body.ColumnDefinitions[0].ActualWidth);
            if (primary && width >= DaylightCommander.Domain.Settings.AppSettings.MinTreeDockWidth) settings.Update(x => x with { TreeDockWidth = width });
        };
    }

    const double DualMinWidth = 960, SingleMinWidth = 560;

    void ApplyMinWidth() => MinWidth = _home.State.SinglePane ? SingleMinWidth : DualMinWidth;

    public MainWindow()
    {
        var primary = _count++ == 0;
        var settings = App.Settings;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // 첫 창은 마지막에 보던 좌우 폴더로 연다 (없어졌으면 홈).
        var left = primary ? ExistingDirectory(settings.Value.LastLeftPath) ?? home : home;
        var right = primary ? ExistingDirectory(settings.Value.LastRightPath) ?? home : home;
        _home = new HomeViewModel(left, right, new ListLocalDirectory(), settings, App.Remote);
        DataContext = _home;
        InitializeComponent();
        if (primary) RestorePlacement();

        // 큐와 홈 상태의 이벤트는 작업 스레드에서 올 수 있다 → UI 스레드로 옮겨서 그린다.
        _home.State.DialogChanged += d => Avalonia.Threading.Dispatcher.UIThread.Post(() => SyncDialog(d));
        _home.State.PropertiesRequested += paths => Avalonia.Threading.Dispatcher.UIThread.Post(() => PropertiesWindow.Open(paths, this));
        _home.State.EditRequested += path => Avalonia.Threading.Dispatcher.UIThread.Post(() => EditorWindow.Open(path, this));
        _home.State.RemoteEditRequested += (path, link) => Avalonia.Threading.Dispatcher.UIThread.Post(() => EditorWindow.Open(path, this, link));
        _home.State.ViewRequested += path => Avalonia.Threading.Dispatcher.UIThread.Post(() => ViewerWindow.Open(path, this));
        _home.State.Queue.ConflictChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(SyncConflict);
        // 작업 목록은 기능 키 바 위에 겹쳐 뜨므로 바와 같은 높이를 넘지 않게 한다.
        Bar.SizeChanged += (_, e) => Operations.MaxHeight = e.NewSize.Height;
        Closed += (_, _) => _home.Dispose();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Bubble | RoutingStrategies.Tunnel);

        if (primary)
        {
            // 좌우 패널의 현재 경로를 기억해 다음 실행에 복원한다.
            _home.State.Left.StateChanged += st => settings.Update(s => s.LastLeftPath == st.CurrentPath ? s : s with { LastLeftPath = st.CurrentPath });
            _home.State.Right.StateChanged += st => settings.Update(s => s.LastRightPath == st.CurrentPath ? s : s with { LastRightPath = st.CurrentPath });

            // 창 위치·크기·최대화가 바뀌면 설정에 반영한다 (저장은 SettingsController가 모아서 한다).
            PositionChanged += (_, _) => SavePlacement();
            PropertyChanged += (_, e) =>
            {
                if (e.Property == ClientSizeProperty || e.Property == WindowStateProperty) SavePlacement();
            };
            Closing += (_, _) => settings.Flush();
        }
        else
        {
            Title += $" #{_count}";
        }

        Opened += async (_, _) =>
        {
            await _home.StartAsync();
            _home.RefreshHasTarget();
            // 복원하는 동안의 크기 변화가 저장되지 않게, 열린 뒤에 추적을 시작한다.
            await Task.Delay(300);
            _trackPlacement = true;
        };

        NewWindow.Click += (_, _) => WindowPlacementHelper.OpenNear(this, new MainWindow());
        SettingsButton.Click += (_, _) => SettingsWindow.Open(this);
        AboutButton.Click += (_, _) => AboutDialog.Open(this);
        SetupTreeDock(primary, settings);
        // 좌우 교환은 첫 창만 기억하고 복원한다.
        if (primary)
        {
            _home.State.SetSwapped(settings.Value.PanesSwapped);
            _home.State.SetSinglePane(settings.Value.SinglePane);
            _home.State.LayoutChanged += () => settings.Update(x => x with { PanesSwapped = _home.State.Swapped, SinglePane = _home.State.SinglePane });
        }
        // 1패널 모드에서는 창을 더 좁게 줄일 수 있다 (원본). 2패널 모드로 돌아가면 최소 폭이 다시 늘어난다.
        _home.State.LayoutChanged += ApplyMinWidth;
        ApplyMinWidth();
    }
}
