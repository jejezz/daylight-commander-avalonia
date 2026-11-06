using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DaylightCommander.App.ViewModels;
using DaylightCommander.Services;

namespace DaylightCommander.App.Views;

/// <summary>
/// 1패널 모드에서 접힌 패널을 가장자리에 세워 둔 좁은 막대 (원본 <c>_DockedRail</c>). 열린 폴더 아이콘과 세로로 쓴 폴더 이름이 보이고,
/// 누르면 그 패널이 펼쳐지며, 파일을 끌어다 놓으면 그 패널의 현재 폴더로 복사/이동한다 (같은 드라이브면 이동).
/// </summary>
public sealed class PaneRail : Border
{
    readonly TextBlock _name = new() { MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    PaneViewModel? _vm;
    bool _active;

    PaneViewModel? Vm => DataContext as PaneViewModel;

    public PaneRail()
    {
        Classes.Add("card");
        Cursor = new Cursor(StandardCursorType.Hand);
        Background = Brushes.Transparent;

        var icon = new Icons.DuoIcon { Icon = "folder-open", ColorKey = "AppPrimary", Width = 18, Height = 18, Margin = new Thickness(0, 10, 0, 8), HorizontalAlignment = HorizontalAlignment.Center };
        var rotated = new LayoutTransformControl { LayoutTransform = new RotateTransform(90), Child = _name, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        var column = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Top);
        column.Children.Add(icon);
        column.Children.Add(new Border { Child = rotated, ClipToBounds = true });
        Child = column;

        // 눌러서 이 패널로 전환한다.
        AddHandler(PointerPressedEvent, (_, _) => Vm?.RequestActivate(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => SetActive(false));
        AddHandler(DragDrop.DropEvent, OnDrop);

        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
            _vm = Vm;
            if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
            Refresh();
        };
    }

    void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PaneViewModel.CurrentPath)) Refresh();
    }

    void Refresh()
    {
        var path = Vm?.CurrentPath ?? "";
        _name.Text = path.Split('\\', '/').LastOrDefault(p => p.Length > 0) ?? path;
        ToolTip.SetTip(this, Strings.Current.DockedPaneTooltip(path));
    }

    void SetActive(bool on)
    {
        if (_active == on) return;
        _active = on;
        if (on)
        {
            this.Bind(BackgroundProperty, Application.Current!.GetResourceObservable("AppCursor"));
            this.Bind(BorderBrushProperty, Application.Current!.GetResourceObservable("AppPrimary"));
        }
        else
        {
            this.ClearValue(BackgroundProperty);
            this.ClearValue(BorderBrushProperty);
            Background = Brushes.Transparent;
        }
    }

    static IReadOnlyList<string> SourcesOf(DragEventArgs e)
    {
        if (DragSession.Paths is { } own) return own;
        var files = e.DataTransfer.TryGetFiles();
        return files?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
    }

    static DropMode ModeOf(KeyModifiers keys) =>
        Mods.Copy(keys) ? DropMode.ForceCopy
        : keys.HasFlag(KeyModifiers.Shift) ? DropMode.ForceMove
        : DragSession.Paths is null ? DropMode.ForceCopy // 앱 밖의 파일은 기본으로 복사
        : DropMode.Auto;                                 // 같은 드라이브면 이동, 아니면 복사

    void OnDragOver(object? sender, DragEventArgs e)
    {
        var sources = SourcesOf(e);
        if (Vm is not { } vm || sources.Count == 0) { e.DragEffects = DragDropEffects.None; SetActive(false); return; }
        var drives = DragSession.Drives ??= new ListDrives().Invoke();
        var plan = DropPlanner.Plan(sources, vm.CurrentPath, drives, ModeOf(e.KeyModifiers));
        e.DragEffects = plan.Action switch
        {
            DropAction.Copy => DragDropEffects.Copy,
            DropAction.Move => DragDropEffects.Move,
            _ => DragDropEffects.None,
        };
        SetActive(plan.Action is DropAction.Copy or DropAction.Move);
        e.Handled = true;
    }

    void OnDrop(object? sender, DragEventArgs e)
    {
        var sources = SourcesOf(e);
        SetActive(false);
        if (Vm is not { Actions: { } act } vm || sources.Count == 0) return;
        act.Drop(sources, vm.CurrentPath, ModeOf(e.KeyModifiers));
        e.Handled = true;
    }
}
