using System.Globalization;
using Avalonia;
using DaylightCommander.App.Localization;
using DaylightCommander.Domain.Settings;

namespace DaylightCommander.App;

/// <summary>
/// 지금 쓰는 언어의 문구. 코드는 <see cref="Current"/>로 읽고, XAML은 <c>{DynamicResource Str.PaneBackTooltip}</c>처럼 읽는다.
/// <see cref="Apply"/>가 언어를 바꾸면 리소스를 새로 채우므로 열려 있는 모든 창의 XAML 문구가 바로 바뀌고,
/// 코드가 만든 문구(상태줄, 작업 목록 등)는 <see cref="Changed"/>를 받아 다시 만든다.
/// </summary>
static class Strings
{
    public static IAppStrings Current { get; private set; } = KoreanStrings.Instance;

    /// <summary>언어가 바뀌었다. 코드가 만들어 둔 문구를 다시 만들어야 한다.</summary>
    public static event Action? Changed;

    /// <summary>설정의 언어(없으면 시스템)를 풀어 적용한다. 시스템은 OS가 한국어일 때만 한국어, 그 밖에는 영어.</summary>
    public static void Apply(AppLocale? setting)
    {
        var locale = DaylightCommander.Domain.Settings.Localization.Resolve(setting, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Current = locale == AppLocale.Korean ? KoreanStrings.Instance : EnglishStrings.Instance;
        PublishResources();
        Changed?.Invoke();
    }

    /// <summary>XAML이 읽는 문구를 앱 리소스에 올린다 (<c>Str.&lt;이름&gt;</c>).</summary>
    static void PublishResources()
    {
        if (Application.Current is not { } app) return;
        var s = Current;
        void Set(string key, string value) => app.Resources["Str." + key] = value;

        Set("CommonCancel", s.CommonCancel);
        Set("CommonConfirm", s.CommonConfirm);
        Set("AppBarNewWindow", s.AppBarNewWindow);
        Set("SettingsTooltip", s.SettingsTooltip);
        Set("TreeTooltip", s.TreeTooltip);
        Set("CompareOnTooltip", s.CompareOnTooltip);
        Set("CompareOffTooltip", s.CompareOffTooltip);
        Set("SyncBidirectional", s.SyncBidirectional);
        Set("SwapPanesTooltip", s.SwapPanesTooltip);
        Set("TerminalTooltip", s.TerminalTooltip);
        Set("SyncPathRightTooltip", s.SyncPathRightTooltip);
        Set("SyncPathLeftTooltip", s.SyncPathLeftTooltip);

        Set("SettingsSectionAppearance", s.SettingsSectionAppearance);
        Set("SettingsMode", s.SettingsMode);
        Set("SettingsModeSystem", s.SettingsModeSystem);
        Set("ThemeLight", s.ThemeLight);
        Set("ThemeDark", s.ThemeDark);
        Set("SettingsPalette", s.SettingsPalette);
        Set("PaletteSaturn", s.PaletteSaturn);
        Set("PaletteTeal", s.PaletteTeal);
        Set("PaletteAmber", s.PaletteAmber);
        Set("PaletteRose", s.PaletteRose);
        Set("PaletteSlate", s.PaletteSlate);
        Set("PaletteHighContrast", s.PaletteHighContrast);
        Set("SettingsFont", s.SettingsFont);
        Set("FontSearchHint", s.FontSearchHint);
        Set("SettingsFontSize", s.SettingsFontSize);
        Set("FontSizeSmall", s.FontSizeSmall);
        Set("FontSizeNormal", s.FontSizeNormal);
        Set("FontSizeLarge", s.FontSizeLarge);
        Set("SettingsLanguage", s.SettingsLanguage);
        Set("LanguageSystemShort", s.LanguageSystemShort);
        Set("SettingsSectionEditor", s.SettingsSectionEditor);
        Set("EditorTitle", s.EditorTitle);
        Set("EditorBuiltin", s.EditorBuiltin);
        Set("EditorExternal", s.EditorExternal);
        Set("EditorNotReady", s.EditorNotReady);
        Set("EditorExternalHint", s.EditorExternalHint);
        Set("EditorChoose", s.EditorChoose);
        Set("EditorClear", s.EditorClear);

        Set("PaneBackTooltip", s.PaneBackTooltip);
        Set("PaneForwardTooltip", s.PaneForwardTooltip);
        Set("PaneUpTooltip", s.PaneUpTooltip);
        Set("PaneRefreshTooltip", s.PaneRefreshTooltip);
        Set("PaneShowHiddenTooltip", s.PaneShowHiddenTooltip);
        Set("DriveTooltip", s.DriveTooltip);
        Set("BookmarkTooltip", s.BookmarkTooltip);
        Set("RecentTooltip", s.RecentTooltip);

        Set("FnRename", s.FnRename);
        Set("FnView", s.FnView);
        Set("FnEdit", s.FnEdit);
        Set("FnCopy", s.FnCopy);
        Set("FnMove", s.FnMove);
        Set("FnNewFolder", s.FnNewFolder);
        Set("FnDelete", s.FnDelete);
        Set("FnNewFile", s.FnNewFile);
        Set("FnCompress", s.FnCompress);
        Set("FnExtract", s.FnExtract);
        Set("FnProperties", s.FnProperties);
        Set("FnPatternSelect", s.FnPatternSelect);

        Set("JobsClearFinished", s.JobsClearFinished);
        Set("JobsCancel", s.JobsCancel);
    }
}
