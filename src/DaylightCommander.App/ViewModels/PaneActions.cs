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
    /// <summary>끌어다 놓기: (경로들, 놓은 폴더, 복사/이동 지정).</summary>
    Action<IReadOnlyList<string>, string, DropMode> Drop,
    /// <summary>서버 연결 대화상자를 연다 (저장해 둔 서버를 고르면 미리 채워서).</summary>
    Action<NetworkProfile?> Connect,
    /// <summary>서버 연결을 끊는다 (인자는 서버 열쇠 <c>sftp://user@host</c>).</summary>
    Action<string> Disconnect,
    Action<NetworkProfile> RemoveProfile,
    /// <summary>지금 연결된 서버들의 열쇠 (<c>sftp://user@host</c>).</summary>
    Func<IReadOnlyList<string>> Connected);
