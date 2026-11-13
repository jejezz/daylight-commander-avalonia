using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DaylightCommander.App.Updates;
using DaylightCommander.Data;
using DaylightCommander.Domain;
using DaylightCommander.Domain.Settings;
using DaylightCommander.Services;
using DaylightCommander.Services.Updates;

namespace DaylightCommander.App;

public partial class App : Application
{
    /// <summary>앱 설정의 단일 출처. 모든 창이 이것을 읽고 바꾼다.</summary>
    public static SettingsController Settings { get; private set; } = null!;

    /// <summary>연결된 원격 서버들. 모든 창이 나눠 쓴다.</summary>
    public static RemoteSessions Remote { get; private set; } = null!;

    /// <summary>업데이트 확인·설치. 서버 주소가 없는 빌드(<c>-p:AppUpdateServer=</c>)나 로컬 빌드는 null.</summary>
    internal static UpdateService? Updates { get; private set; }

    /// <summary>화면에 이미 적용한 설정. 방문 기록 같은 다른 설정이 바뀔 때마다 외형을 다시 적용하지 않으려고 둔다.</summary>
    AppSettings? _applied;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // macOS 메뉴 막대의 앱 메뉴 제목(굵은 글씨)과 "… 숨기기"·"… 종료" 항목은 Application.Name을 쓴다.
        // 정하지 않으면 Avalonia 기본값 "Avalonia Application"이 보인다.
        Name = AppIdentity.DisplayName;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Settings = new SettingsController(new JsonSettingsRepository(JsonSettingsRepository.DefaultFile(AppIdentity.FileName)));

        // 창을 만들기 전에 색·글꼴·언어를 정해 둔다 (첫 프레임부터 올바른 모습).
        Remote = new RemoteSessions(new SettingsHostKeyStore(Settings));
        RemoteFiles.CleanOld(); // 지난번에 받아 둔 원격 파일 사본 중 오래된 것을 지운다
        ApplyAppearance(Settings.Value);
        Settings.Changed += s => Dispatcher.UIThread.Post(() => ApplyAppearance(s));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            MacAppMenu.Install(this, desktop);
            Updates = UpdateService.Create(
                FileUpdateStateStore.BesideSettings(JsonSettingsRepository.DefaultFile(AppIdentity.FileName)),
                owner: () => desktop.Windows.OfType<MainWindow>().FirstOrDefault(w => w.IsActive) ?? desktop.Windows.OfType<MainWindow>().FirstOrDefault(),
                // 파일 작업이 진행 중이면 알리지도, 설치하지도 않는다 (설치는 앱을 종료시킨다).
                isBusy: () => desktop.Windows.OfType<MainWindow>().Any(w => w.HasUnfinishedJobs));
            Updates?.StartAutomaticCheck();
            desktop.Exit += (_, _) => { Remote.Dispose(); Settings.Dispose(); }; // 종료할 때 마지막 변경까지 저장
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
