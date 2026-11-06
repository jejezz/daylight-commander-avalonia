using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DaylightCommander.App.ViewModels;

namespace DaylightCommander.App.Views;

/// <summary>선택된 줄은 글씨를 조금 굵게.</summary>
public static class TreeFonts
{
    public static readonly IValueConverter Weight =
        new FuncValueConverter<bool, FontWeight>(selected => selected ? FontWeight.SemiBold : FontWeight.Medium);
}

public partial class DirectoryTreeView : UserControl
{
    public DirectoryTreeView()
    {
        InitializeComponent();
        // 줄 아무 데나 누르면 그 폴더로 이동, 화살표 버튼은 펼침/접힘만 바꾼다.
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
    }

    void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not DirectoryTreeViewModel vm || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var source = e.Source as Avalonia.Visual;
        if (source?.FindAncestorOfType<Button>(includeSelf: true)?.Tag is TreeRow toggled)
        {
            vm.Toggle(toggled);
            e.Handled = true;
            return;
        }
        for (var node = source; node is not null; node = node.GetVisualParent())
        {
            if (node is Border { Tag: TreeRow row })
            {
                vm.Select(row);
                e.Handled = true;
                return;
            }
        }
    }
}
