using Avalonia;
using System;

namespace DaylightCommander.App;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // macOS 앱 메뉴는 Avalonia 기본(영어 고정 "About … / Hide … / Quit …") 대신 앱 언어로 직접 만든다 (MacAppMenu).
            .With(new MacOSPlatformOptions { DisableDefaultApplicationMenuItems = true })
            .LogToTrace();
}
