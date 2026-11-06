using System.Text.Json.Serialization;
using DaylightCommander.Domain.Theme;
using DaylightCommander.Domain.Window;

namespace DaylightCommander.Domain.Settings;

/// <summary>
/// UI 언어 (conventions localization.md): 한국어가 기준이고 영어를 더한다. 저장 값은 <c>ko</c> / <c>en</c>이고,
/// 설정하지 않으면(null) 시스템 언어를 따른다 — <see cref="Localization.Resolve"/>.
/// </summary>
public enum AppLocale { Korean, English }

public static class Localization
{
    /// <summary>
    /// 실제로 쓸 언어. 설정이 있으면 그대로, 없으면(시스템) <b>OS 언어가 한국어면 한국어, 그 밖의 모든 언어는 영어</b>다
    /// (localization.md §3). <paramref name="osLanguage"/>는 <c>CultureInfo.CurrentUICulture.TwoLetterISOLanguageName</c> 같은 언어 코드.
    /// </summary>
    public static AppLocale Resolve(AppLocale? setting, string osLanguage) =>
        setting ?? (osLanguage.Equals("ko", StringComparison.OrdinalIgnoreCase) ? AppLocale.Korean : AppLocale.English);
}

/// <summary>F4로 여는 텍스트 편집기. <see cref="Builtin"/>이 기본이고, <see cref="External"/>이면 사용자가 고른 실행 파일로 연다.</summary>
public enum EditorMode { Builtin, External }

/// <summary>글자 크기 선택지: 작게 · 보통 · 크게. 저장은 배율 숫자(<c>text_scale</c>)로 한다.</summary>
public enum FontSize { Small, Normal, Large }

public static class FontSizes
{
    public static double Scale(this FontSize size) => size switch
    {
        FontSize.Small => 0.9,
        FontSize.Large => 1.15,
        _ => 1.0,
    };
}

/// <summary>
/// 영속화되는 앱 설정. 저장 키는 모든 앱 공통 규약을 따른다 (conventions localization.md §5):
/// <c>theme_mode</c>, <c>accent_palette</c>, <c>text_scale</c>, <c>app_locale</c>, <c>window_placement</c>.
/// 앱 고유 설정은 <c>&lt;기능&gt;_&lt;이름&gt;</c> 키를 쓴다 (<c>pane_*</c>). 같은 키 이름의 JSON 파일 하나에 저장한다.
/// <para>
/// 모든 필드에 기본값이 있어서 옛 파일에 없는 키가 있어도, 새 버전이 키를 더해도 읽힌다.
/// 기본값은 규약과 같다 (모드 시스템, 색 구성 새턴, 글자 보통). 불변이라 <c>with</c>로 바꾼다.
/// </para>
/// </summary>
public sealed record AppSettings
{
    public const int MaxRecent = 20;

    [JsonPropertyName("theme_mode")]
    [JsonConverter(typeof(ThemeModeConverter))]
    public ThemeMode Theme { get; init; } = ThemeMode.System;

    [JsonPropertyName("accent_palette")]
    [JsonConverter(typeof(ColorPaletteConverter))]
    public ColorPalette Palette { get; init; } = ColorPalette.Saturn;

    [JsonPropertyName("text_scale")]
    public double TextScale { get; init; } = FontSize.Normal.Scale();

    /// <summary>null이면 시스템 언어를 따른다.</summary>
    [JsonPropertyName("app_locale")]
    [JsonConverter(typeof(AppLocaleConverter))]
    public AppLocale? Locale { get; init; }

    /// <summary>UI 글꼴 이름. null이면 기본(SeoulNamsan). 시스템에 설치된 글꼴 이름을 고를 수 있다.</summary>
    [JsonPropertyName("ui_font_family")]
    public string? UiFontFamily { get; init; }

    [JsonPropertyName("editor_mode")]
    [JsonConverter(typeof(EditorModeConverter))]
    public EditorMode EditorMode { get; init; } = EditorMode.Builtin;

