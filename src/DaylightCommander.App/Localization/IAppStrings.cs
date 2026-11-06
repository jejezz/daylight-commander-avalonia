using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.Localization;

/// <summary>
/// 화면에 보이는 모든 문구 (conventions localization.md의 ARB에 해당하는 C# 판).
/// <list type="bullet">
/// <item>한국어(<see cref="KoreanStrings"/>)가 기준이고 영어(<see cref="EnglishStrings"/>)는 <b>모든 항목</b>을 가져야 한다.
/// 인터페이스라서 빠뜨리면 컴파일이 실패한다 — Flutter의 "누락 번역은 CI에서 막는다"를 컴파일 단계에서 지킨다.</item>
/// <item>이름은 PascalCase에 화면 이름을 접두어로 붙인다 (<c>PaneEmpty</c>, <c>SettingsMode</c>).</item>
/// <item>값이 들어가는 문구는 메서드다 (<c>PaneItemCount(n)</c>). 복수형·어순은 언어가 정한다.</item>
/// <item>앱 표시 이름은 번역하지 않는다. <c>AppIdentity.DisplayName</c>을 인자로 받는다.</item>
/// <item>한국어 문체는 간결한 명사형이나 해요체, 영어 버튼·메뉴는 Title Case, 문장은 Sentence case (localization.md §7).</item>
/// </list>
/// </summary>
public interface IAppStrings
{
    // 공통
    string CommonCancel { get; }
    string CommonConfirm { get; }

    // 앱 바
    string AppBarNewWindow { get; }
    string SettingsTooltip { get; }
    string TreeTooltip { get; }

    // 설정 창
    string SettingsWindowTitle(string appName);
    string SettingsSectionAppearance { get; }
    string SettingsMode { get; }
    string SettingsModeSystem { get; }
    string ThemeLight { get; }
    string ThemeDark { get; }
    string SettingsPalette { get; }
    string PaletteSaturn { get; }
    string PaletteTeal { get; }
    string PaletteAmber { get; }
    string PaletteRose { get; }
    string PaletteSlate { get; }
    string PaletteHighContrast { get; }
    string SettingsFont { get; }
    string FontDefaultOption(string name);
    string FontSearchHint { get; }
    string SettingsFontSize { get; }
    string FontSizeSmall { get; }
    string FontSizeNormal { get; }
    string FontSizeLarge { get; }
    string SettingsLanguage { get; }
    string LanguageSystemShort { get; }

    // 설정 창: 편집기
    string SettingsSectionEditor { get; }
    string EditorTitle { get; }
    string EditorBuiltin { get; }
    string EditorExternal { get; }
    string EditorNotReady { get; }
    string EditorExternalHint { get; }
    string EditorExecutableNone { get; }
    string EditorChoose { get; }
    string EditorClear { get; }
    string EditorChooseTitle { get; }

    // 패널
    string PaneLoading { get; }
    string PaneEmpty { get; }
    string PaneNoMatches { get; }
    string PaneOpenError(string error);
    string PaneItemCount(int count);
    string PaneSelectedCount(int count);
    string PaneFilter(string query);
    string PaneBackTooltip { get; }
    string PaneForwardTooltip { get; }
    string PaneUpTooltip { get; }
    string PaneRefreshTooltip { get; }
    string PaneShowHiddenTooltip { get; }
    string DriveTooltip { get; }
    string ColumnName { get; }
    string ColumnSize { get; }
    string ColumnModified { get; }

    // 즐겨찾기·최근 방문
    string BookmarkTooltip { get; }
    string BookmarkNone { get; }
    string BookmarkRemoveTooltip { get; }
    string BookmarkAddCurrent { get; }
    string BookmarkRemoveCurrent { get; }
    string RecentTooltip { get; }
    string RecentNone { get; }
    string RecentClear { get; }

    // 기능 키 바
    string FnRename { get; }
    string FnView { get; }
    string FnEdit { get; }
    string FnCopy { get; }
    string FnMove { get; }
    string FnNewFolder { get; }
    string FnDelete { get; }
    string FnNewFile { get; }
    string FnCompress { get; }
    string FnExtract { get; }
    string FnProperties { get; }
    string FnPatternSelect { get; }

    // 대화상자
    string DialogRenameTitle { get; }
    string DialogNewFolderTitle { get; }
    string DialogNewFileTitle { get; }
    string TransferCopyTitle { get; }
    string TransferMoveTitle { get; }
    string TransferPrompt(FileOperationKind kind, string what);
    string DeleteTrashTitle { get; }
    string DeletePermanentTitle { get; }
    string DeleteTrashPrompt(string what);
    string DeletePermanentPrompt(string what);
    string DeleteMore(int count);
    string DeleteTrashAction { get; }
    string DeletePermanentAction { get; }

    /// <summary>"'a.txt'" 또는 "3개 항목". 한 개면 이름, 여러 개면 개수.</summary>
    string ItemsWhat(int count, string firstName);

