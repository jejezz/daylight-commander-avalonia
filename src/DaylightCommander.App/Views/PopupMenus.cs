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

    /// <summary>끌기 데이터: 탐색기 같은 바깥 앱이 받을 수 있게 파일 형식으로 담는다. 파일로 풀지 못하면 경로 글자로 대신한다.</summary>
    public static async Task<Avalonia.Input.DataTransfer> BuildDataAsync(Visual source, IReadOnlyList<string> paths)
    {
        var data = new Avalonia.Input.DataTransfer();
        var storage = TopLevel.GetTopLevel(source)?.StorageProvider;
        var added = 0;
        if (storage is not null)
        {
            foreach (var path in paths)
            {
                try
                {
                    Avalonia.Platform.Storage.IStorageItem? item = Directory.Exists(path)
                        ? await storage.TryGetFolderFromPathAsync(new Uri(path))
                        : await storage.TryGetFileFromPathAsync(new Uri(path));
                    if (item is null) continue;
                    data.Add(Avalonia.Input.DataTransferItem.CreateFile(item));
                    added++;
                }
                catch (Exception) { /* 이 항목만 건너뛴다 */ }
            }
        }
        if (added == 0)
            data.Add(Avalonia.Input.DataTransferItem.Create(Avalonia.Input.DataFormat.Text, string.Join("\n", paths)));
        return data;
    }
}
