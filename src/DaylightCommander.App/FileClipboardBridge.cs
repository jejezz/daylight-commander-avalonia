using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using DaylightCommander.App.Views;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App;

/// <summary>
/// 복사·잘라내기·붙여넣기 (Ctrl+C·X·V, 패널과 트리의 우클릭 메뉴).
/// 앱 클립보드(<see cref="HomeState.Clipboard"/>)에 담는 동시에 시스템 클립보드에도 파일로 담아 탐색기 같은 바깥 앱과 주고받는다.
/// 붙여넣을 때 시스템 클립보드의 파일이 앱이 담은 것과 다르면 바깥 앱에서 복사한 것으로 보고 그 파일을 복사한다.
/// </summary>
internal static class FileClipboardBridge
{
    /// <summary>활성 패널의 선택(없으면 커서) 항목을 담는다.</summary>
    public static Task CopySelectionAsync(Visual source, HomeState state, bool cut) =>
        PublishAsync(source, state.CopySelectionToClipboard(cut));

    /// <summary>지정한 항목을 담는다 (트리의 폴더).</summary>
    public static Task CopyAsync(Visual source, HomeState state, IReadOnlyList<string> paths, bool cut)
    {
        state.CopyToClipboard(paths, cut);
        return PublishAsync(source, paths);
    }

    /// <summary><paramref name="targetDir"/>에 붙인다: 바깥 앱에서 복사한 파일이 있으면 그것을, 아니면 앱 클립보드의 항목을.</summary>
    public static async Task PasteAsync(Visual source, HomeState state, string targetDir)
    {
        var clipboard = TopLevel.GetTopLevel(source)?.Clipboard;
        var external = await ExternalFilesAsync(clipboard, state.Clipboard);
        if (external is { Count: > 0 })
        {
            state.PasteExternal(external, targetDir);
            return;
        }
        var wasCut = state.Clipboard is { Cut: true };
        state.PasteClipboard(targetDir);
        // 잘라낸 것은 한 번만 붙는다: 시스템 클립보드에 남은 옛 경로가 다음 붙여넣기에 다시 쓰이지 않게 비운다.
        if (wasCut && clipboard is not null) await Try(clipboard.ClearAsync);
    }

    /// <summary>붙여넣을 것이 있는지 (메뉴의 사용 가능 여부): 앱 클립보드에 담은 것이 있거나 시스템 클립보드에 파일이 있다.</summary>
    public static async Task<bool> CanPasteAsync(Visual source, HomeState state)
    {
        if (state.Clipboard is not null) return true;
        if (TopLevel.GetTopLevel(source)?.Clipboard is not { } clipboard) return false;
        try
        {
            var formats = await clipboard.GetDataFormatsAsync();
            return formats.Contains(DataFormat.File);
        }
        catch (Exception) { return false; }
    }

    static async Task PublishAsync(Visual source, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || TopLevel.GetTopLevel(source)?.Clipboard is not { } clipboard) return;
        try
        {
            // 원격 항목은 바깥 앱에 파일로 줄 수 없다. 경로 글자만 담아, 전에 담겨 있던 파일이 붙여넣기를 가로채지 않게 한다.
            DataTransfer data;
            if (paths.Any(Locations.IsRemote))
            {
                data = new DataTransfer();
                data.Add(DataTransferItem.Create(DataFormat.Text, string.Join("\n", paths)));
            }
            else data = await DragSession.BuildDataAsync(source, paths);
            await clipboard.SetDataAsync(data);
        }
        catch (Exception) { /* 시스템 클립보드를 못 쓰면 앱 안에서만 붙여넣을 수 있다 */ }
    }

    static async Task<IReadOnlyList<string>?> ExternalFilesAsync(IClipboard? clipboard, FileClipboard? own)
    {
        if (clipboard is null) return null;
        try
        {
            var files = await clipboard.TryGetFilesAsync();
            var paths = files?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
            if (paths is not { Count: > 0 }) return null;
            return own is not null && SamePaths(own.Paths, paths) ? null : paths;
        }
        catch (Exception) { return null; }
    }

    static bool SamePaths(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        static string Key(string p) => Path.TrimEndingDirectorySeparator(p);
        var comparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return a.Select(Key).ToHashSet(comparer).SetEquals(b.Select(Key));
    }

    static async Task Try(Func<Task> action)
    {
        try { await action(); }
        catch (Exception) { /* 시스템 클립보드를 못 써도 앱 안의 붙여넣기는 끝났다 */ }
    }
}