    // 이름 충돌
    string ConflictTitle { get; }
    string ConflictExisting { get; }
    string ConflictNew { get; }
    string ConflictApplyAll { get; }
    string ConflictOverwrite { get; }
    string ConflictKeepBoth { get; }
    string ConflictSkip { get; }
    string ConflictFolderInfo(string modified);
    string ConflictInfoUnavailable { get; }

    // 작업 패널
    string JobsClearFinished { get; }
    string JobsCancel { get; }
    string JobQueued { get; }
    string JobRunning { get; }
    string JobDone { get; }
    string JobFailed { get; }
    string JobCancelled { get; }
    string JobTitle(JobLabel label);
    string JobFailureSummary(IReadOnlyList<OperationFailure> failures, FileError? error);

    // 뷰어 (F3)
    string ViewerNormalView { get; }
    string ViewerHexView { get; }
    string ViewerUnsupported { get; }
    string ViewerOpenWithDefaultApp { get; }
    string ViewerLoadError(string error);
    string ViewerImageLoadError(string error);
    string ViewerHexTruncatedNote(int kb);
    string ViewerTextTruncatedNote(int kb);

    // 에디터 (F4)
    string EditorSave { get; }
    string EditorSaveAs { get; }
    string EditorDontSave { get; }
    string EditorUnsavedTitle { get; }
    string EditorUnsavedBody(string name);
    string EditorExternalChangedTitle { get; }
    string EditorExternalChangedBody { get; }
    string EditorOverwrite { get; }
    string EditorSaveFailed(string error);
    string EditorLoadFailed(string error);
    string EditorFind { get; }
    string EditorFindHint { get; }
    string EditorReplaceHint { get; }
    string EditorReplace { get; }
    string EditorReplaceAll { get; }
    string EditorMatchCase { get; }
    string EditorRegex { get; }
    string EditorPrevMatch { get; }
    string EditorNextMatch { get; }
    string EditorToggleReplace { get; }
    string EditorNoMatches { get; }
    string EditorMatchCount(int current, int total);
    string EditorWordWrap { get; }
    string EditorGoToLine { get; }
    string EditorGoToLineHint(int max);
    string EditorStatusPosition(int line, int column);
    string EditorStatusSelected(int count);
    string EditorHighlightOff { get; }
    string EditorSaveAsHint { get; }
    string EditorOverwriteExistingBody(string name);
    string EditorReadOnlyBinary { get; }
    string EditorUndo { get; }
    string EditorRedo { get; }
    string EditorMenuView { get; }
    string EditorCut { get; }
    string EditorCopy { get; }
    string EditorPaste { get; }
    string EditorPreviewTooltip { get; }
    string EditorGroupFile { get; }
    string EditorGroupEdit { get; }
    string EditorGroupFind { get; }
    string EditorScClose { get; }
    string EditorScSelectAll { get; }
    string EditorScDeleteLine { get; }

    // 정보 창·속성
    string AboutTooltip { get; }
    string AboutTagline { get; }
    string AboutDescription { get; }
    string AboutVersion(string version, int build);
    string AboutOpenSourceLicenses { get; }
    string AboutRepository { get; }
    string AboutClose { get; }
    string LicensesTitle { get; }
    string LicensesProject { get; }
    string LicensesNote { get; }
    string PropTitle(string name);
    string PropMultiName(int count);
    string PropName { get; }
    string PropLocation { get; }
    string PropKind { get; }
    string PropKindFolder { get; }
    string PropKindFile { get; }
    string PropKindFileExt(string ext);
    string PropKindMixed { get; }
    string PropLinkTarget { get; }
    string PropSize { get; }
    string PropSizeValue(string formatted, long bytes);
    string PropContains { get; }
    string PropContainsValue(int files, int folders);
    string PropCreated { get; }
    string PropModified { get; }
    string PropAccessed { get; }
    string PropAttributes { get; }
    string PropReadOnly { get; }
    string PropHidden { get; }
    string PropSystem { get; }
    string PropCalculating { get; }
    string PropIncomplete { get; }

    // 우클릭 메뉴
    string ContextOpen { get; }
    string ContextOpenWithDefault { get; }
    string ContextView { get; }
    string ContextEdit { get; }
    string ContextRename { get; }
    string ContextCopy { get; }
    string ContextDuplicate { get; }
    string ContextMove { get; }
    string ContextDelete { get; }
    string ContextProperties { get; }
    string ContextBookmark { get; }
    string TreeMenuCut { get; }
    string TreeMenuCopy { get; }
    string TreeMenuPaste { get; }
    string TreeMenuDelete { get; }
    string CompareOnTooltip { get; }
    string CompareOffTooltip { get; }
    string SyncBidirectional { get; }
    string SwapPanesTooltip { get; }
    string SyncPathRightTooltip { get; }
    string SyncPathLeftTooltip { get; }
    string SyncUseLeft { get; }
    string SyncUseRight { get; }
    string SyncDiffToRight(int count);
    string SyncDiffToLeft(int count);
    string SyncConflictTitle(string name);
    string SyncConflictLeft(string size, string modified);
    string SyncConflictRight(string size, string modified);
    string ContextReveal { get; }

    // 오류
    string FileErrorText(FileError error);
}
