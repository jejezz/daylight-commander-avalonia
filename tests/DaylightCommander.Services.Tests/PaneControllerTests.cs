using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public class PaneControllerTests
{
    static readonly string Root = Path.GetPathRoot(Directory.GetCurrentDirectory())!;
    static string P(string name) => Path.Combine(Root, name);

    static FileEntry File(string name, long size = 1, bool hidden = false) =>
        new(P(name), name, false, size, null, hidden);

    readonly Dictionary<string, IReadOnlyList<FileEntry>> _fs = new()
    {
        [Root] = [File("r.txt")],
        [P("a")] = [File("a.txt")],
        [P("b")] = [File("b.txt"), File(".h", hidden: true)],
        [P("c")] = [],
    };

    readonly List<string> _calls = [];

    PaneController Controller(string? start = null) => new(start ?? P("a"), (path, _) =>
    {
        _calls.Add(path);
        return _fs.TryGetValue(path, out var list)
            ? Task.FromResult(list)
            : throw new DirectoryNotFoundException(path);
    });

    async Task<PaneController> Started(string? start = null)
    {
        var c = Controller(start);
        await c.StartAsync();
        return c;
    }

    [Fact]
    public async Task 시작하면_현재_경로_목록을_읽는다()
    {
        var c = Controller();
        Assert.True(c.State.Loading);
        await c.StartAsync();
        Assert.False(c.State.Loading);
        Assert.Equal(["a.txt"], c.State.Entries.Select(e => e.Name));
    }

    [Fact]
    public async Task 시작_경로를_못_읽으면_빈_목록과_오류()
    {
        var c = Controller(P("missing"));
        await c.StartAsync();
        Assert.False(c.State.Loading);
        Assert.Empty(c.State.Entries);
        Assert.NotNull(c.State.Error);
    }

    [Fact]
    public async Task 이동은_BackHistory에_쌓고_ForwardHistory를_비운다()
    {
        var c = await Started();
        await c.NavigateToAsync(P("b"));
        await c.NavigateToAsync(P("c"));
        Assert.Equal(P("c"), c.State.CurrentPath);
        Assert.Equal([P("a"), P("b")], c.State.BackHistory);

        await c.GoBackAsync();
        Assert.Equal([P("c")], c.State.ForwardHistory);
        await c.NavigateToAsync(P("a"));
        Assert.Empty(c.State.ForwardHistory);
    }

    [Fact]
    public async Task 뒤로_앞으로()
    {
        var c = await Started();
        await c.NavigateToAsync(P("b"));
        await c.NavigateToAsync(P("c"));

        await c.GoBackAsync();
        Assert.Equal(P("b"), c.State.CurrentPath);
        Assert.Equal([P("a")], c.State.BackHistory);
        Assert.Equal([P("c")], c.State.ForwardHistory);

        await c.GoBackAsync();
        Assert.Equal(P("a"), c.State.CurrentPath);
        Assert.Equal([P("b"), P("c")], c.State.ForwardHistory);

        await c.GoForwardAsync();
        Assert.Equal(P("b"), c.State.CurrentPath);
        Assert.Equal([P("a")], c.State.BackHistory);
        Assert.Equal([P("c")], c.State.ForwardHistory);
    }

    [Fact]
    public async Task 히스토리가_없으면_뒤로_앞으로는_아무_일도_없다()
    {
        var c = await Started();
        var before = c.State;
        await c.GoBackAsync();
        await c.GoForwardAsync();
        Assert.Same(before, c.State);
    }

    [Fact]
    public async Task 같은_경로로는_이동하지_않는다()
    {
        var c = await Started();
        _calls.Clear();
        await c.NavigateToAsync(P("a"));
        Assert.Empty(c.State.BackHistory);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task 이동에_실패하면_이전_목록과_히스토리가_그대로고_오류만_설정된다()
    {
        var c = await Started();
        await c.NavigateToAsync(P("missing"));
        Assert.Equal(P("a"), c.State.CurrentPath);
        Assert.Equal(["a.txt"], c.State.Entries.Select(e => e.Name));
        Assert.Empty(c.State.BackHistory);
        Assert.IsType<FileError.NotFound>(c.State.Error);

        c.ClearError();
        Assert.Null(c.State.Error);
    }

    [Fact]
    public async Task 뒤로_가기에_실패해도_상태가_유지된다()
    {
        var c = await Started();
        await c.NavigateToAsync(P("b"));
        _fs.Remove(P("a"));
        await c.GoBackAsync();
        Assert.Equal(P("b"), c.State.CurrentPath);
        Assert.Equal([P("a")], c.State.BackHistory);
        Assert.NotNull(c.State.Error);
    }

    [Fact]
    public async Task 성공하면_이전_오류가_지워진다()
    {
        var c = await Started();
        await c.NavigateToAsync(P("missing"));
        await c.NavigateToAsync(P("b"));
        Assert.Null(c.State.Error);
    }

    [Fact]
    public async Task GoUp은_부모로_이동하고_루트에서는_아무것도_안_한다()
    {
        var c = await Started();
        Assert.True(c.State.CanGoUp);
        await c.GoUpAsync();
        Assert.Equal(Root, c.State.CurrentPath);
        Assert.False(c.State.CanGoUp);

        var before = c.State;
        await c.GoUpAsync();
        Assert.Same(before, c.State);
    }

    [Fact]
    public async Task 위로_가면_방금_있던_폴더에_커서가_놓인다()
    {
        _fs[Root] = [File("r.txt"), new FileEntry(P("a"), "a", true)];
        var c = await Started();
        await c.GoUpAsync();
        Assert.Equal(P("a"), c.State.CursorEntry?.Path);
    }

    [Fact]
    public async Task 뒤로_가면_방금_있던_폴더에_커서가_놓인다()
    {
        _fs[Root] = [new FileEntry(P("a"), "a", true), File("r.txt")];
        var c = await Started(Root);
        await c.NavigateToAsync(P("a"));
        await c.GoBackAsync();
        Assert.Equal(Root, c.State.CurrentPath);
        Assert.Equal(P("a"), c.State.CursorEntry?.Path);
    }

    [Fact]
    public void 정렬_필드_토글_규칙()
    {
        var c = Controller();
        c.SetSortField(SortField.Size);
        Assert.Equal(SortField.Size, c.State.SortField);
        Assert.True(c.State.SortAscending);

        c.SetSortField(SortField.Size);
        Assert.False(c.State.SortAscending);

        c.SetSortField(SortField.Name);
        Assert.Equal(SortField.Name, c.State.SortField);
        Assert.True(c.State.SortAscending);
    }

    [Fact]
    public async Task 정렬을_바꿔도_다시_읽지_않는다()
    {
        var c = await Started();
        _calls.Clear();
        c.SetSortField(SortField.Size);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task 정렬을_바꾸면_표시_목록이_다시_정렬된다()
    {
        _fs[P("a")] = [File("big", 100), File("small", 1), File("mid", 50)];
        var c = await Started();
        c.SetSortField(SortField.Size);
        Assert.Equal(["small", "mid", "big"], c.State.VisibleEntries.Select(e => e.Name));
        c.SetSortField(SortField.Size);
        Assert.Equal(["big", "mid", "small"], c.State.VisibleEntries.Select(e => e.Name));
    }

    [Fact]
    public async Task 숨김_파일은_토글로_보이고_감춘다()
    {
        var c = await Started(P("b"));
        Assert.Equal(["b.txt"], c.State.VisibleEntries.Select(e => e.Name));
        c.ToggleShowHidden();
        Assert.Equal([".h", "b.txt"], c.State.VisibleEntries.Select(e => e.Name));
        c.ToggleShowHidden();
        Assert.Equal(["b.txt"], c.State.VisibleEntries.Select(e => e.Name));
    }

    [Fact]
    public async Task 경로_문자열은_정규화되고_잘못된_경로는_오류()
    {
        var c = await Started();
        var sep = Path.DirectorySeparatorChar;
        await c.NavigateToAsync($"  {P("b")}{sep}x{sep}..  ");
        Assert.Equal(P("b"), c.State.CurrentPath);

        await c.NavigateToAsync("bad\0path");
        Assert.Equal(P("b"), c.State.CurrentPath);
        Assert.NotNull(c.State.Error);
    }

    [Fact]
    public async Task 끝의_구분자는_떼고_루트는_그대로_둔다()
    {
        var c = await Started();
        await c.NavigateToAsync(P("b") + Path.DirectorySeparatorChar);
        Assert.Equal(P("b"), c.State.CurrentPath);
        await c.NavigateToAsync(Root);
        Assert.Equal(Root, c.State.CurrentPath);
    }

    [Fact]
    public async Task 새로고침은_같은_경로를_다시_읽는다()
    {
        var c = await Started();
        _fs[P("a")] = [File("a.txt"), File("new.txt")];
        await c.RefreshAsync();
        Assert.Equal(["a.txt", "new.txt"], c.State.Entries.Select(e => e.Name));
        Assert.Empty(c.State.BackHistory);
    }

    [Fact]
    public async Task 이동에_성공하면_onVisit이_불린다()
    {
        var visited = new List<string>();
        var c = new PaneController(P("a"), (p, _) => Task.FromResult(_fs[p]), onVisit: visited.Add);
        await c.StartAsync();
        await c.NavigateToAsync(P("b"));
        await c.NavigateToAsync(P("c"));
        await c.GoBackAsync();
        Assert.Equal([P("b"), P("c"), P("b")], visited);
    }

    [Fact]
    public async Task 상태가_바뀌면_StateChanged가_불린다()
    {
        var c = await Started();
        var count = 0;
        c.StateChanged += _ => count++;
        c.ToggleShowHidden();
        c.SetSortField(SortField.Size);
        Assert.Equal(2, count);
        c.ClearError(); // 오류가 없으면 바뀌지 않는다
        Assert.Equal(2, count);
    }

    // ---- 3단계: 커서·선택·빠른 필터 ----
    // 정렬 후 순서: sub(폴더), apple, Apricot, banana, cherry

    async Task<PaneController> Many()
    {
        _fs[P("many")] =
        [
            new FileEntry(P("sub"), "sub", true),
            File("apple", 10), File("Apricot", 20), File("banana", 30), File("cherry", 40),
        ];
        return await Started(P("many"));
    }

    static string? CursorName(PaneController c) => c.State.CursorEntry?.Name;

    static HashSet<string> SelectedNames(PaneController c) =>
        c.State.VisibleEntries.Where(e => c.State.Selected.Contains(e.Path)).Select(e => e.Name).ToHashSet();

    [Fact]
    public async Task 커서는_처음에_첫_항목이고_범위를_넘지_않는다()
    {
        var c = await Many();
        Assert.Equal("sub", CursorName(c));
        c.MoveCursor(-1);
        Assert.Equal("sub", CursorName(c));
        c.MoveCursor(2);
        Assert.Equal("Apricot", CursorName(c));
        c.CursorToEnd();
        Assert.Equal("cherry", CursorName(c));
        c.MoveCursor(5);
        Assert.Equal("cherry", CursorName(c));
        c.CursorToStart();
        Assert.Equal("sub", CursorName(c));
    }

    [Fact]
    public async Task 빈_폴더에서_커서_조작은_안전하다()
    {
        var c = await Started(P("c"));
        c.MoveCursor(1);
        c.ToggleSelectAtCursor();
        c.CursorToEnd(extend: true);
        Assert.Equal(-1, c.State.CursorIndex);
        Assert.Empty(c.State.Selected);
    }

    [Fact]
    public async Task Space는_선택을_토글하고_한_칸_아래로_간다()
    {
        var c = await Many();
        c.ToggleSelectAtCursor();
        Assert.Equal(new HashSet<string> { "sub" }, SelectedNames(c));
        Assert.Equal("apple", CursorName(c));
        c.MoveCursor(-1);
        c.ToggleSelectAtCursor();
        Assert.Empty(SelectedNames(c));
    }

    [Fact]
    public async Task Shift_방향키는_시작점부터_커서까지를_선택하고_되돌리면_줄어든다()
    {
        var c = await Many();
        c.MoveCursor(1); // apple
        c.MoveCursor(1, extend: true); // Apricot
        c.MoveCursor(1, extend: true); // banana
        Assert.Equal(new HashSet<string> { "apple", "Apricot", "banana" }, SelectedNames(c));
        c.MoveCursor(-1, extend: true);
        Assert.Equal(new HashSet<string> { "apple", "Apricot" }, SelectedNames(c));
    }

    [Fact]
    public async Task 클릭은_커서만_Ctrl은_토글_Shift는_범위()
    {
        var c = await Many();
        var v = c.State.VisibleEntries.ToDictionary(e => e.Name);
        c.ClickEntry(v["apple"].Path);
        Assert.Equal("apple", CursorName(c));
        Assert.Empty(c.State.Selected);

        c.ClickEntry(v["cherry"].Path, ctrl: true);
        Assert.Equal(new HashSet<string> { "cherry" }, SelectedNames(c));
        c.ClickEntry(v["banana"].Path, ctrl: true);
        Assert.Equal(new HashSet<string> { "banana", "cherry" }, SelectedNames(c));
        c.ClickEntry(v["banana"].Path, ctrl: true);
        Assert.Equal(new HashSet<string> { "cherry" }, SelectedNames(c));

        c.ClickEntry(v["Apricot"].Path);
        c.ClickEntry(v["banana"].Path, shift: true);
        Assert.Equal(new HashSet<string> { "Apricot", "banana" }, SelectedNames(c));
    }

    [Fact]
    public async Task 목록에_없는_경로를_클릭하면_무시한다()
    {
        var c = await Many();
        var before = c.State;
        c.ClickEntry(P("nope"));
        Assert.Same(before, c.State);
    }

    [Fact]
    public async Task 전체_선택과_해제()
    {
        var c = await Many();
        c.ToggleSelectAll();
        Assert.Equal(5, c.State.Selected.Count);
        Assert.True(c.ClearSelection());
        Assert.False(c.ClearSelection());
        Assert.Empty(c.State.Selected);
    }

    [Fact]
    public async Task Ctrl_A를_다시_누르면_전체_해제된다()
    {
        var c = await Many();
        c.ToggleSelectAll();
        Assert.Equal(5, c.State.Selected.Count);
        c.ToggleSelectAll();
        Assert.Empty(c.State.Selected);
        c.ToggleSelectAll();
        Assert.Equal(5, c.State.Selected.Count);
    }

    [Fact]
    public async Task 일부만_선택된_상태에서_Ctrl_A는_전체_선택이다()
    {
        var c = await Many();
        c.ToggleSelectAtCursor();
        c.ToggleSelectAll();
        Assert.Equal(5, c.State.Selected.Count);
    }

    [Fact]
    public async Task 필터_중_Ctrl_A는_보이는_항목만_선택하고_해제하며_가려진_선택은_유지한다()
    {
        var c = await Many();
        c.CursorToEnd();
        c.ToggleSelectAtCursor(); // cherry
        c.AppendFilter("ap");
        c.ToggleSelectAll();
        Assert.Equal(new HashSet<string> { "cherry", "apple", "Apricot" },
            c.State.Entries.Where(e => c.State.Selected.Contains(e.Path)).Select(e => e.Name).ToHashSet());
        c.ToggleSelectAll();
        Assert.Equal(new HashSet<string> { "cherry" },
            c.State.Entries.Where(e => c.State.Selected.Contains(e.Path)).Select(e => e.Name).ToHashSet());
    }

    [Fact]
    public async Task 빠른_필터는_이름_부분_일치로_거르고_지우면_돌아온다()
    {
        var c = await Many();
        c.AppendFilter("A");
        Assert.Equal(["apple", "Apricot", "banana"], c.State.VisibleEntries.Select(e => e.Name));
        c.AppendFilter("p");
        Assert.Equal(["apple", "Apricot"], c.State.VisibleEntries.Select(e => e.Name));
        Assert.True(c.BackspaceFilter());
        Assert.Equal("A", c.State.QuickFilter);
        Assert.True(c.ClearFilter());
        Assert.False(c.ClearFilter());
        Assert.False(c.BackspaceFilter());
        Assert.Equal(5, c.State.VisibleEntries.Count);
    }

    [Fact]
    public async Task 필터로_커서_항목이_사라지면_첫_일치_항목에_커서가_간다()
    {
        var c = await Many();
        c.CursorToEnd(); // cherry
        c.AppendFilter("app");
        Assert.Equal("apple", CursorName(c));
    }

    [Fact]
    public async Task Enter는_커서가_폴더일_때만_들어가고_들어가면_필터와_선택이_초기화된다()
    {
        _fs[P("sub")] = [File("inner")];
        var c = await Many();
        c.CursorToEnd();
        await c.OpenCursorAsync(); // 파일: 이동 없음
        Assert.Equal(P("many"), c.State.CurrentPath);

        c.CursorToStart();
        c.ToggleSelectAll();
        c.AppendFilter("s");
        await c.OpenCursorAsync();
        Assert.Equal(P("sub"), c.State.CurrentPath);
        Assert.Empty(c.State.Selected);
        Assert.Equal("", c.State.QuickFilter);
    }

    [Fact]
    public async Task 새로고침은_사라진_항목을_선택에서_뺀다()
    {
        var c = await Many();
        c.ToggleSelectAll();
        _fs[P("many")] = _fs[P("many")].Where(e => e.Name != "banana").ToList();
        await c.RefreshAsync();
        Assert.Equal(4, c.State.Selected.Count);
        Assert.DoesNotContain("banana", SelectedNames(c));
    }

    [Fact]
    public async Task 대상_항목은_선택이_있으면_선택_없으면_커서_항목이다()
    {
        var c = await Many();
        Assert.Equal(["sub"], c.TargetEntries().Select(e => e.Name));
        c.MoveCursor(1);
        c.ToggleSelectAtCursor(); // apple 선택, 커서는 Apricot로
        c.ToggleSelectAtCursor(); // Apricot 선택
        Assert.Equal(["apple", "Apricot"], c.TargetEntries().Select(e => e.Name));
    }
}
