using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.Localization;

/// <summary>한국어 문구. 기준 언어다. 문체는 간결한 명사형이나 해요체 (localization.md §7).</summary>
public sealed class KoreanStrings : IAppStrings
{
    public static readonly KoreanStrings Instance = new();

    public string CommonCancel => "취소";
    public string CommonConfirm => "확인";

    public string AppBarNewWindow => "새 창";
    public string SettingsTooltip => "설정";

    public string SettingsWindowTitle(string appName) => $"{appName} 설정";
    public string SettingsSectionAppearance => "외형";
    public string SettingsMode => "모드";
    public string SettingsModeSystem => "시스템";
    public string ThemeLight => "라이트";
    public string ThemeDark => "다크";
    public string SettingsPalette => "색 구성";
    public string PaletteSaturn => "새턴";
    public string PaletteTeal => "청록";
    public string PaletteAmber => "호박";
    public string PaletteRose => "장미";
    public string PaletteSlate => "슬레이트";
    public string PaletteHighContrast => "고대비";
    public string SettingsFont => "글꼴";
    public string FontDefaultOption(string name) => $"{name} (기본)";
    public string FontSearchHint => "글꼴 검색";
    public string SettingsFontSize => "글자 크기";
    public string FontSizeSmall => "작게";
    public string FontSizeNormal => "보통";
    public string FontSizeLarge => "크게";
    public string SettingsLanguage => "언어";
    public string LanguageSystemShort => "시스템";

    public string SettingsSectionEditor => "편집기";
    public string EditorTitle => "텍스트 편집기 (F4)";
    public string EditorBuiltin => "내장 에디터 (기본)";
    public string EditorExternal => "외부 프로그램";
    public string EditorNotReady => "내장 에디터는 아직 준비 중이에요. 외부 프로그램을 고르면 F4로 열 수 있어요.";
    public string EditorExternalHint => "F4를 누르면 커서가 있는 파일을 이 프로그램으로 열어요.";
    public string EditorExecutableNone => "고른 프로그램이 없어요";
    public string EditorChoose => "찾아보기…";
    public string EditorClear => "지우기";
    public string EditorChooseTitle => "편집기 실행 파일 선택";

    public string PaneLoading => "불러오는 중…";
    public string PaneEmpty => "비어 있음";
    public string PaneNoMatches => "일치하는 항목 없음";
    public string PaneOpenError(string error) => $"열 수 없어요: {error} (눌러서 닫기)";
    public string PaneItemCount(int count) => $"{count}개 항목";
    public string PaneSelectedCount(int count) => $"선택 {count}개";
    public string PaneFilter(string query) => $"필터: {query}";
    public string PaneBackTooltip => "뒤로";
    public string PaneForwardTooltip => "앞으로";
    public string PaneUpTooltip => "상위 폴더";
    public string PaneRefreshTooltip => "새로고침";
    public string PaneShowHiddenTooltip => "숨김 파일 표시";
    public string DriveTooltip => "드라이브";
    public string ColumnName => "이름";
    public string ColumnSize => "크기";
    public string ColumnModified => "수정일";

    public string BookmarkTooltip => "즐겨찾기";
    public string BookmarkNone => "즐겨찾기가 없어요";
    public string BookmarkRemoveTooltip => "즐겨찾기 삭제";
    public string BookmarkAddCurrent => "현재 폴더를 즐겨찾기에 추가";
    public string BookmarkRemoveCurrent => "현재 폴더 즐겨찾기 해제";
    public string RecentTooltip => "최근 방문";
    public string RecentNone => "최근 방문한 폴더가 없어요";
    public string RecentClear => "최근 방문 지우기";

    public string FnRename => "F2 이름변경";
    public string FnView => "F3 보기";
    public string FnEdit => "F4 편집";
    public string FnCopy => "F5 복사";
    public string FnMove => "F6 이동";
    public string FnNewFolder => "F7 새 폴더";
    public string FnDelete => "F8 삭제";
    public string FnNewFile => "새 파일 (Ctrl+N)";
    public string FnCompress => "압축";
    public string FnExtract => "압축풀기";
    public string FnProperties => "속성";
    public string FnPatternSelect => "패턴선택";

