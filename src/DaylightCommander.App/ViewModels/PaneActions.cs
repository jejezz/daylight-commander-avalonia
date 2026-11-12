using DaylightCommander.Domain.Settings;
using DaylightCommander.Services;

namespace DaylightCommander.App.ViewModels;

/// <summary>
/// 패널 화면(우클릭 메뉴, 끌어다 놓기)이 부르는 동작 모음. HomeViewModel이 패널마다 하나씩 만들어 주고, 각 동작은 먼저 그 패널을
/// 활성 패널로 만든 뒤 실행한다 — 뷰는 홈 상태를 직접 알 필요가 없다.
/// </summary>
public sealed record PaneActions(
    Action<string> OpenWithDefaultApp,
    Action View,
    Action Edit,
    Action Rename,
    Action Copy,
    Action Move,
    Action Delete,
    Action Duplicate,
    Action Properties,
    Action Compress,
    Action Extract,
    Action<string> Reveal,
    Action<string> ToggleBookmark,
    /// <summary>복사(false)·잘라내기(true): 선택(없으면 커서) 항목을 클립보드에 담는다. 첫 인자는 시스템 클립보드를 찾을 화면 요소.</summary>
    Func<Avalonia.Visual, bool, Task> ClipboardCopy,
    /// <summary>붙여넣기: 클립보드의 항목을 이 패널의 현재 폴더에 붙인다.</summary>
    Func<Avalonia.Visual, Task> ClipboardPaste,
    /// <summary>붙여넣을 것이 있는지 (메뉴의 사용 가능 여부).</summary>
    Func<Avalonia.Visual, Task<bool>> CanPaste,
    Action NewFolder,
    /// <summary>끌어다 놓기: (경로들, 놓은 폴더, 복사/이동 지정).</summary>
    Action<IReadOnlyList<string>, string, DropMode> Drop,
    /// <summary>서버 연결 대화상자를 연다 (저장해 둔 서버를 고르면 미리 채워서; 두 번째 인자는 새로 연결할 때의 프로토콜).</summary>
    Action<NetworkProfile?, string> Connect,
    /// <summary>서버 연결을 끊는다 (인자는 서버 열쇠 <c>sftp://user@host</c>).</summary>
    Action<string> Disconnect,
    Action<NetworkProfile> RemoveProfile,
    /// <summary>지금 연결된 서버들의 열쇠 (<c>sftp://user@host</c>).</summary>
    Func<IReadOnlyList<string>> Connected);
