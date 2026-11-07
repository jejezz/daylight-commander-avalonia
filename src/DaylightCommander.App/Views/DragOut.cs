using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using DaylightCommander.Domain;

namespace DaylightCommander.App.Views;

/// <summary>
/// 앱 밖(탐색기·바탕화면·다른 프로그램)으로 끌어낼 때 쓰는 데이터를 만든다. 경로를 텍스트로만 실으면 다른 프로그램은 받지 못하므로,
/// 로컬 파일·폴더는 파일 항목(<see cref="DataTransferItem.CreateFile"/>)으로 싣는다. 서버 경로는 로컬 파일이 아니라 싣지 않는다.
/// 앱 안의 끌어다 놓기는 <see cref="DragSession"/>이 우선이라 이 데이터를 쓰지 않는다.
/// </summary>
static class DragOut
{
    public static async Task<DataTransfer> BuildAsync(Visual source, IReadOnlyList<string> paths)
    {
        var data = new DataTransfer();
        var provider = TopLevel.GetTopLevel(source)?.StorageProvider;
        if (provider is not null)
        {
            foreach (var path in paths.Where(p => !Locations.IsRemote(p)))
            {
                try
                {
                    IStorageItem? item = Directory.Exists(path)
                        ? await provider.TryGetFolderFromPathAsync(new Uri(path))
                        : await provider.TryGetFileFromPathAsync(new Uri(path));
                    if (item is not null) data.Add(DataTransferItem.CreateFile(item));
                }
                catch (Exception e) when (e is IOException or UriFormatException or UnauthorizedAccessException)
                {
                    // 만들지 못한 항목은 건너뛴다.
                }
            }
        }
        // 파일 항목을 하나도 못 만들었으면 (서버 경로 등) 경로를 텍스트로라도 싣는다.
        if (data.Items.Count == 0) data.Add(DataTransferItem.CreateText(string.Join("\n", paths)));
        return data;
    }
}
