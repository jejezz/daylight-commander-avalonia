using System.Collections.Immutable;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 패널 하나의 이동·정렬·히스토리 (원본 <c>PaneController</c>). UI와 무관해서 6단계에서 패널을 다른 창으로 옮겨도
/// 같은 상태를 볼 수 있다. 커서·선택·빠른 필터는 3단계에서 더한다.
/// <para>
/// 이동은 비동기다. 이동에 실패하면 이동 자체가 일어나지 않고 이전 목록이 그대로 남으며 <see cref="PaneState.Error"/>만
/// 설정된다. 위로/뒤로 갈 때는 방금 있던 폴더에 커서가 놓인다.
/// </para>
/// 상태는 UI 스레드에서만 바꾼다고 가정한다 (await 뒤에 UI 스레드로 돌아온다). 바뀔 때마다 <see cref="StateChanged"/>.
/// </summary>
public sealed class PaneController
{
    public delegate Task<IReadOnlyList<FileEntry>> ListDirectory(string path, CancellationToken cancellationToken);

    readonly ListDirectory _list;
    readonly Action<string> _onVisit;

    /// <param name="onVisit">다른 폴더로 이동에 성공할 때마다 새 경로와 함께 불린다 (최근 경로 기록용).</param>
    public PaneController(string initialPath, ListDirectory list, bool showHidden = false, Action<string>? onVisit = null)
    {
        _list = list;
        _onVisit = onVisit ?? (_ => { });
        State = new PaneState(initialPath) { Loading = true, ShowHidden = showHidden };
    }

    public PaneState State { get; private set; }

    public event Action<PaneState>? StateChanged;

    void Update(Func<PaneState, PaneState> change)
    {
        var old = State;
        var next = change(old);
        if (ReferenceEquals(next, old)) return;
        if (!ReferenceEquals(next.Entries, old.Entries) || next.SortField != old.SortField
            || next.SortAscending != old.SortAscending || next.ShowHidden != old.ShowHidden
            || next.QuickFilter != old.QuickFilter)
        {
            var shown = next.ShowHidden ? next.Entries : next.Entries.Where(e => !e.IsHidden);
            if (next.QuickFilter.Length > 0)
                shown = shown.Where(e => e.Name.Contains(next.QuickFilter, StringComparison.OrdinalIgnoreCase));
            next = next with { VisibleEntries = EntrySorting.Sort(shown, next.SortField, next.SortAscending) };
        }
        State = next;
        StateChanged?.Invoke(next);
    }

