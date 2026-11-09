using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaylightCommander.App.ViewModels;
using DaylightCommander.Services;

namespace DaylightCommander.App.Views;

/// <summary>선택된 줄은 글씨를 조금 굵게.</summary>
public static class TreeFonts
{
    public static readonly IValueConverter Weight =
        new FuncValueConverter<bool, FontWeight>(selected => selected ? FontWeight.SemiBold : FontWeight.Medium);
}

/// <summary>
/// 디렉터리 트리 화면 (원본 <c>DirectoryTree</c>): 눌러서 이동 · 폴더 끌어다 놓기(기본 이동, Ctrl이면 복사) · 행 우클릭 메뉴 ·
/// 새로고침(버튼, 5초마다, 패널 목록이 바뀔 때).
/// </summary>
public partial class DirectoryTreeView : UserControl
{
    DirectoryTreeViewModel? Vm => DataContext as DirectoryTreeViewModel;

    readonly DispatcherTimer _reloadTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    PointerPressedEventArgs? _pressArgs;
    Point _pressPoint;
    TreeRow? _pressRow;
    TreeRow? _menuRow;
    bool _dragging;
    Border? _dropRow;

    public DirectoryTreeView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => SetDropRow(null));
        AddHandler(DragDrop.DropEvent, OnDrop);

        ReloadButton.Click += (_, _) => _ = Vm?.ReloadAllAsync();
        _reloadTimer.Tick += (_, _) => _ = Vm?.ReloadAsync();
        AttachedToVisualTree += (_, _) => _reloadTimer.Start();
        DetachedFromVisualTree += (_, _) => _reloadTimer.Stop();
    }

    static TreeRow? RowAt(Avalonia.Visual? source)
    {
        for (var node = source; node is not null; node = node.GetVisualParent())
            if (node is Border { Tag: TreeRow row }) return row;
        return null;
    }

    static Border? RowBorderAt(Avalonia.Visual? source)
    {
        for (var node = source; node is not null; node = node.GetVisualParent())
            if (node is Border { Tag: TreeRow }) return node as Border;
        return null;
    }

    // --- 누르기·끌기·우클릭 ---

    void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is not { } vm) return;
        var props = e.GetCurrentPoint(this).Properties;
        var source = e.Source as Avalonia.Visual;
        if (props.IsRightButtonPressed)
        {
            // 우클릭한 폴더가 곧 선택된 폴더다: 이미 그 폴더가 아니면 먼저 그리로 이동해 강조가 따라오게 한다. 메뉴는 놓을 때 연다.
            // 이동하면 줄이 다시 그려져 놓을 때의 이벤트 대상이 바뀌므로, 누른 줄을 기억해 두었다가 그 줄의 메뉴를 연다.
            _menuRow = RowAt(source);
            if (_menuRow is { Selected: false } target) vm.Select(target);
            return;
        }
        if (!props.IsLeftButtonPressed) return;

        if (source?.FindAncestorOfType<Button>(includeSelf: true) is { Tag: TreeRow toggled })
        {
            vm.Toggle(toggled);
            e.Handled = true;
            return;
        }
        if (RowAt(source) is { } row)
        {
            // 눌렀다 놓으면 이동하고, 누른 채 끌면 끌어다 놓기가 된다.
            _pressArgs = e;
            _pressPoint = e.GetPosition(this);
            _pressRow = row;
        }
    }

    async void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressArgs is not { } pressed || _pressRow is not { } row || _dragging) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { _pressArgs = null; return; }
        var delta = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(delta.X) < 6 && Math.Abs(delta.Y) < 6) return;

        _pressArgs = null;
        _pressRow = null;
        if (row.IsDrive) return; // 드라이브 루트는 끌 수 없다
        _dragging = true;
        try
        {
            DragSession.Paths = [row.Path];
            var data = await DragSession.BuildDataAsync(this, [row.Path]);
            await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
        finally
        {
            DragSession.Paths = null;
            _dragging = false;
            SetDropRow(null);
        }
    }

    void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Vm is not { } vm) return;
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            var row = _pressRow;
            _pressArgs = null;
            _pressRow = null;
            if (row is not null && !_dragging && RowAt(e.Source as Avalonia.Visual)?.Path == row.Path) vm.Select(row);
        }
        else if (e.InitialPressMouseButton == MouseButton.Right && _menuRow is { } menuRow)
        {
            _menuRow = null;
            ShowContextMenu(vm, menuRow);
            e.Handled = true;
        }
    }

    // --- 우클릭 메뉴 ---

    void ShowContextMenu(DirectoryTreeViewModel vm, TreeRow row)
    {
        var s = Strings.Current;
        ContextItems.Children.Clear();
        void Add(string text, Action action, bool enabled = true) =>
            ContextItems.Children.Add(PopupMenus.Row(text, action, ContextPopup, enabled));

        Add(s.ContextOpen, () => vm.Open(row));
        Add(s.DialogNewFolderTitle, () => vm.NewFolder(row));
        if (!row.IsDrive) Add(s.DialogRenameTitle, () => vm.Rename(row));
        ContextItems.Children.Add(PopupMenus.Separator());
        if (!row.IsDrive) Add(s.TreeMenuCut, () => vm.Cut(row));
        Add(s.TreeMenuCopy, () => vm.Copy(row));
        Add(s.TreeMenuPaste, () => vm.Paste(row), enabled: vm.HasClipboard);
        if (!row.IsDrive) Add(s.TreeMenuDelete, () => vm.Delete(row));
        ContextItems.Children.Add(PopupMenus.Separator());
        Add(s.FnProperties, () => vm.Properties(row));
        Add(s.ContextBookmark, () => vm.ToggleBookmark(row));
        Add(s.ContextReveal, () => vm.Reveal(row));
        Add(s.PaneRefreshTooltip, () => _ = vm.ReloadAllAsync());
        ContextPopup.IsOpen = true;
    }

    // --- 끌어다 놓기 (목적지) ---

    static DropMode ModeOf(KeyModifiers keys, bool external) =>
        Mods.Copy(keys) ? DropMode.ForceCopy
        : keys.HasFlag(KeyModifiers.Shift) ? DropMode.ForceMove
        : external ? DropMode.ForceCopy // 앱 밖의 파일은 기본으로 복사한다
        : DropMode.ForceMove;           // 트리 안에서는 기본이 이동 (원본)

    static IReadOnlyList<string> SourcesOf(DragEventArgs e)
    {
        if (DragSession.Paths is { } own) return own;
        var files = e.DataTransfer.TryGetFiles();
        return files?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        var sources = SourcesOf(e);
        var border = RowBorderAt(this.InputHitTest(e.GetPosition(this)) as Avalonia.Visual);
        if (sources.Count == 0 || border?.Tag is not TreeRow row)
        {
            e.DragEffects = DragDropEffects.None;
            SetDropRow(null);
            return;
        }
        var plan = DropPlanner.Plan(sources, row.Path, [], ModeOf(e.KeyModifiers, DragSession.Paths is null));
        var accepted = plan.Action is DropAction.Copy or DropAction.Move;
        e.DragEffects = plan.Action switch
        {
            DropAction.Copy => DragDropEffects.Copy,
            DropAction.Move => DragDropEffects.Move,
            _ => DragDropEffects.None,
        };
        SetDropRow(accepted ? border : null);
        e.Handled = true;
    }

    void OnDrop(object? sender, DragEventArgs e)
    {
        var sources = SourcesOf(e);
        var border = RowBorderAt(this.InputHitTest(e.GetPosition(this)) as Avalonia.Visual);
        SetDropRow(null);
        if (sources.Count == 0 || Vm is not { } vm || border?.Tag is not TreeRow row) return;
        vm.Drop(sources, row, ModeOf(e.KeyModifiers, DragSession.Paths is null));
        e.Handled = true;
    }

    void SetDropRow(Border? row)
    {
        if (ReferenceEquals(_dropRow, row)) return;
        _dropRow?.Classes.Set("drop", false);
        _dropRow = row;
        _dropRow?.Classes.Set("drop", true);
    }
}
