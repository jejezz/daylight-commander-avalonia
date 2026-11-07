using DaylightCommander.Domain.Settings;
using DaylightCommander.Domain.Theme;

namespace DaylightCommander.Domain.Tests;

public class AppSettingsTests
{
    [Fact]
    public void 기본값은_원본과_같다_시스템_테마_새턴_보통_글자()
    {
        var s = new AppSettings();
        Assert.Equal(ThemeMode.System, s.Theme);
        Assert.Equal(ColorPalette.Saturn, s.Palette);
        Assert.Equal(1.0, s.TextScale);
        Assert.Null(s.Locale);
        Assert.Null(s.UiFontFamily);
        Assert.Equal(EditorMode.Builtin, s.EditorMode);
        Assert.Null(s.ExternalEditor);
    }

    [Fact]
    public void 글자_크기는_범위_안_5퍼센트_단위로_맞춘다()
    {
        Assert.Equal(1.15, new AppSettings().WithTextScale(1.14).TextScale);
        Assert.Equal(TextScales.Max, new AppSettings().WithTextScale(3).TextScale);
        Assert.Equal(TextScales.Min, new AppSettings().WithTextScale(0.1).TextScale);
        Assert.Equal(1.0, TextScales.Snap(double.NaN));
    }

    [Fact]
    public void 범위_밖_배율은_범위_안으로_고쳐_읽는다()
    {
        var s = (new AppSettings() with { TextScale = 1.7 }).Normalized();
        Assert.Equal(TextScales.Max, s.TextScale);
        Assert.Same(s, s.Normalized());
        Assert.Equal(1.25, (new AppSettings() with { TextScale = 1.25 }).Normalized().TextScale);
    }

    [Fact]
    public void 색_구성은_원본의_여섯_가지와_주색이다()
    {
        Assert.Equal(6, Enum.GetValues<ColorPalette>().Length);
        Assert.Equal(0xFF4C9DFFu, ColorPalette.Saturn.DarkPrimary());
        Assert.Equal(0xFF2C6BE0u, ColorPalette.Saturn.LightPrimary());
        Assert.Equal(0xFFFFD60Au, ColorPalette.HighContrast.DarkPrimary());
    }

    [Fact]
    public void 방문하면_맨_앞에_오고_중복은_하나로_합쳐진다()
    {
        var s = new AppSettings().WithVisit("/a").WithVisit("/b").WithVisit("/a");
        Assert.Equal(["/a", "/b"], s.RecentPaths);
    }

    [Fact]
    public void 최근_경로는_20개까지만_남는다()
    {
        var s = new AppSettings();
        for (var i = 1; i <= 30; i++) s = s.WithVisit($"/p{i}");
        Assert.Equal(AppSettings.MaxRecent, s.RecentPaths.Count);
        Assert.Equal("/p30", s.RecentPaths[0]);
        Assert.Equal("/p11", s.RecentPaths[^1]);
    }

    [Fact]
    public void 북마크는_토글되고_순서가_유지된다()
    {
        var s = new AppSettings().WithBookmarkToggled("/a").WithBookmarkToggled("/b");
        Assert.Equal(["/a", "/b"], s.Bookmarks);
        s = s.WithBookmarkToggled("/a");
        Assert.Equal(["/b"], s.Bookmarks);
    }

    [Fact]
    public void 외부_편집기는_외부_모드이고_실행_파일이_있을_때만_값이_있다()
    {
        Assert.Null((new AppSettings() with { EditorExecutable = "C:\\ed.exe" }).ExternalEditor); // 내장 모드
        Assert.Null((new AppSettings() with { EditorMode = EditorMode.External }).ExternalEditor); // 실행 파일 없음
        Assert.Null((new AppSettings() with { EditorMode = EditorMode.External, EditorExecutable = "  " }).ExternalEditor);
        Assert.Equal("C:\\ed.exe",
            (new AppSettings() with { EditorMode = EditorMode.External, EditorExecutable = "C:\\ed.exe" }).ExternalEditor);
    }

    [Fact]
    public void 언어는_설정이_있으면_그대로_없으면_OS가_한국어일_때만_한국어다()
    {
        Assert.Equal(AppLocale.English, Localization.Resolve(AppLocale.English, "ko"));
        Assert.Equal(AppLocale.Korean, Localization.Resolve(AppLocale.Korean, "en"));
        Assert.Equal(AppLocale.Korean, Localization.Resolve(null, "ko"));
        Assert.Equal(AppLocale.Korean, Localization.Resolve(null, "KO"));
        Assert.Equal(AppLocale.English, Localization.Resolve(null, "en"));
        Assert.Equal(AppLocale.English, Localization.Resolve(null, "ja"));
        Assert.Equal(AppLocale.English, Localization.Resolve(null, ""));
    }

    [Fact]
    public void 트리_폭은_허용_범위로_고친다()
    {
        Assert.False(new AppSettings().TreeDockOpen);
        Assert.Equal(AppSettings.DefaultTreeDockWidth, new AppSettings().TreeDockWidth);
        Assert.Equal(AppSettings.MaxTreeDockWidth, (new AppSettings() with { TreeDockWidth = 9999 }).Normalized().TreeDockWidth);
        Assert.Equal(AppSettings.MinTreeDockWidth, (new AppSettings() with { TreeDockWidth = 10 }).Normalized().TreeDockWidth);
        Assert.Equal(AppSettings.DefaultTreeDockWidth, (new AppSettings() with { TreeDockWidth = double.NaN }).Normalized().TreeDockWidth);
    }
}
