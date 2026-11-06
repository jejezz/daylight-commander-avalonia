using System.Text.Json;
using DaylightCommander.Data;
using DaylightCommander.Domain.Settings;
using DaylightCommander.Domain.Theme;
using DaylightCommander.Domain.Window;

namespace DaylightCommander.Data.Tests;

public sealed class JsonSettingsRepositoryTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("dc-settings-test").FullName;
    string File_ => Path.Combine(_dir, "sub", "settings.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    JsonSettingsRepository Repo() => new(File_);

    void WriteRaw(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, json);
    }

    JsonElement Saved()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(File_));
        return doc.RootElement.Clone();
    }

    static readonly AppSettings Full = new()
    {
        Theme = ThemeMode.Dark,
        Palette = ColorPalette.Rose,
        TextScale = 1.15,
        Locale = AppLocale.English,
        UiFontFamily = "Malgun Gothic",
        EditorMode = EditorMode.External,
        EditorExecutable = "C:\\Tools\\ed.exe",
        Bookmarks = ["C:\\a", "C:\\b"],
        RecentPaths = ["C:\\b", "C:\\a"],
        LastLeftPath = "C:\\left",
        LastRightPath = "C:\\right",
        TreeDockOpen = true,
        TreeDockWidth = 300,
        Window = new WindowPlacement { X = 10, Y = 20, Width = 1000, Height = 600, Scale = 1.5, Maximized = true },
    };

    [Fact]
    public void 저장한_값을_그대로_다시_읽는다()
    {
        Repo().Save(Full);
        var back = Repo().Load();
        Assert.Equal(Full.Theme, back.Theme);
        Assert.Equal(Full.Palette, back.Palette);
        Assert.Equal(Full.TextScale, back.TextScale);
        Assert.Equal(Full.Locale, back.Locale);
        Assert.Equal(Full.UiFontFamily, back.UiFontFamily);
        Assert.Equal(Full.EditorMode, back.EditorMode);
        Assert.Equal(Full.EditorExecutable, back.EditorExecutable);
        Assert.Equal(Full.Bookmarks, back.Bookmarks);
        Assert.Equal(Full.RecentPaths, back.RecentPaths);
        Assert.Equal(Full.LastLeftPath, back.LastLeftPath);
        Assert.Equal(Full.LastRightPath, back.LastRightPath);
        Assert.Equal(Full.Window, back.Window);
        Assert.True(back.TreeDockOpen);
        Assert.Equal(300, back.TreeDockWidth);
    }

    [Fact]
    public void 파일이_없으면_기본값()
    {
        var s = Repo().Load();
        Assert.Equal(ThemeMode.System, s.Theme);
        Assert.Equal(ColorPalette.Saturn, s.Palette);
    }

    [Fact]
    public void 저장하면_폴더를_만들고_임시_파일을_남기지_않는다()
    {
        Repo().Save(Full);
        Assert.True(File.Exists(File_));
        Assert.False(File.Exists(File_ + ".tmp"));
    }

    [Fact]
    public void 깨진_파일은_기본값으로_읽고_broken으로_옮겨_둔다()
    {
        WriteRaw("{ 이건 JSON이 아니다");
        var s = Repo().Load();
        Assert.Equal(ThemeMode.System, s.Theme);
        Assert.False(File.Exists(File_));
        Assert.Equal("{ 이건 JSON이 아니다", File.ReadAllText(File_ + ".broken"));
    }

    [Fact]
    public void 모르는_키는_무시하고_없는_키는_기본값이다()
    {
        WriteRaw("""{ "theme_mode": "dark", "future_setting": { "a": 1 } }""");
        var s = Repo().Load();
        Assert.Equal(ThemeMode.Dark, s.Theme);
        Assert.Equal(ColorPalette.Saturn, s.Palette);
    }

    [Fact]
    public void 모르는_열거값은_기본값으로_읽는다()
    {
        WriteRaw("""{ "theme_mode": "sepia", "accent_palette": "neon", "editor_mode": "magic" }""");
        var s = Repo().Load();
        Assert.Equal(ThemeMode.System, s.Theme);
        Assert.Equal(ColorPalette.Saturn, s.Palette);
        Assert.Equal(EditorMode.Builtin, s.EditorMode);
    }

    [Fact]
    public void 저장_키와_값은_모든_앱_공통_규약을_따른다()
    {
        Repo().Save(Full);
        var j = Saved();
        Assert.Equal("dark", j.GetProperty("theme_mode").GetString());
        Assert.Equal("rose", j.GetProperty("accent_palette").GetString());
        Assert.Equal(1.15, j.GetProperty("text_scale").GetDouble());
        Assert.Equal("en", j.GetProperty("app_locale").GetString());
        var w = j.GetProperty("window_placement");
        Assert.Equal(10, w.GetProperty("x").GetDouble());
        Assert.Equal(20, w.GetProperty("y").GetDouble());
        Assert.Equal(1000, w.GetProperty("w").GetDouble());
        Assert.Equal(600, w.GetProperty("h").GetDouble());
        Assert.Equal(1.5, w.GetProperty("scale").GetDouble());
        Assert.True(w.GetProperty("max").GetBoolean());
        Assert.Equal(2, j.GetProperty("pane_bookmarks").GetArrayLength());
        Assert.Equal("C:\\left", j.GetProperty("pane_last_left_path").GetString());
    }

    [Fact]
    public void highContrast는_camelCase_그대로_저장된다()
    {
        Repo().Save(new AppSettings { Palette = ColorPalette.HighContrast });
        Assert.Equal("highContrast", Saved().GetProperty("accent_palette").GetString());
        Assert.Equal(ColorPalette.HighContrast, Repo().Load().Palette);
    }

    [Fact]
    public void 모르는_글자_배율은_보통으로_읽는다()
    {
        WriteRaw("""{ "text_scale": 3.5 }""");
        Assert.Equal(1.0, Repo().Load().TextScale);
    }

    [Fact]
    public void 손상된_창_위치_하나가_설정_전체를_못_읽게_만들지_않는다()
    {
        WriteRaw("""{ "theme_mode": "light", "window_placement": { "x": "abc" } }""");
        // 숫자가 아닌 값은 읽기에 실패하지만, 설정은 기본값으로라도 시작해야 한다 (깨진 파일 처리).
        var s = Repo().Load();
        Assert.Equal(ThemeMode.System, s.Theme);
        Assert.True(File.Exists(File_ + ".broken"));
    }

    [Fact]
    public void 필드가_빠진_창_위치는_읽히지만_쓸_수_없는_값이다()
    {
        WriteRaw("""{ "window_placement": { "x": 1 } }""");
        var w = Repo().Load().Window;
        Assert.NotNull(w);
        Assert.False(w!.IsValid);
    }

    [Fact]
    public void 언어는_app_locale_키에_ko_또는_en으로_저장되고_없으면_시스템이다()
    {
        Repo().Save(new AppSettings { Locale = AppLocale.Korean });
        Assert.Equal("ko", Saved().GetProperty("app_locale").GetString());
        Assert.Equal(AppLocale.Korean, Repo().Load().Locale);

        Repo().Save(new AppSettings());
        Assert.Equal(JsonValueKind.Null, Saved().GetProperty("app_locale").ValueKind);
        Assert.Null(Repo().Load().Locale);
    }

    [Fact]
    public void 모르는_언어_값은_시스템으로_읽는다()
    {
        WriteRaw("""{ "app_locale": "fr" }""");
        Assert.Null(Repo().Load().Locale);
    }

    [Fact]
    public void 글꼴과_편집기_설정은_규약의_앱_고유_키_형식으로_저장된다()
    {
        Repo().Save(Full);
        var j = Saved();
        Assert.Equal("Malgun Gothic", j.GetProperty("ui_font_family").GetString());
        Assert.Equal("external", j.GetProperty("editor_mode").GetString());
        Assert.Equal("C:\\Tools\\ed.exe", j.GetProperty("editor_executable").GetString());
    }

    [Fact]
    public void 글꼴과_편집기를_정하지_않은_설정은_기본값이다()
    {
        WriteRaw("{}");
        var s = Repo().Load();
        Assert.Null(s.UiFontFamily);
        Assert.Equal(EditorMode.Builtin, s.EditorMode);
        Assert.Null(s.EditorExecutable);
    }

    [Fact]
    public void 덮어쓰기()
    {
        Repo().Save(new AppSettings { Theme = ThemeMode.Dark });
        Repo().Save(new AppSettings { Theme = ThemeMode.Light });
        Assert.Equal(ThemeMode.Light, Repo().Load().Theme);
    }

    [Fact]
    public void 설정_파일_위치는_앱_이름_폴더_아래의_settings_json이다()
    {
        var file = JsonSettingsRepository.DefaultFile("DaylightCommander");
        Assert.Equal("settings.json", Path.GetFileName(file));
        Assert.Equal("DaylightCommander", Path.GetFileName(Path.GetDirectoryName(file)));
    }
}
