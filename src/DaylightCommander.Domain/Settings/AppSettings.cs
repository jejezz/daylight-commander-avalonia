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

/// <summary>패널 보기 방식: 목록(상세) · 작은 미리보기 · 큰 미리보기. 저장 값은 <c>list</c> / <c>small</c> / <c>large</c>.</summary>
public enum PaneViewMode { List, SmallThumbs, LargeThumbs }

public static class PaneViewModes
{
    /// <summary>격자 타일의 한 변(아이콘·썸네일 영역, DIP). 목록은 0.</summary>
    public static int IconSize(this PaneViewMode mode) => mode switch
    {
        PaneViewMode.SmallThumbs => 48,
        PaneViewMode.LargeThumbs => 112,
        _ => 0,
    };

    /// <summary>타일 한 칸의 폭·높이 (아이콘 아래에 이름 두 줄이 들어간다). 목록은 0.</summary>
    public static int TileWidth(this PaneViewMode mode) => mode switch
    {
        PaneViewMode.SmallThumbs => 88,
        PaneViewMode.LargeThumbs => 140,
        _ => 0,
    };

    public static int TileHeight(this PaneViewMode mode) => mode.IsGrid() ? mode.IconSize() + 56 : 0;

    /// <summary>폭 <paramref name="width"/> 안에 들어가는 타일 열 수 (최소 1). 목록은 1.</summary>
    public static int Columns(this PaneViewMode mode, double width) =>
        mode.IsGrid() && double.IsFinite(width) ? Math.Max(1, (int)(width / mode.TileWidth())) : 1;

    public static bool IsGrid(this PaneViewMode mode) => mode != PaneViewMode.List;

    /// <summary>Ctrl+휠 한 칸: 위로 돌리면 더 크게, 아래로 돌리면 더 작게 (목록 ↔ 작은 ↔ 큰, 끝에서 멈춘다).</summary>
    public static PaneViewMode Step(this PaneViewMode mode, int direction) =>
        (PaneViewMode)Math.Clamp((int)mode + Math.Sign(direction), (int)PaneViewMode.List, (int)PaneViewMode.LargeThumbs);
}

/// <summary>화면 글자 크기 배율의 범위·간격 (슬라이더 80%~160%, 5% 단위). 저장은 배율 숫자(<c>text_scale</c>)로 한다.</summary>
public static class TextScales
{
    public const double Min = 0.8, Max = 1.6, Step = 0.05, Default = 1.0;

    /// <summary>범위 안으로, 5% 단위로 맞춘다. 숫자가 아니면 기본(100%).</summary>
    public static double Snap(double value) =>
        !double.IsFinite(value) ? Default : Math.Round(Math.Clamp(Math.Round(value / Step) * Step, Min, Max), 2);
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
    public double TextScale { get; init; } = TextScales.Default;

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
    /// <summary>1패널 모드: 활성 패널만 펼치고 다른 패널은 가장자리에 접어 둔다.</summary>
    /// <summary>자주 쓰는 네트워크 서버 (연결 대화상자에서 한 번에 고른다).</summary>
    [JsonPropertyName("network_profiles")]
    public IReadOnlyList<NetworkProfile> NetworkProfiles { get; init; } = [];

    /// <summary>접속한 적 있는 SSH 서버의 호스트 키 지문 (<c>host:port</c> → 지문). 다음 접속 때 같은 서버인지 확인한다.</summary>
    [JsonPropertyName("known_host_keys")]
    public IReadOnlyDictionary<string, string> KnownHostKeys { get; init; } = new Dictionary<string, string>();

    /// <summary>패널별 보기 방식. 파일에 없거나 모르는 값이면 목록(첫 항목)으로 읽힌다.</summary>
    [JsonPropertyName("pane_left_view")]
    [JsonConverter(typeof(PaneViewModeConverter))]
    public PaneViewMode LeftView { get; init; } = PaneViewMode.List;

    [JsonPropertyName("pane_right_view")]
    [JsonConverter(typeof(PaneViewModeConverter))]
    public PaneViewMode RightView { get; init; } = PaneViewMode.List;

    [JsonPropertyName("pane_single_mode")]
    public bool SinglePane { get; init; }

    [JsonPropertyName("pane_swapped")]
    public bool PanesSwapped { get; init; }

