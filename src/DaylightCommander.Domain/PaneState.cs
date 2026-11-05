namespace DaylightCommander.Domain;

/// <summary>한 패널의 상태 (원본 <c>PaneState</c>). 불변이라 <c>with</c>로 갱신한다.</summary>
public sealed record PaneState(string CurrentPath)
{
    /// <summary>읽어 온 그대로의 목록. 표시용은 <see cref="VisibleEntries"/>.</summary>
    public IReadOnlyList<FileEntry> Entries { get; init; } = [];

    /// <summary>숨김·필터를 적용하고 정렬한 표시용 목록. 컨트롤러가 항목·정렬·숨김·필터가 바뀔 때만 다시 계산해 채운다.</summary>
    public IReadOnlyList<FileEntry> VisibleEntries { get; init; } = [];

    public bool Loading { get; init; }

    /// <summary>마지막 이동·읽기 실패. 화면이 언어에 맞춰 문장으로 만든다.</summary>
    public FileError? Error { get; init; }

    /// <summary>가장 최근 경로가 마지막.</summary>
    public IReadOnlyList<string> BackHistory { get; init; } = [];

    /// <summary>가장 가까운 앞 경로가 처음.</summary>
    public IReadOnlyList<string> ForwardHistory { get; init; } = [];

    public SortField SortField { get; init; } = SortField.Name;
    public bool SortAscending { get; init; } = true;
    public bool ShowHidden { get; init; }

    /// <summary>커서가 있는 항목의 경로. 목록에 없으면 첫 항목으로 본다 (<see cref="CursorIndex"/>).</summary>
    public string? CursorPath { get; init; }

    /// <summary>Shift 범위 선택의 시작점.</summary>
    public string? AnchorPath { get; init; }

    /// <summary>선택된 항목의 경로. 필터로 가려진 항목의 선택도 유지된다.</summary>
    public System.Collections.Immutable.ImmutableHashSet<string> Selected { get; init; } = [];

    /// <summary>대소문자 무시 부분 일치. 비어 있으면 필터 없음.</summary>
    public string QuickFilter { get; init; } = "";

    public bool CanGoBack => BackHistory.Count > 0;
    public bool CanGoForward => ForwardHistory.Count > 0;

    /// <summary>루트(<c>C:\</c>, <c>/</c>)에는 부모가 없다.</summary>
    public bool CanGoUp => Path.GetDirectoryName(CurrentPath) is not null;

    /// <summary>표시 목록 안의 커서 위치. 목록이 비면 -1.</summary>
    public int CursorIndex
    {
        get
        {
            if (VisibleEntries.Count == 0) return -1;
            if (CursorPath is null) return 0;
            for (var i = 0; i < VisibleEntries.Count; i++)
                if (VisibleEntries[i].Path == CursorPath) return i;
            return 0;
        }
    }

    public FileEntry? CursorEntry => CursorIndex is var i and >= 0 ? VisibleEntries[i] : null;
}
