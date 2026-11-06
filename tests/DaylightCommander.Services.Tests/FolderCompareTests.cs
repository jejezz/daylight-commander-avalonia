using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>폴더 비교 규칙(FolderComparison)과 HomeState의 비교 모드·동기화.</summary>
public sealed class FolderCompareTests : IAsyncLifetime
{
    static readonly DateTimeOffset T0 = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    static FileEntry F(string dir, string name, long size = 1, DateTimeOffset? modified = null) =>
        new(Path.Combine(dir, name), name, false, size, modified ?? T0);

    static FileEntry D(string dir, string name) => new(Path.Combine(dir, name), name, true);

    // --- 순수 규칙 ---

    [Fact]
    public void 한쪽에만_있으면_이쪽에만이고_같으면_결과에_없다()
    {
        var l = new[] { F("L", "a.txt"), F("L", "both.txt") };
        var r = new[] { F("R", "b.txt"), F("R", "both.txt") };
        var c = FolderComparison.Compare(l, r);
        Assert.Equal(FileDiffStatus.OnlyHere, c.Left[Path.Combine("L", "a.txt")]);
        Assert.Equal(FileDiffStatus.OnlyHere, c.Right[Path.Combine("R", "b.txt")]);
        Assert.DoesNotContain(Path.Combine("L", "both.txt"), c.Left.Keys);
        Assert.DoesNotContain(Path.Combine("R", "both.txt"), c.Right.Keys);
    }

    [Fact]
    public void 크기나_수정_시각이_다르면_다름이다()
    {
        var l = new[] { F("L", "size.txt", 1), F("L", "time.txt", 1, T0) };
        var r = new[] { F("R", "size.txt", 2), F("R", "time.txt", 1, T0.AddMinutes(1)) };
        var c = FolderComparison.Compare(l, r);
        Assert.Equal(FileDiffStatus.Differs, c.Left[Path.Combine("L", "size.txt")]);
        Assert.Equal(FileDiffStatus.Differs, c.Left[Path.Combine("L", "time.txt")]);
        Assert.Equal(FileDiffStatus.Differs, c.Right[Path.Combine("R", "size.txt")]);
    }

    [Fact]
    public void 양쪽에_있는_폴더는_같다고_본다()
    {
        var c = FolderComparison.Compare([D("L", "dir")], [D("R", "dir")]);
        Assert.Empty(c.Left);
        Assert.Empty(c.Right);
    }

    [Fact]
    public void 이름이_같고_형식이_다르면_다름이_아니라_같다()
    {
        // 원본과 같다: 파일↔폴더는 내용 비교 대상이 아니다.
        var c = FolderComparison.Compare([F("L", "x")], [D("R", "x")]);
        Assert.Empty(c.Left);
    }

    [Fact]
    public void 다른_짝은_이름으로_맞춘다()
    {
        var l = new[] { F("L", "a.txt", 1), F("L", "same.txt", 5), F("L", "only.txt") };
        var r = new[] { F("R", "a.txt", 2), F("R", "same.txt", 5) };
        var pairs = FolderComparison.DiffPairs(l, r);
        var (left, right) = Assert.Single(pairs);
        Assert.Equal("a.txt", left.Name);
        Assert.Equal(2, right.SizeBytes);
    }

    // --- HomeState ---

    readonly string _root = Directory.CreateTempSubdirectory("dc-compare-test").FullName;
    readonly string _left;
    readonly string _right;
    readonly ListLocalDirectory _lister = new();
    HomeState _home = null!;

    public FolderCompareTests()
    {
        _left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
        _right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
        File.WriteAllText(Path.Combine(_left, "onlyL.txt"), "L");
        File.WriteAllText(Path.Combine(_right, "onlyR.txt"), "R");
        File.WriteAllText(Path.Combine(_left, "diff.txt"), "left version");
        File.WriteAllText(Path.Combine(_right, "diff.txt"), "right!");
        File.WriteAllText(Path.Combine(_left, "same.txt"), "same");
        File.WriteAllText(Path.Combine(_right, "same.txt"), "same");
        File.SetLastWriteTimeUtc(Path.Combine(_left, "same.txt"), T0.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(_right, "same.txt"), T0.UtcDateTime);
    }

