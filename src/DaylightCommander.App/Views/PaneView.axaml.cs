using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using DaylightCommander.App.ViewModels;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.Views;

public partial class PaneView : UserControl
{
    PaneViewModel? Vm => DataContext as PaneViewModel;

    public PaneView()
    {
        InitializeComponent();

        // 패널 어디를 눌러도 이 패널이 활성 패널이 된다 (버튼이 이벤트를 먹어도 받도록 tunnel + handledEventsToo).
        AddHandler(PointerPressedEvent, (_, _) => Vm?.RequestActivate(), RoutingStrategies.Tunnel, handledEventsToo: true);

        // 행 클릭: 목록 컨트롤의 기본 선택 동작 대신 컨트롤러가 커서·선택을 정한다 (Ctrl 토글, Shift 범위).
        // 더블클릭은 Tapped 이벤트에 기대지 않고 ClickCount로 직접 판단한다.
        Entries.AddHandler(PointerPressedEvent, OnEntryPressed, RoutingStrategies.Tunnel);

        Entries.AddHandler(PointerMovedEvent, OnEntryMoved, RoutingStrategies.Tunnel);
        Entries.AddHandler(PointerReleasedEvent, OnEntryReleased, RoutingStrategies.Tunnel);

        // 끌어다 놓기: 이 패널 어디든 목적지가 된다.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => ClearDropHighlight());
        AddHandler(DragDrop.DropEvent, OnDrop);

        BookmarkButton.Click += (_, _) => { FillBookmarks(); BookmarkPopup.IsOpen = true; };
        DriveButton.Click += (_, _) => { FillDrives(); DrivePopup.IsOpen = true; };
        RecentButton.Click += (_, _) => { FillRecent(); RecentPopup.IsOpen = true; };

        StatusBar.PointerPressed += (_, _) => Vm?.ClearErrorCommand.Execute(null);

        PathBox.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Vm?.GoToPathCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Vm?.ResetPathTextCommand.Execute(null);
                    Entries.Focus();
                    e.Handled = true;
                    break;
            }
        };
    }

    // --- 행 클릭·끌기·우클릭 ---

    PointerPressedEventArgs? _pressArgs;
    Point _pressPoint;
    EntryItem? _pressItem;
    bool _clickDeferred; // 여러 개 선택한 행을 눌렀다: 끌기인지 클릭인지 알 때까지 선택을 건드리지 않는다
    bool _dragging;
    bool _menuPending;

    void OnEntryPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(Entries);
        var row = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        if (row?.DataContext is not EntryItem item || Vm is not { } vm) return;

        if (point.Properties.IsRightButtonPressed)
        {
            // 여러 개를 고른 상태에서 그 안의 행을 우클릭하면 선택을 유지하고, 아니면 그 행만 고른다.
            if (!item.IsSelected) vm.Click(item, ctrl: false, shift: false);
            _menuPending = true; // 선택이 바뀌면 줄이 다시 그려지므로, 놓을 때의 이벤트 대상 대신 이 표시로 메뉴를 연다
            Entries.Focus();
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed) return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _pressArgs = e;
        _pressPoint = point.Position;
        _pressItem = item;
        _clickDeferred = !ctrl && !shift && item.IsSelected && e.ClickCount == 1 && vm.Items.Count(x => x.IsSelected) > 1;
        if (!_clickDeferred) vm.Click(item, ctrl, shift);
        Entries.Focus(); // 경로 표시줄에 있던 키보드 초점을 목록으로 돌려 단축키가 듣게 한다
        if (e.ClickCount == 2) vm.OpenCursorCommand.Execute(null);
        e.Handled = true;
    }

    async void OnEntryMoved(object? sender, PointerEventArgs e)
    {
        if (_pressArgs is not { } pressed || _pressItem is not { } item || _dragging || Vm is not { } vm) return;
        var point = e.GetCurrentPoint(Entries);
        if (!point.Properties.IsLeftButtonPressed) { _pressArgs = null; return; }
        var delta = point.Position - _pressPoint;
        if (Math.Abs(delta.X) < 6 && Math.Abs(delta.Y) < 6) return;

        _dragging = true;
        _pressArgs = null;
        _clickDeferred = false;
        try
        {
            var picked = vm.Items.Where(x => x.IsSelected).Select(x => x.Path).ToList();
            IReadOnlyList<string> paths = item.IsSelected && picked.Count > 0 ? picked : [item.Path];
            DragSession.Paths = paths;
            DragSession.Drives = new ListDrives().Invoke();

            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DataFormat.Text, string.Join("\n", paths)));
            await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
        finally
        {
            DragSession.Paths = null;
            DragSession.Drives = null;
            _dragging = false;
            ClearDropHighlight();
        }
    }

    void OnEntryReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasDeferred = _clickDeferred;
        var item = _pressItem;
        _pressArgs = null;
        _clickDeferred = false;
        if (e.InitialPressMouseButton == MouseButton.Left && wasDeferred && item is not null && Vm is { } vm)
            vm.Click(item, ctrl: false, shift: false); // 끌지 않고 뗐다 → 보통 클릭으로 처리

        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            if (_menuPending) ShowContextMenu();
            _menuPending = false;
            e.Handled = true;
        }
    }

    // --- 우클릭 메뉴 ---

    void ShowContextMenu()
    {
        if (Vm is not { Actions: { } act } vm) return;
        var s = Strings.Current;
        var picked = vm.Items.Where(x => x.IsSelected).ToList();
        if (picked.Count == 0 && vm.Cursor is { } cursor) picked = [cursor];
        if (picked.Count == 0) return;

        var single = picked.Count == 1 ? picked[0] : null;
        var popup = ContextPopup;
        ContextItems.Children.Clear();
        void Add(string text, Action action) => ContextItems.Children.Add(MenuRow(text, action, popup));

        if (single is { IsDirectory: true }) Add(s.ContextOpen, () => vm.OpenCursorCommand.Execute(null));
        if (single is { IsFile: true })
        {
            Add(s.ContextOpenWithDefault, () => act.OpenWithDefaultApp(single.Path));
            Add(s.ContextView, act.View);
            Add(s.ContextEdit, act.Edit);
        }
        if (ContextItems.Children.Count > 0) ContextItems.Children.Add(MenuSeparator());

        if (single is not null) Add(s.ContextRename, act.Rename);
        Add(s.ContextCopy, act.Copy);
        Add(s.ContextDuplicate, act.Duplicate);
        Add(s.ContextMove, act.Move);
        Add(s.ContextDelete, act.Delete);
        Add(s.FnCompress, act.Compress);
        if (single is { IsFile: true } zip && ArchiveService.IsZip(zip.Name)) Add(s.FnExtract, act.Extract);
        ContextItems.Children.Add(MenuSeparator());

        Add(s.ContextProperties, act.Properties);
        if (single is { IsDirectory: true }) Add(s.ContextBookmark, () => act.ToggleBookmark(single.Path));
        Add(s.ContextReveal, () => act.Reveal(picked[0].Path));
        popup.IsOpen = true;
    }

    // --- 끌어다 놓기 (목적지) ---

    ListBoxItem? _dropRow;

    static DropMode ModeOf(KeyModifiers keys) =>
        keys.HasFlag(KeyModifiers.Control) ? DropMode.ForceCopy
        : keys.HasFlag(KeyModifiers.Shift) ? DropMode.ForceMove
        : DropMode.Auto;

    /// <summary>놓을 폴더: 포인터 아래가 (끌고 있지 않은) 폴더 행이면 그 폴더, 아니면 이 패널의 현재 폴더.</summary>
    (string Destination, ListBoxItem? Row) TargetAt(DragEventArgs e, IReadOnlyList<string> sources)
    {
        var hit = Entries.InputHitTest(e.GetPosition(Entries)) as Avalonia.Visual;
        var row = hit?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        if (row?.DataContext is EntryItem { IsDirectory: true } dir && !sources.Contains(dir.Path))
            return (dir.Path, row);
        return (Vm?.CurrentPath ?? "", null);
    }

    IReadOnlyList<string> SourcesOf(DragEventArgs e)
    {
        if (DragSession.Paths is { } own) return own;
        var files = e.DataTransfer.TryGetFiles(); // 다른 프로그램(탐색기 등)에서 끌어온 파일
        return files?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        var sources = SourcesOf(e);
        if (sources.Count == 0 || Vm is null) { e.DragEffects = DragDropEffects.None; ClearDropHighlight(); return; }

        var (destination, row) = TargetAt(e, sources);
        var drives = DragSession.Drives ??= new ListDrives().Invoke();
        // 앱 밖에서 온 파일은 기본으로 복사한다 (Shift로 요청하면 이동).
        var mode = ModeOf(e.KeyModifiers);
        if (DragSession.Paths is null && mode == DropMode.Auto) mode = DropMode.ForceCopy;
        var plan = DropPlanner.Plan(sources, destination, drives, mode);
        var accepted = plan.Action is DropAction.Copy or DropAction.Move;
        e.DragEffects = plan.Action switch
        {
            DropAction.Copy => DragDropEffects.Copy,
            DropAction.Move => DragDropEffects.Move,
            _ => DragDropEffects.None,
        };
        SetDropHighlight(accepted ? row : null, frame: accepted && row is null);
        e.Handled = true;
    }

    void OnDrop(object? sender, DragEventArgs e)
    {
        var sources = SourcesOf(e);
        ClearDropHighlight();
        if (sources.Count == 0 || Vm is not { Actions: { } act }) return;

        var (destination, _) = TargetAt(e, sources);
        var mode = ModeOf(e.KeyModifiers);
        if (DragSession.Paths is null && mode == DropMode.Auto) mode = DropMode.ForceCopy;
        act.Drop(sources, destination, mode);
        e.Handled = true;
    }

    void SetDropHighlight(ListBoxItem? row, bool frame)
    {
        if (!ReferenceEquals(_dropRow, row))
        {
            _dropRow?.Classes.Set("drop", false);
            _dropRow = row;
            _dropRow?.Classes.Set("drop", true);
        }
        DropFrame.IsVisible = frame;
    }

    void ClearDropHighlight() => SetDropHighlight(null, frame: false);

    // --- 즐겨찾기·최근 방문 메뉴 ---
    // 메뉴는 Popup 안의 단순한 버튼 목록이다. 열 때마다 내용을 새로 채워서 설정의 최신 값을 읽는다.

    static TextBlock PathText(string path) => new()
    {
        Text = path,
        MaxWidth = 480,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    static Button MenuRow(object content, Action onClick, Popup popup, bool enabled = true) =>
        PopupMenus.Row(content, onClick, popup, enabled);

    static Border MenuSeparator() => PopupMenus.Separator();

    /// <summary>드라이브(볼륨) 목록. 지금 폴더가 있는 드라이브는 굵게 보인다. 눌러서 그 드라이브의 루트로 이동한다.</summary>
    void FillDrives()
    {
        DriveItems.Children.Clear();
        if (Vm is not { } vm) return;
        var drives = new ListDrives().Invoke();
        var current = ListDrives.DriveOf(vm.CurrentPath, drives);
        foreach (var drive in drives)
        {
            var label = drive.Name == drive.Path ? drive.Path : $"{drive.Name}   {drive.Path}";
            var text = new TextBlock { Text = label, MaxWidth = 420, TextTrimming = TextTrimming.CharacterEllipsis };
            if (drive == current) text.FontWeight = FontWeight.Bold;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new Icons.DuoIcon { Icon = Icons.FileIcons.ForDrive(drive.Kind), Width = 18, Height = 18, ColorKey = "AppPrimary" });
            row.Children.Add(text);
            DriveItems.Children.Add(MenuRow(row, () => _ = vm.Controller.NavigateToAsync(drive.Path), DrivePopup));
        }
    }

    void FillBookmarks()
    {
        BookmarkItems.Children.Clear();
        if (Vm is not { Settings: { } settings } vm) return;
        var s = Strings.Current;
        var marks = settings.Value.Bookmarks;
        if (marks.Count == 0)
            BookmarkItems.Children.Add(MenuRow(s.BookmarkNone, () => { }, BookmarkPopup, enabled: false));
        foreach (var path in marks)
        {
            // 한 줄: [경로 ............ ✕]. 경로를 누르면 이동, ✕를 누르면 즐겨찾기에서 뺀다.
            var go = MenuRow(PathText(path), () => _ = vm.Controller.NavigateToAsync(path), BookmarkPopup);
            var remove = new Button { Classes = { "tool" }, Width = 28, Height = 28, VerticalAlignment = VerticalAlignment.Center };
            remove.Content = new Icons.DuoIcon { Icon = "close", Width = 14, Height = 14 };
            ToolTip.SetTip(remove, s.BookmarkRemoveTooltip);
            remove.Click += (_, _) =>
            {
                settings.ToggleBookmark(path);
                FillBookmarks(); // 열어 둔 채 목록만 새로 그린다
            };
            var line = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(remove, Dock.Right);
            line.Children.Add(remove);
            line.Children.Add(go);
            BookmarkItems.Children.Add(line);
        }
        BookmarkItems.Children.Add(MenuSeparator());
        var here = vm.CurrentPath;
        var marked = marks.Contains(here);
        BookmarkItems.Children.Add(MenuRow(
            marked ? s.BookmarkRemoveCurrent : s.BookmarkAddCurrent, () => settings.ToggleBookmark(here), BookmarkPopup));
    }

    void FillRecent()
    {
        RecentItems.Children.Clear();
        if (Vm is not { Settings: { } settings } vm) return;
        var s = Strings.Current;
        var recent = settings.Value.RecentPaths;
        if (recent.Count == 0)
        {
            RecentItems.Children.Add(MenuRow(s.RecentNone, () => { }, RecentPopup, enabled: false));
            return;
        }
        foreach (var path in recent)
            RecentItems.Children.Add(MenuRow(PathText(path), () => _ = vm.Controller.NavigateToAsync(path), RecentPopup));
        RecentItems.Children.Add(MenuSeparator());
        RecentItems.Children.Add(MenuRow(s.RecentClear, () => settings.Update(x => x with { RecentPaths = [] }), RecentPopup));
    }
}