    [JsonPropertyName("window_placement")]
    public WindowPlacement? Window { get; init; }

    /// <summary>F4가 실제로 열 외부 편집기. 외부 모드이고 실행 파일이 지정돼 있을 때만 값이 있다.</summary>
    [JsonIgnore]
    public string? ExternalEditor =>
        EditorMode == EditorMode.External && !string.IsNullOrWhiteSpace(EditorExecutable) ? EditorExecutable : null;

    public AppSettings WithTextScale(double scale) => this with { TextScale = TextScales.Snap(scale) };

    /// <summary>
    /// 파일에서 읽은 값을 쓸 수 있는 값으로 고친다 (배율·폭은 범위 안으로).
    /// 또 하나: JSON 소스 생성기는 파일에 없는 키의 <c>init</c> 속성을 초기값이 아니라 <c>null</c>/0으로 두므로, 이전 버전이 쓴 파일을
    /// 읽으면 나중에 생긴 목록 속성이 null이 된다. 여기서 초기값으로 채운다.
    /// </summary>
    public AppSettings Normalized()
    {
        var snapped = TextScales.Snap(TextScale);
        var fixedScale = snapped == TextScale ? this : this with { TextScale = snapped };
        var width = !double.IsFinite(TreeDockWidth) || TreeDockWidth <= 0 ? DefaultTreeDockWidth : Math.Clamp(TreeDockWidth, MinTreeDockWidth, MaxTreeDockWidth);
        var result = width == fixedScale.TreeDockWidth ? fixedScale : fixedScale with { TreeDockWidth = width };
        // 파일에 없던 목록·표는 null로 읽힌다 (컴파일러는 null이 아니라고 믿으므로 object로 본다).
        if ((object?)result.Bookmarks is null) result = result with { Bookmarks = [] };
        if ((object?)result.RecentPaths is null) result = result with { RecentPaths = [] };
        if ((object?)result.NetworkProfiles is null) result = result with { NetworkProfiles = [] };
        if ((object?)result.KnownHostKeys is null) result = result with { KnownHostKeys = new Dictionary<string, string>() };
        return result;
    }

    public AppSettings WithVisit(string path) =>
        this with { RecentPaths = new[] { path }.Concat(RecentPaths.Where(p => p != path)).Take(MaxRecent).ToList() };

    /// <summary>서버를 저장한다. 같은 서버(스킴·호스트·포트·계정)가 이미 있으면 그 자리를 바꾸고, 없으면 맨 끝에 더한다.</summary>
    public AppSettings WithNetworkProfile(NetworkProfile profile)
    {
        static bool Same(NetworkProfile a, NetworkProfile b) =>
            a.Scheme == b.Scheme && a.Port == b.Port && a.User == b.User && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase);
        return NetworkProfiles.Any(p => Same(p, profile))
            ? this with { NetworkProfiles = NetworkProfiles.Select(p => Same(p, profile) ? profile : p).ToList() }
            : this with { NetworkProfiles = [.. NetworkProfiles, profile] };
    }

    public AppSettings WithoutNetworkProfile(NetworkProfile profile) =>
        this with { NetworkProfiles = NetworkProfiles.Where(p => p != profile).ToList() };

    public AppSettings WithBookmarkToggled(string path) =>
        Bookmarks.Contains(path)
            ? this with { Bookmarks = Bookmarks.Where(p => p != path).ToList() }
            : this with { Bookmarks = [.. Bookmarks, path] };
}

/// <summary>저장해 둔 네트워크 서버. 비밀번호는 저장하지 않는다 (연결할 때마다 묻는다).</summary>
public sealed record NetworkProfile
{
    [JsonPropertyName("scheme")]
    public string Scheme { get; init; } = "sftp";

    [JsonPropertyName("host")]
    public string Host { get; init; } = "";

    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("user")]
    public string User { get; init; } = "";

    /// <summary>시작 폴더 (WebDAV처럼 서버 주소 뒤에 <c>/dav/</c> 같은 경로가 붙는 곳). 옛 파일에는 없으므로 null일 수 있다.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }
}

/// <summary>설정을 읽고 쓴다. 구현은 data 계층.</summary>
public interface ISettingsRepository
{
    /// <summary>파일이 없거나 읽을 수 없으면 기본값. 예외를 던지지 않는다.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
