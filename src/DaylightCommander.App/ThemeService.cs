using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using DaylightCommander.Domain.Settings;
using DaylightCommander.Domain.Theme;

namespace DaylightCommander.App;

/// <summary>
/// 모드·색 구성을 앱 전체에 적용한다. 색은 <c>AppBg</c>, <c>AppSurface</c> 같은 이름의 리소스로 내려가고,
/// 화면은 <c>{DynamicResource ...}</c>로만 읽는다 (색을 직접 적지 않는다 — theming.md). 바뀌면 열려 있는 모든 창이 같이 바뀐다.
/// 5단계에서 설정 창과 영속화가 이 서비스를 부른다.
/// </summary>
static class ThemeService
{
    public static ThemeMode Mode { get; private set; } = ThemeMode.System;
    public static ColorPalette Palette { get; private set; } = ColorPalette.Saturn;

    public static bool IsDark(Application app) => Mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => app.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark,
    };

    /// <summary>번들한 기본 글꼴 (SeoulNamsan 400/700/800, fonts.md).</summary>
    const string DefaultFontUri = "avares://DaylightCommander/Assets/Fonts#SeoulNamsan";

    /// <summary>
    /// 설정 전체(모드·색 구성·글꼴·글자 크기)를 앱에 적용한다. 열려 있는 모든 창이 같이 바뀐다.
    /// 글꼴은 이름이 비었거나 기본 이름이면 번들 글꼴, 그 밖에는 시스템에 설치된 그 이름의 글꼴이다
    /// (시스템에서 못 찾으면 번들 글꼴로 돌아간다). 글자 크기는 13/12/11/16을 배율만큼 키우거나 줄인다 (ui-ux.md §4).
    /// </summary>
    public static void Apply(AppSettings settings)
    {
        Apply(settings.Theme, settings.Palette);
        var app = Application.Current!;
        app.Resources["AppFont"] = FontFamilyFor(settings.UiFontFamily);
        var scale = settings.TextScale;
        app.Resources["FsBody"] = 13 * scale;
        app.Resources["FsMid"] = 12 * scale;
        app.Resources["FsSmall"] = 11 * scale;
        app.Resources["FsTitle"] = 16 * scale;
    }

    public const string DefaultFontName = "SeoulNamsan";

    static FontFamily FontFamilyFor(string? name)
    {
        var bundled = new FontFamily(DefaultFontUri);
        if (string.IsNullOrWhiteSpace(name) || name == DefaultFontName) return bundled;
        return FontManager.Current.SystemFonts.Any(f => f.Name == name) ? new FontFamily(name) : bundled;
    }

    public static void Apply(ThemeMode mode, ColorPalette palette)
    {
        var app = Application.Current ?? throw new InvalidOperationException("앱이 아직 없다");
        Mode = mode;
        Palette = palette;
        app.RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        var dark = IsDark(app);
        var c = AppColors.For(palette, dark);
        Brush(app, "AppBg", c.Bg);
        Brush(app, "AppSurface", c.Surface);
        Brush(app, "AppSurfaceHi", c.SurfaceHi);
        Brush(app, "AppStroke", c.Stroke);
        Brush(app, "AppStrokeStrong", c.StrokeStrong);
        Brush(app, "AppTextHi", c.TextHi);
        Brush(app, "AppTextMid", c.TextMid);
        Brush(app, "AppPrimary", c.Primary);
        Brush(app, "AppOnPrimary", c.OnPrimary);
        Brush(app, "AppAccent", c.Accent);
        Brush(app, "AppDanger", c.Danger);
        // 파일 아이콘 색 (투톤 아이콘): 라이트는 진하게, 다크는 밝게 해서 두 모드 모두에서 또렷하다.
        Brush(app, "AppFolder", dark ? 0xFFF2BC4Eu : 0xFFE3A12Bu);
        Brush(app, "AppIconFolder", dark ? 0xFFF2BC4Eu : 0xFFE3A12Bu);
        Brush(app, "AppIconArchive", dark ? 0xFFF0A940u : 0xFFD98A1Fu);
        Brush(app, "AppIconPdf", dark ? 0xFFFF7A70u : 0xFFD6453Du);
        Brush(app, "AppIconImage", dark ? 0xFF6FC3F0u : 0xFF2F8FCCu);
        Brush(app, "AppIconVideo", dark ? 0xFF4DD0C8u : 0xFF1A9E96u);
        Brush(app, "AppIconAudio", dark ? 0xFFFF7FB6u : 0xFFD4488Au);
        Brush(app, "AppIconCode", dark ? 0xFFB093FFu : 0xFF7C4DDBu);
        Brush(app, "AppIconText", dark ? 0xFF8FB0CFu : 0xFF5B7A99u);
        Brush(app, "AppIconDoc", dark ? 0xFF7FA4FFu : 0xFF3C6FD8u);
        Brush(app, "AppIconSheet", dark ? 0xFF5CCB88u : 0xFF2E9E5Bu);
        Brush(app, "AppIconSlide", dark ? 0xFFFF9A5Cu : 0xFFE2702Bu);
        Brush(app, "AppIconApp", dark ? 0xFFA3AEC0u : 0xFF6E7B8Fu);
        // 작업 알림 상자: 한 단계 띄운 면에 주색을 20% 섞은 불투명색 + 주색 테두리. (기능 키 바를 덮으므로 투명하면 안 된다)
        Brush(app, "AppNoticeBg", Blend(c.SurfaceHi, c.Primary, 0.20));
        Brush(app, "AppNoticeBorder", c.Primary);
        Brush(app, "AppCursor", (c.Primary & 0x00FFFFFFu) | 0x4D000000u); // 커서 행: 주색 30%
        Brush(app, "AppHover", (c.TextHi & 0x00FFFFFFu) | 0x14000000u);   // 마우스 올림: 글자색 8%

        // Fluent 컨트롤(버튼·선택 색 등)도 같은 주색을 쓰게 한다.
        foreach (var fluent in app.Styles.OfType<FluentTheme>())
        {
            foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                fluent.Palettes[variant] = new ColorPaletteResources
                {
                    Accent = Color.FromUInt32(c.Primary),
                    RegionColor = Color.FromUInt32(c.Bg),
                };
            }
        }
    }

    /// <summary><paramref name="over"/>를 <paramref name="amount"/>(0~1)만큼 <paramref name="under"/> 위에 섞은 불투명색.</summary>
    static uint Blend(uint under, uint over, double amount)
    {
        static uint Mix(uint a, uint b, int shift, double t) =>
            (uint)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t) << shift;
        return 0xFF000000u | Mix(under, over, 16, amount) | Mix(under, over, 8, amount) | Mix(under, over, 0, amount);
    }

    static void Brush(Application app, string key, uint argb) =>
        app.Resources[key] = new SolidColorBrush(Color.FromUInt32(argb));
}