    /// <summary>처음 한 번 현재 경로를 읽는다.</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        var entries = await ReadAsync(State.CurrentPath, ct);
        Update(s => s with { Entries = entries ?? [], Loading = false });
    }

    /// <summary>같은 경로를 다시 읽는다. <paramref name="focus"/>가 주어지고 목록에 있으면 커서를 거기로 옮긴다.</summary>
    public async Task RefreshAsync(string? focus = null, CancellationToken ct = default)
    {
        var entries = await ReadAsync(State.CurrentPath, ct);
        if (entries is null) return;
        Update(s => s with
        {
            Entries = entries,
            Error = null,
            Selected = s.Selected.Intersect(entries.Select(e => e.Path)),
            CursorPath = focus is not null && entries.Any(e => e.Path == focus) ? focus : s.CursorPath,
            AnchorPath = null,
        });
    }

    public async Task NavigateToAsync(string path, CancellationToken ct = default)
    {
        string target;
        try
        {
            target = Normalize(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Fail(e, path);
            return;
        }
        if (target == State.CurrentPath) return;
        var entries = await ReadAsync(target, ct);
        if (entries is null) return;
        Update(s => Arrive(s, target, entries, cursorHint: null) with
        {
            BackHistory = [.. s.BackHistory, s.CurrentPath],
            ForwardHistory = [],
        });
        _onVisit(target);
    }

    public async Task GoBackAsync(CancellationToken ct = default)
    {
        if (State.BackHistory.Count == 0) return;
        var target = State.BackHistory[^1];
        var entries = await ReadAsync(target, ct);
        if (entries is null) return;
        Update(s => Arrive(s, target, entries, cursorHint: s.CurrentPath) with
        {
            BackHistory = s.BackHistory.Take(s.BackHistory.Count - 1).ToList(),
            ForwardHistory = [s.CurrentPath, .. s.ForwardHistory],
        });
        _onVisit(target);
    }

    public async Task GoForwardAsync(CancellationToken ct = default)
    {
        if (State.ForwardHistory.Count == 0) return;
        var target = State.ForwardHistory[0];
        var entries = await ReadAsync(target, ct);
        if (entries is null) return;
        Update(s => Arrive(s, target, entries, cursorHint: s.CurrentPath) with
        {
            BackHistory = [.. s.BackHistory, s.CurrentPath],
            ForwardHistory = s.ForwardHistory.Skip(1).ToList(),
        });
        _onVisit(target);
    }

    /// <summary>루트에서는 아무것도 하지 않는다. 방금 있던 폴더에 커서가 놓인다.</summary>
    public async Task GoUpAsync(CancellationToken ct = default)
    {
        var from = State.CurrentPath;
        var parent = Path.GetDirectoryName(from);
        if (parent is null) return;
        await NavigateToAsync(parent, ct);
        Update(s => s.CurrentPath == parent ? s with { CursorPath = from } : s);
    }

    /// <summary>같은 필드를 다시 누르면 오름/내림 토글, 다른 필드는 오름차순부터.</summary>
    public void SetSortField(SortField field) => Update(s =>
        s.SortField == field
            ? s with { SortAscending = !s.SortAscending }
            : s with { SortField = field, SortAscending = true });

    public void ToggleShowHidden() => Update(s => s with { ShowHidden = !s.ShowHidden });

    public void ClearError() => Update(s => s.Error is null ? s : s with { Error = null });

    /// <summary>이동 밖에서 생긴 오류를 이 패널의 상태줄에 띄운다.</summary>
    public void ReportError(FileError error) => Update(s => s with { Error = error });

    // --- 커서와 선택 ---

    /// <summary>커서를 <paramref name="delta"/>칸 옮긴다. <paramref name="extend"/>면 시작점부터 커서까지를 선택한다 (Shift+방향키).</summary>
    public void MoveCursor(int delta, bool extend = false) => Update(s => CursorTo(s, s.CursorIndex + delta, extend));

    public void CursorToStart(bool extend = false) => Update(s => CursorTo(s, 0, extend));

    public void CursorToEnd(bool extend = false) => Update(s => CursorTo(s, int.MaxValue, extend));

    /// <summary>커서 항목의 선택을 토글하고 한 칸 아래로 (Space/Insert).</summary>
    public void ToggleSelectAtCursor() => Update(s =>
    {
        if (s.CursorEntry is not { } entry) return s;
        var selected = s.Selected.Contains(entry.Path) ? s.Selected.Remove(entry.Path) : s.Selected.Add(entry.Path);
        return CursorTo(s with { Selected = selected }, s.CursorIndex + 1, extend: false);
    });

    /// <summary>마우스 클릭. Ctrl은 토글, Shift는 범위, 아무것도 없으면 커서만 옮긴다. 목록에 없는 경로는 무시한다.</summary>
    public void ClickEntry(string path, bool ctrl = false, bool shift = false) => Update(s =>
    {
        if (s.VisibleEntries.All(e => e.Path != path)) return s;
        if (shift)
        {
            var anchor = s.AnchorPath ?? s.CursorEntry?.Path ?? path;
            return s with { CursorPath = path, AnchorPath = anchor, Selected = RangePaths(s.VisibleEntries, anchor, path) };
        }
        if (ctrl)
        {
            return s with
            {
                CursorPath = path,
                AnchorPath = path,
                Selected = s.Selected.Contains(path) ? s.Selected.Remove(path) : s.Selected.Add(path),
            };
        }
        return s.CursorPath == path && s.AnchorPath == path ? s : s with { CursorPath = path, AnchorPath = path };
    });

    /// <summary>
    /// 표시 중인 항목을 모두 선택한다 (Ctrl+A). 이미 모두 선택돼 있으면 전체 해제한다.
    /// 필터로 가려진 항목의 선택은 건드리지 않는다.
    /// </summary>
    public void ToggleSelectAll() => Update(s =>
    {
        if (s.VisibleEntries.Count == 0) return s;
        var visible = s.VisibleEntries.Select(e => e.Path).ToList();
        return visible.All(s.Selected.Contains)
            ? s with { Selected = s.Selected.Except(visible), AnchorPath = null }
            : s with { Selected = s.Selected.Union(visible) };
    });

    /// <summary>선택이 있었으면 지우고 true.</summary>
    public bool ClearSelection()
    {
        if (State.Selected.IsEmpty) return false;
        Update(s => s with { Selected = [], AnchorPath = null });
        return true;
    }

    // --- 빠른 필터 ---

    public void AppendFilter(string text) => Update(s => s with { QuickFilter = s.QuickFilter + text });

    /// <summary>필터가 있었으면 한 글자 지우고 true. 없으면 false (호출한 쪽이 상위 폴더 이동 등으로 쓴다).</summary>
    public bool BackspaceFilter()
    {
        if (State.QuickFilter.Length == 0) return false;
        Update(s => s with { QuickFilter = s.QuickFilter[..^1] });
        return true;
    }

    /// <summary>필터가 있었으면 지우고 true.</summary>
    public bool ClearFilter()
    {
        if (State.QuickFilter.Length == 0) return false;
        Update(s => s with { QuickFilter = "" });
        return true;
    }

    /// <summary>커서가 폴더 위면 들어간다. 파일 열기는 7단계(뷰어).</summary>
    public async Task OpenCursorAsync(CancellationToken ct = default)
    {
        if (State.CursorEntry is { IsDirectory: true } entry) await NavigateToAsync(entry.Path, ct);
    }

    /// <summary>선택된 표시 항목. 선택이 없으면 커서 항목 하나 (파일 작업의 대상, 4단계).</summary>
    public IReadOnlyList<FileEntry> TargetEntries()
    {
        var s = State;
        var picked = s.VisibleEntries.Where(e => s.Selected.Contains(e.Path)).ToList();
        return picked.Count > 0 ? picked : s.CursorEntry is { } c ? [c] : [];
    }

    async Task<IReadOnlyList<FileEntry>?> ReadAsync(string path, CancellationToken ct)
    {
        try
        {
            return await _list(path, ct);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Fail(e, path);
            return null;
        }
    }

    void Fail(Exception e, string path) =>
        Update(s => s with { Loading = false, Error = FileErrors.From(e, path) });

    /// <summary>앞뒤 공백을 지우고 절대 경로로 바꿔 <c>..</c>를 정리한다. 루트가 아니면 끝의 구분자는 뗀다.</summary>
    internal static string Normalize(string path)
    {
        var full = Path.GetFullPath(path.Trim());
        var root = Path.GetPathRoot(full);
        return full == root ? full : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>폴더에 들어간 뒤의 상태. 커서는 <paramref name="cursorHint"/>에 둔다.</summary>
    static PaneState Arrive(PaneState s, string path, IReadOnlyList<FileEntry> entries, string? cursorHint) =>
        s with
        {
            CurrentPath = path,
            Entries = entries,
            Error = null,
            CursorPath = cursorHint,
            AnchorPath = null,
            Selected = [],
            QuickFilter = "",
            Loading = false,
        };

    static PaneState CursorTo(PaneState s, int index, bool extend)
    {
        var visible = s.VisibleEntries;
        if (visible.Count == 0) return s;
        var path = visible[Math.Clamp(index, 0, visible.Count - 1)].Path;
        if (!extend) return s with { CursorPath = path, AnchorPath = null };
        var anchor = s.AnchorPath ?? visible[s.CursorIndex].Path;
        return s with { CursorPath = path, AnchorPath = anchor, Selected = RangePaths(visible, anchor, path) };
    }

    static System.Collections.Immutable.ImmutableHashSet<string> RangePaths(IReadOnlyList<FileEntry> visible, string a, string b)
    {
        var i = IndexOf(visible, a);
        var j = IndexOf(visible, b);
        if (i < 0 || j < 0) return [b];
        var (lo, hi) = i <= j ? (i, j) : (j, i);
        return visible.Skip(lo).Take(hi - lo + 1).Select(e => e.Path).ToImmutableHashSet();
    }

    static int IndexOf(IReadOnlyList<FileEntry> list, string path)
    {
        for (var k = 0; k < list.Count; k++) if (list[k].Path == path) return k;
        return -1;
    }
}