    public async Task InitializeAsync()
    {
        _home = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct));
        await _home.Left.StartAsync();
        await _home.Right.StartAsync();
    }

    public Task DisposeAsync()
    {
        _home.Dispose();
        Directory.Delete(_root, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>덮어쓰는 도중(지웠다가 쓰는 사이)에는 읽을 수 없으므로 null — 그 사이는 "아직"으로 본다.</summary>
    static string? TryRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; } // Windows는 지우는 중인 파일을 읽으면 접근 거부를 낸다
    }

    string L(string n) => Path.Combine(_left, n);
    string R(string n) => Path.Combine(_right, n);

    static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    Task WaitQueueIdle() => WaitUntil(() =>
    {
        if (_home.Queue.Conflict is { } c) throw new InvalidOperationException($"conflict {c.Source} -> {c.Target}");
        return _home.Queue.Jobs.Count > 0 && _home.Queue.Jobs.All(j => j.IsFinished);
    });

    [Fact]
    public void 비교_모드가_꺼져_있으면_결과가_없고_켜면_알린다()
    {
        Assert.Null(_home.Comparison);
        var changes = 0;
        _home.CompareChanged += () => changes++;
        _home.SetCompare(true);
        _home.SetCompare(true);
        Assert.Equal(1, changes);
        Assert.NotNull(_home.Comparison);
        Assert.Equal(FileDiffStatus.OnlyHere, _home.Comparison!.Left[L("onlyL.txt")]);
        Assert.Equal(FileDiffStatus.Differs, _home.Comparison.Right[R("diff.txt")]);
    }

    [Fact]
    public void 왼쪽_차이_복사는_왼쪽에만_있는_것과_다른_것을_오른쪽으로_복사한다()
    {
        _home.SetCompare(true);
        _home.SyncFolders(PaneSide.Left);
        Assert.Single(_home.Queue.Jobs);
        Assert.Equal(2, _home.Queue.Jobs[0].Label.Count); // onlyL, diff
    }

    [Fact]
    public async Task 양방향_동기화는_한쪽에만_있는_것을_복사하고_다른_파일은_하나씩_묻는다()
    {
        _home.SetCompare(true);
        _home.SyncBidirectional();
        await WaitQueueIdle();
        Assert.Equal("L", File.ReadAllText(R("onlyL.txt")));
        Assert.Equal("R", File.ReadAllText(L("onlyR.txt")));

        var d = Assert.IsType<HomeDialog.SyncConflict>(_home.Dialog);
        Assert.Equal("diff.txt", d.Left.Name);

        _home.ResolveSyncConflict(SyncChoice.UseLeft);
        await WaitUntil(() => TryRead(R("diff.txt")) == "left version");
        Assert.Null(_home.Dialog);
        Assert.Equal("left version", File.ReadAllText(L("diff.txt")));
    }

    [Fact]
    public async Task 오른쪽_파일을_고르면_왼쪽이_바뀐다()
    {
        _home.SyncBidirectional();
        _home.ResolveSyncConflict(SyncChoice.UseRight);
        await WaitUntil(() => TryRead(L("diff.txt")) == "right!");
        Assert.Equal("right!", File.ReadAllText(R("diff.txt")));
    }

    [Fact]
    public void 건너뛰기는_아무것도_바꾸지_않고_취소하면_남은_것도_그만둔다()
    {
        File.WriteAllText(L("diff2.txt"), "aaaa");
        File.WriteAllText(R("diff2.txt"), "bb");
        _home.Left.RefreshAsync().Wait();
        _home.Right.RefreshAsync().Wait();

        _home.SyncBidirectional();
        var first = Assert.IsType<HomeDialog.SyncConflict>(_home.Dialog);
        _home.ResolveSyncConflict(SyncChoice.Skip);
        var second = Assert.IsType<HomeDialog.SyncConflict>(_home.Dialog);
        Assert.NotEqual(first.Left.Name, second.Left.Name);
        Assert.Equal("left version", File.ReadAllText(L("diff.txt")));

        _home.ResolveSyncConflict(SyncChoice.Cancel);
        Assert.Null(_home.Dialog);
        _home.ResolveSyncConflict(SyncChoice.UseLeft); // 이미 끝났다 — 아무 일도 없다
        Assert.Null(_home.Dialog);
    }

    [Fact]
    public void 사용자가_충돌_창을_닫으면_동기화가_끝난다()
    {
        _home.SyncBidirectional();
        Assert.IsType<HomeDialog.SyncConflict>(_home.Dialog);
        _home.CloseDialog();
        _home.ResolveSyncConflict(SyncChoice.UseLeft);
        Assert.Null(_home.Dialog);
    }
}
