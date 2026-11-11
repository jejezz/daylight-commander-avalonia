using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace DaylightCommander.App.Views;

/// <summary>
/// 끄는 동안 커서 옆에 따라다니는 이름표. 운영체제는 커서 아이콘 하나만 그려 주므로, 무엇을 끄는지 알 수 있게
/// 이름(여러 개면 "이름 외 N개")을 작은 창으로 띄운다. 클릭이 통과하는 창이라 놓을 곳을 가리지 않는다. Windows 전용.
/// </summary>
internal sealed partial class DragGhost : IDisposable
{
    readonly Window _window;
    readonly DispatcherTimer _timer;

    DragGhost(string text, string? others, bool folder)
    {
        var res = Application.Current!;
        IBrush Brush(string key, IBrush fallback) =>
            res.TryFindResource(key, res.ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;

        _window = new Window
        {
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            CanResize = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            IsHitTestVisible = false,
            Focusable = false,
            Content = new Border
            {
                Background = Brush("AppSurface", Brushes.DimGray),
                BorderBrush = Brush("AppPrimary", Brushes.SteelBlue),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5),
                Opacity = 0.92,
                MaxWidth = 360,
                // 이름만 줄여 쓰고 "외 N개"는 항상 보이게 한다.
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        Label((folder ? "📁 " : "📄 ") + text, 0, Brush("AppTextHi", Brushes.White), true),
                        Label(others ?? "", 1, Brush("AppTextMid", Brushes.LightGray), false),
                    },
                },
            },
        };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Follow();
    }

    static TextBlock Label(string text, int column, IBrush foreground, bool trim)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
        };
        Grid.SetColumn(block, column);
        return block;
    }

    /// <summary>이름표를 띄운다. 만들 수 없는 환경이면 null (끌기는 그대로 된다).</summary>
    public static DragGhost? Show(IReadOnlyList<string> paths)
    {
        if (!OperatingSystem.IsWindows() || Application.Current is null || paths.Count == 0) return null;
        try
        {
            var first = Path.GetFileName(paths[0].TrimEnd('\\', '/'));
            if (first.Length == 0) first = paths[0];
            var others = paths.Count == 1 ? null : Strings.Current.DragGhostOthers(paths.Count - 1);
            var ghost = new DragGhost(first, others, Directory.Exists(paths[0]));
            ghost._window.Show();
            ghost.MakeClickThrough();
            ghost.Follow();
            ghost._timer.Start();
            return ghost;
        }
        catch (Exception) { return null; }
    }

    void MakeClickThrough()
    {
        if (_window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == 0) return;
        const int GwlExStyle = -20;
        const nint WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
        var style = GetWindowLongPtr(hwnd, GwlExStyle);
        SetWindowLongPtr(hwnd, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    void Follow()
    {
        if (!GetCursorPos(out var p)) return;
        _window.Position = new PixelPoint(p.X + 16, p.Y + 18);
    }

    public void Dispose()
    {
        _timer.Stop();
        _window.Close();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
