using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DaylightCommander.Data;
using DaylightCommander.Domain;
using DaylightCommander.Domain.Settings;
using DaylightCommander.Services;

namespace DaylightCommander.App;

public partial class App : Application
{
    /// <summary>앱 설정의 단일 출처. 모든 창이 이것을 읽고 바꾼다.</summary>
    public static SettingsController Settings { get; private set; } = null!;

    /// <summary>화면에 이미 적용한 설정. 방문 기록 같은 다른 설정이 바뀔 때마다 외형을 다시 적용하지 않으려고 둔다.</summary>
    AppSettings? _applied;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Settings = new SettingsController(new JsonSettingsRepository(JsonSettingsRepository.DefaultFile(AppIdentity.FileName)));

        // 창을 만들기 전에 색·글꼴·언어를 정해 둔다 (첫 프레임부터 올바른 모습).
        ApplyAppearance(Settings.Value);
        Settings.Changed += s => Dispatcher.UIThread.Post(() => ApplyAppearance(s));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => Settings.Dispose(); // 종료할 때 마지막 변경까지 저장
        }

        base.OnFrameworkInitializationCompleted();
    }

    void ApplyAppearance(AppSettings s)
    {
        var previous = _applied;
        _applied = s;
        if (previous is null || previous.Theme != s.Theme || previous.Palette != s.Palette
            || previous.TextScale != s.TextScale || previous.UiFontFamily != s.UiFontFamily)
            ThemeService.Apply(s);
        if (previous is null || previous.Locale != s.Locale) Strings.Apply(s.Locale);
    }
}