    /// <summary><see cref="EditorMode"/>가 <see cref="EditorMode.External"/>일 때 쓸 실행 파일(macOS는 <c>.app</c>도 가능).</summary>
    [JsonPropertyName("editor_executable")]
    public string? EditorExecutable { get; init; }

    [JsonPropertyName("pane_bookmarks")]
    public IReadOnlyList<string> Bookmarks { get; init; } = [];

    /// <summary>가장 최근이 처음. <see cref="MaxRecent"/>개까지.</summary>
    [JsonPropertyName("pane_recent_paths")]
    public IReadOnlyList<string> RecentPaths { get; init; } = [];

    /// <summary>다음 실행 때 복원할 좌우 패널 경로.</summary>
    [JsonPropertyName("pane_last_left_path")]
    public string? LastLeftPath { get; init; }

    [JsonPropertyName("pane_last_right_path")]
    public string? LastRightPath { get; init; }

    /// <summary>왼쪽의 디렉터리 트리를 열어 두었는가와 그 폭.</summary>
    [JsonPropertyName("tree_dock_open")]
    public bool TreeDockOpen { get; init; }

    [JsonPropertyName("tree_dock_width")]
    public double TreeDockWidth { get; init; } = DefaultTreeDockWidth;

    public const double DefaultTreeDockWidth = 220, MinTreeDockWidth = 160, MaxTreeDockWidth = 420;

    /// <summary>좌우 패널의 화면 위치를 맞바꿨는가 (패널의 경로·선택은 그대로).</summary>
    [JsonPropertyName("pane_swapped")]
    public bool PanesSwapped { get; init; }

    [JsonPropertyName("window_placement")]
    public WindowPlacement? Window { get; init; }

    /// <summary>F4가 실제로 열 외부 편집기. 외부 모드이고 실행 파일이 지정돼 있을 때만 값이 있다.</summary>
    [JsonIgnore]
    public string? ExternalEditor =>
        EditorMode == EditorMode.External && !string.IsNullOrWhiteSpace(EditorExecutable) ? EditorExecutable : null;

    /// <summary><see cref="TextScale"/>에 해당하는 선택지. 목록에 없는 값이면 보통.</summary>
    [JsonIgnore]
    public FontSize FontSize =>
        Enum.GetValues<FontSize>().FirstOrDefault(s => s.Scale() == TextScale, FontSize.Normal);

    public AppSettings WithFontSize(FontSize size) => this with { TextScale = size.Scale() };

    /// <summary>파일에서 읽은 값을 쓸 수 있는 값으로 고친다 (모르는 배율은 보통으로).</summary>
    public AppSettings Normalized()
    {
        var fixedScale = Enum.GetValues<FontSize>().Any(s => s.Scale() == TextScale) ? this : this with { TextScale = FontSize.Normal.Scale() };
        var width = double.IsFinite(TreeDockWidth) ? Math.Clamp(TreeDockWidth, MinTreeDockWidth, MaxTreeDockWidth) : DefaultTreeDockWidth;
        return width == fixedScale.TreeDockWidth ? fixedScale : fixedScale with { TreeDockWidth = width };
    }

    public AppSettings WithVisit(string path) =>
        this with { RecentPaths = new[] { path }.Concat(RecentPaths.Where(p => p != path)).Take(MaxRecent).ToList() };

    public AppSettings WithBookmarkToggled(string path) =>
        Bookmarks.Contains(path)
            ? this with { Bookmarks = Bookmarks.Where(p => p != path).ToList() }
            : this with { Bookmarks = [.. Bookmarks, path] };
}

/// <summary>설정을 읽고 쓴다. 구현은 data 계층.</summary>
public interface ISettingsRepository
{
    /// <summary>파일이 없거나 읽을 수 없으면 기본값. 예외를 던지지 않는다.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