    public string DialogRenameTitle => "이름 변경";
    public string DialogNewFolderTitle => "새 폴더";
    public string DialogNewFileTitle => "새 파일";
    public string TransferCopyTitle => "복사";
    public string TransferMoveTitle => "이동";
    public string TransferPrompt(FileOperationKind kind, string what) =>
        kind == FileOperationKind.Copy ? $"{what} 복사할 폴더" : $"{what} 이동할 폴더";
    public string DeleteTrashTitle => "휴지통으로 이동";
    public string DeletePermanentTitle => "영구 삭제";
    public string DeleteTrashPrompt(string what) => $"{what} 휴지통으로 이동할까요?";
    public string DeletePermanentPrompt(string what) => $"{what} 영구 삭제할까요? 되돌릴 수 없어요.";
    public string DeleteMore(int count) => $"… 외 {count}개";
    public string DeleteTrashAction => "휴지통으로";
    public string DeletePermanentAction => "삭제";

    public string ItemsWhat(int count, string firstName) => count == 1 ? $"'{firstName}'" : $"{count}개 항목";

    public string ConflictTitle => "같은 이름이 있어요";
    public string ConflictExisting => "기존";
    public string ConflictNew => "새것";
    public string ConflictApplyAll => "남은 항목에도 모두 적용";
    public string ConflictOverwrite => "덮어쓰기";
    public string ConflictKeepBoth => "둘 다 유지";
    public string ConflictSkip => "건너뛰기";
    public string ConflictFolderInfo(string modified) => $"폴더 · {modified}";
    public string ConflictInfoUnavailable => "정보를 읽을 수 없어요";

    public string JobsClearFinished => "완료 항목 지우기";
    public string JobsCancel => "취소";
    public string JobQueued => "대기 중";
    public string JobRunning => "진행 중";
    public string JobDone => "완료";
    public string JobFailed => "실패";
    public string JobCancelled => "취소됨";

    public string JobTitle(JobLabel label)
    {
        var what = ItemsWhat(label.Count, label.FirstName);
        return label.Kind switch
        {
            FileOperationKind.Copy => $"복사 · {what} → {label.Target}",
            FileOperationKind.Move => $"이동 · {what} → {label.Target}",
            _ => (label.ToTrash ? "휴지통 · " : "삭제 · ") + what,
        };
    }

    public string JobFailureSummary(IReadOnlyList<OperationFailure> failures, FileError? error)
    {
        if (failures.Count == 0) return error is null ? "" : FileErrorText(error);
        var first = failures[0];
        var name = Path.GetFileName(first.Path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : first.Path;
        var more = failures.Count > 1 ? $" 외 {failures.Count - 1}건" : "";
        return $"{failures.Count}개 실패: {name} — {FileErrorText(first.Error)}{more}";
    }

    public string FileErrorText(FileError error) => error switch
    {
        FileError.NameEmpty => "이름을 입력해 주세요",
        FileError.NameReserved e => $"사용할 수 없는 이름이에요: {e.Name}",
        FileError.NameInvalidChar e => $"이름에 쓸 수 없는 문자가 있어요: {e.Char}",
        FileError.IntoItself e => $"폴더를 자기 자신 안으로 복사하거나 이동할 수 없어요: {e.Name}",
        FileError.DifferentTypes e => $"파일과 폴더는 서로 덮어쓸 수 없어요: {e.Name}",
        FileError.RootNotAllowed => "루트는 복사하거나 이동할 수 없어요",
        FileError.AlreadyExists e => $"이미 있는 이름이에요: {e.Name}",
        FileError.NotFound e => $"찾을 수 없어요: {e.Name}",
        FileError.AccessDenied e => $"권한이 없거나 다른 곳에서 쓰고 있어요: {e.Name}",
        FileError.TrashFailed e => $"휴지통으로 옮기지 못했어요: {e.Name}" + (e.Detail is null ? "" : $" ({e.Detail})"),
        FileError.TargetFolderMissing => "대상 폴더가 없어요. 경로를 확인해 주세요",
        FileError.EditorNotFound e => $"편집기를 찾을 수 없어요: {e.Path}. 설정에서 다시 골라 주세요",
        FileError.EditorLaunchFailed e => "편집기를 실행하지 못했어요" + (e.Detail is null ? "" : $" ({e.Detail})"),
        FileError.Other e => e.Detail ?? "알 수 없는 오류예요",
        _ => "알 수 없는 오류예요",
    };
}
