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
    public string TreeTooltip => "디렉토리 트리 (Ctrl+T)";

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
    public string SettingsFontSize => "화면 글자 크기";
    public string SettingsFontSizeHint => "메뉴, 탭, 폴더 이름, 상태 줄 등 모든 글자의 크기입니다.";
    public string SettingsLanguage => "언어";
    public string LanguageSystemShort => "시스템";

    public string SettingsSectionEditor => "편집기";
    public string EditorTitle => "텍스트 편집기 (F4)";
    public string EditorBuiltin => "내장 에디터 (기본)";
    public string EditorExternal => "외부 프로그램";
    public string EditorNotReady => "F4를 누르면 내장 에디터로 열어요.";
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
    public string PaneViewTooltip => "보기 방식";
    public string PaneViewList => "목록";
    public string PaneViewSmallThumbs => "작은 미리보기";
    public string PaneViewLargeThumbs => "큰 미리보기";
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

    public string ViewerNormalView => "일반 보기";
    public string ViewerHexView => "Hex로 보기";
    public string ViewerUnsupported => "미리보기를 지원하지 않는 파일 형식이에요.";
    public string ViewerOpenWithDefaultApp => "기본 앱으로 열기";
    public string ViewerLoadError(string error) => $"파일을 읽을 수 없어요: {error}";
    public string ViewerImageLoadError(string error) => $"이미지를 열 수 없어요: {error}";
    public string ViewerHexTruncatedNote(int kb) => $"처음 {kb}KB만 표시해요.";
    public string ViewerTextTruncatedNote(int kb) => $"큰 파일이라 처음 {kb}KB만 표시해요.";

    public string EditorSave => "저장";
    public string EditorSaveAs => "다른 이름으로 저장";
    public string EditorDontSave => "저장 안 함";
    public string EditorUnsavedTitle => "저장하지 않은 변경";
    public string EditorUnsavedBody(string name) => $"{name}의 변경 내용을 저장할까요?";
    public string EditorExternalChangedTitle => "파일이 바뀌었어요";
    public string EditorExternalChangedBody => "열어 둔 뒤에 디스크의 파일이 바뀌었어요. 덮어쓸까요?";
    public string EditorOverwrite => "덮어쓰기";
    public string EditorSaveFailed(string error) => $"저장하지 못했어요: {error}";
    public string EditorUploadFailed(string error, string tempPath) => $"서버에 올리지 못했어요: {error}\n고친 내용은 이 임시 파일에 남아 있어요:\n{tempPath}";
    public string EditorRemoteChangedBody => "열어 둔 뒤에 서버의 파일이 바뀌었어요. 덮어쓸까요?";
    public string EditorLoadFailed(string error) => $"파일을 열지 못했어요: {error}";
    public string EditorFind => "찾기";
    public string EditorFindHint => "찾을 내용";
    public string EditorReplaceHint => "바꿀 내용";
    public string EditorReplace => "바꾸기";
    public string EditorReplaceAll => "모두 바꾸기";
    public string EditorMatchCase => "대소문자 구분";
    public string EditorRegex => "정규식";
    public string EditorPrevMatch => "이전";
    public string EditorNextMatch => "다음";
    public string EditorToggleReplace => "바꾸기 전환";
    public string EditorNoMatches => "결과 없음";
    public string EditorMatchCount(int current, int total) => $"{current}/{total}";
    public string EditorWordWrap => "줄 바꿈";
    public string EditorGoToLine => "줄 이동";
    public string EditorGoToLineHint(int max) => $"줄 번호 (1–{max})";
    public string EditorStatusPosition(int line, int column) => $"{line}행 {column}열";
    public string EditorStatusSelected(int count) => $"{count}자 선택됨";
    public string EditorHighlightOff => "구문 강조 꺼짐 (파일이 큼)";
    public string EditorSaveAsHint => "저장할 경로 (이름만 쓰면 같은 폴더)";
    public string EditorOverwriteExistingBody(string name) => $"{name}이(가) 이미 있어요. 덮어쓸까요?";
    public string EditorReadOnlyBinary => "바이너리이거나 UTF-8이 아닌 파일이에요. 바이트를 글자 하나씩 그대로 보여 주는 읽기 전용이에요.";
    public string EditorUndo => "실행 취소";
    public string EditorRedo => "다시 실행";
    public string EditorMenuView => "보기";
    public string EditorCut => "잘라내기";
    public string EditorCopy => "복사";
    public string EditorPaste => "붙여넣기";
    public string EditorPreviewTooltip => "마크다운 미리보기";
    public string EditorGroupFile => "파일";
    public string EditorGroupEdit => "편집";
    public string EditorGroupFind => "찾기";
    public string EditorScClose => "닫기";
    public string EditorScSelectAll => "모두 선택";
    public string EditorScDeleteLine => "줄 지우기";

    public string AboutTooltip => "정보";
    public string AboutTagline => "Total Commander · Midnight Commander 스타일의 무료 2단 패널 데스크톱 파일 매니저";
    public string AboutDescription => "키보드 중심의 빠른 조작과 내장 뷰어·에디터, 여러 창을 나란히 쓸 수 있는 클래식 파일 매니저를 목표로 만들고 있어요.";
    public string AboutVersion(string version, int build) => $"버전 {version} (빌드 {build})";
    public string AboutOpenSourceLicenses => "오픈소스 라이선스";
    public string AboutRepository => "GitHub";
    public string AboutClose => "닫기";
    public string LicensesTitle => "오픈소스 라이선스";
    public string LicensesProject => "이 프로젝트";
    public string LicensesNote => "각 항목의 전문과 저작권 고지는 해당 프로젝트 저장소에서 볼 수 있어요.";
    public string PropTitle(string name) => $"{name} 속성";
    public string PropMultiName(int count) => $"{count}개 항목";
    public string PropName => "이름";
    public string PropLocation => "위치";
    public string PropKind => "종류";
    public string PropKindFolder => "폴더";
    public string PropKindFile => "파일";
    public string PropKindFileExt(string ext) => $"{ext} 파일";
    public string PropKindMixed => "여러 종류";
    public string PropLinkTarget => "연결 대상";
    public string PropSize => "크기";
    public string PropSizeValue(string formatted, long bytes) => $"{formatted} ({bytes:N0}바이트)";
    public string PropContains => "포함";
    public string PropContainsValue(int files, int folders) => $"파일 {files:N0}개, 폴더 {folders:N0}개";
    public string PropCreated => "만든 날짜";
    public string PropModified => "수정한 날짜";
    public string PropAccessed => "액세스한 날짜";
    public string PropAttributes => "특성";
    public string PropReadOnly => "읽기 전용";
    public string PropHidden => "숨김";
    public string PropSystem => "시스템";
    public string PropCalculating => "계산 중…";
    public string PropIncomplete => "일부 항목은 읽지 못해 빠졌어요";

    public string ContextOpen => "열기";
    public string ContextOpenWithDefault => "기본 앱으로 열기";
    public string ContextView => "보기 (F3)";
    public string ContextEdit => "편집 (F4)";
    public string ContextRename => "이름 변경 (F2)";
    public string ContextCopy => "복사 → 반대 패널 (F5)";
    public string ContextDuplicate => "여기에 복제 (Ctrl+D)";
    public string ContextMove => "이동 → 반대 패널 (F6)";
    public string ContextDelete => "삭제 (F8)";
    public string ContextProperties => "속성 (Alt+Enter)";
    public string ContextBookmark => "즐겨찾기에 추가";
    public string TreeMenuCut => "잘라내기";
    public string TreeMenuCopy => "복사";
    public string TreeMenuPaste => "여기에 붙여넣기";
    public string TreeMenuDelete => "삭제";
    public string CompareOnTooltip => "폴더 비교 (좌우 패널)";
    public string CompareOffTooltip => "폴더 비교 끄기";
    public string SyncBidirectional => "양방향 동기화";
    public string SwapPanesTooltip => "좌우 패널 교환 (Ctrl+U)";
    public string SyncPathRightTooltip => "왼쪽 패널 경로를 오른쪽으로 (Ctrl+Shift+→)";
    public string TerminalTooltip => "터미널 열기 (활성 패널 경로, Ctrl+`)";
    public string PatternSelectTitle => "패턴으로 선택";
    public string PatternHint => "예: *.jpg, IMG_*.png";
    public string PatternDeselect => "선택 해제";
    public string PatternAdd => "선택 추가";
    public string NetworkTooltip => "네트워크 드라이브 연결";
    public string NetworkConnectSftp => "SFTP 서버에 연결…";
    public string NetworkRemoveProfile => "저장한 서버 지우기";
    public string ConnectTitle => "서버에 연결";
    public string NetworkConnectFtp => "FTP 서버에 연결…";
    public string NetworkConnectWebDav => "WebDAV 서버에 연결…";
    public string ConnectPath => "시작 폴더 (선택)";
    public string ConnectPathHint => "예: /remote.php/dav/files/me/";
    public string ConnectProtocol => "프로토콜";
    public string ConnectAnonymous => "익명으로 연결";
    public string ConnectHost => "서버 주소";
    public string ConnectHostHint => "예: example.com";
    public string ConnectPort => "포트";
    public string ConnectUser => "사용자 이름";
    public string ConnectPassword => "비밀번호";
    public string ConnectSave => "이 서버를 저장 (비밀번호는 저장하지 않아요)";
    public string ConnectButton => "연결";
    public string ConnectBusy => "연결 중…";
    public string NetworkDisconnect(string server) => $"연결 해제: {server}";
    public string SyncPathLeftTooltip => "오른쪽 패널 경로를 왼쪽으로 (Ctrl+Shift+←)";
    public string SinglePaneTooltip => "1패널 모드 (Ctrl+\\)";
    public string DualPaneTooltip => "2패널 모드 (Ctrl+\\)";
    public string DockedPaneTooltip(string path) => $"접힌 패널: {path}\n클릭하면 이 패널로 전환, 파일을 놓으면 복사/이동";
    public string SyncUseLeft => "왼쪽 파일 사용";
    public string SyncUseRight => "오른쪽 파일 사용";
    public string SyncDiffToRight(int count) => $"차이 {count}개 → 복사";
    public string SyncDiffToLeft(int count) => $"← 차이 {count}개 복사";
    public string SyncConflictTitle(string name) => $"{name} — 어느 쪽을 사용할까요?";
    public string SyncConflictLeft(string size, string modified) => $"왼쪽: {size} · {modified}";
    public string SyncConflictRight(string size, string modified) => $"오른쪽: {size} · {modified}";
    public string ContextReveal => "파일 관리자에서 보기";

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
        FileError.EditorTooLarge e => $"5MB를 넘는 파일은 편집할 수 없어요. F3 보기로 열어 주세요: {e.Name}",
        FileError.RemoteNotConnected e => $"서버({e.Server})에 연결되어 있지 않아요. 네트워크 메뉴에서 연결해 주세요",
        FileError.RemoteAuthFailed => "사용자 이름이나 비밀번호가 맞지 않아요",
        FileError.RemoteConnectFailed e => "서버에 연결하지 못했어요" + (string.IsNullOrEmpty(e.Detail) ? "" : $" ({e.Detail})"),
        FileError.RemoteHostKeyChanged e => $"서버({e.HostPort})의 보안 키가 전에 접속했을 때와 달라요. 서버가 바뀌었거나 누군가 가로채는 중일 수 있어요",
        FileError.RemoteUnsupported => "네트워크 항목에서는 아직 쓸 수 없는 기능이에요",
        FileError.TerminalNotFound => "터미널 프로그램을 찾지 못했어요",
        FileError.EditorLaunchFailed e => "편집기를 실행하지 못했어요" + (e.Detail is null ? "" : $" ({e.Detail})"),
        FileError.Other e => e.Detail ?? "알 수 없는 오류예요",
        _ => "알 수 없는 오류예요",
    };
}
