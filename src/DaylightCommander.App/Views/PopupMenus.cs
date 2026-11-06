using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace DaylightCommander.App.Views;

/// <summary>팝업 메뉴(Border.menu 안의 Button.menuitem 줄)를 코드에서 만드는 도우미. 패널·디렉터리 트리의 메뉴가 같이 쓴다.</summary>
internal static class PopupMenus
{
    public static Button Row(object content, Action onClick, Popup popup, bool enabled = true)
    {
        var row = new Button { Classes = { "menuitem" }, Content = content, IsEnabled = enabled };
        row.Click += (_, _) =>
        {
            popup.IsOpen = false;
            onClick();
        };
        return row;
    }

    public static Border Separator()
    {
        var line = new Border { Height = 1, Margin = new Thickness(4, 4) };
        line.Bind(Border.BackgroundProperty, Application.Current!.GetResourceObservable("AppStroke"));
        return line;
    }
}

/// <summary>앱 안에서 끌고 있는 경로들 (패널 행·트리 폴더가 같이 쓴다). 앱 밖에서 오는 드롭은 파일 목록으로 읽는다.</summary>
internal static class DragSession
{
    public static IReadOnlyList<string>? Paths { get; set; }
    public static IReadOnlyList<DaylightCommander.Domain.DriveEntry>? Drives { get; set; }
}
