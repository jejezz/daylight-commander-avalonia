using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using DaylightCommander.App.ViewModels;
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

    void OnEntryPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(Entries);
        if (!point.Properties.IsLeftButtonPressed) return;
        var row = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        if (row?.DataContext is not EntryItem item || Vm is not { } vm) return;

        vm.Click(item, ctrl: e.KeyModifiers.HasFlag(KeyModifiers.Control), shift: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        Entries.Focus(); // 경로 표시줄에 있던 키보드 초점을 목록으로 돌려 단축키가 듣게 한다
        if (e.ClickCount == 2) vm.OpenCursorCommand.Execute(null);
        e.Handled = true;
    }

    // --- 즐겨찾기·최근 방문 메뉴 ---
    // 메뉴는 Popup 안의 단순한 버튼 목록이다. 열 때마다 내용을 새로 채워서 설정의 최신 값을 읽는다.

    static TextBlock PathText(string path) => new()
    {
        Text = path,
        MaxWidth = 480,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    static Button MenuRow(object content, Action onClick, Popup popup, bool enabled = true)
    {
        var row = new Button { Classes = { "menuitem" }, Content = content, IsEnabled = enabled };
        row.Click += (_, _) =>
        {
            popup.IsOpen = false;
            onClick();
        };
        return row;
    }

    static Border MenuSeparator()
    {
        var line = new Border { Height = 1, Margin = new Thickness(4, 4) };
        line.Bind(Border.BackgroundProperty, Application.Current!.GetResourceObservable("AppStroke"));
        return line;
    }

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
            DriveItems.Children.Add(MenuRow(text, () => _ = vm.Controller.NavigateToAsync(drive.Path), DrivePopup));
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
            remove.Content = new PathIcon { Data = (Geometry?)Application.Current!.FindResource("IconClose"), Width = 14, Height = 14 };
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
