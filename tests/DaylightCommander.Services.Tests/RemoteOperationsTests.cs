using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>원격이 낀 파일 작업 (메모리 파일 시스템으로 서버를 흉내 낸다): 업로드·다운로드·서버 안 이동·서버 간 복사·충돌·삭제 등.</summary>
public sealed class RemoteOperationsTests : IDisposable
{
    const string A = "sftp://me@alpha"; // 서버 A
    const string B = "sftp://me@beta";  // 서버 B

    readonly string _local = Directory.CreateTempSubdirectory("dc-remote-ops").FullName;
    readonly MemoryFileSystem _a = new(A);
    readonly MemoryFileSystem _b = new(B);
    readonly RemoteSessions _sessions;
    readonly FileOperations _ops;

    public RemoteOperationsTests()
    {
        _sessions = new RemoteSessions(connector: (req, ct) => Task.FromResult<IFileSystem>(req.Host == "alpha" ? _a : _b));
        _sessions.ConnectAsync(new RemoteConnectRequest("sftp", "alpha", 22, "me", "pw")).Wait();
        _sessions.ConnectAsync(new RemoteConnectRequest("sftp", "beta", 22, "me", "pw")).Wait();
        _ops = new FileOperations(remote: _sessions);
        _a.Dir("/docs").Dir("/empty");
    }

    public void Dispose() => Directory.Delete(_local, recursive: true);

    static Task<ConflictDecision> Answer(ConflictChoice c, bool all = false) => Task.FromResult(new ConflictDecision(c, all));

    static readonly ConflictResolver NeverAsk = (_, _, _) => throw new InvalidOperationException("충돌을 물으면 안 된다");

    string L(string name) => Path.Combine(_local, name);

    // --- 업로드·다운로드 ---

    [Fact]
    public async Task 로컬_파일을_원격으로_올리면_내용과_수정_시각이_같다()
    {
        File.WriteAllText(L("a.txt"), "hello");
        var when = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(L("a.txt"), when);

        var result = await _ops.CopyAsync([L("a.txt")], A + "/docs", NeverAsk);

        Assert.Equal(1, result.Processed);
        Assert.Empty(result.Failures);
        Assert.Equal("hello", _a.Read("/docs/a.txt"));
        Assert.Equal(new DateTimeOffset(when), _a.ModifiedOf("/docs/a.txt"));
        Assert.True(File.Exists(L("a.txt"))); // 복사는 원본을 남긴다
    }

    [Fact]
    public async Task 로컬_폴더를_통째로_올린다_빈_폴더도_남는다()
    {
        Directory.CreateDirectory(L("proj/sub"));
        Directory.CreateDirectory(L("proj/blank"));
        File.WriteAllText(L("proj/x.txt"), "X");
        File.WriteAllText(L("proj/sub/y.txt"), "Y");

        var result = await _ops.CopyAsync([L("proj")], A + "/docs", NeverAsk);

        Assert.Empty(result.Failures);
        Assert.Equal("X", _a.Read("/docs/proj/x.txt"));
        Assert.Equal("Y", _a.Read("/docs/proj/sub/y.txt"));
        Assert.True(_a.Exists("/docs/proj/blank"));
    }

    [Fact]
    public async Task 원격_파일과_폴더를_로컬로_내려받는다()
    {
        _a.File("/docs/r.txt", "remote!").File("/docs/dir/inner.txt", "in");

        var result = await _ops.CopyAsync([A + "/docs/r.txt", A + "/docs/dir"], _local, NeverAsk);

        Assert.Empty(result.Failures);
        Assert.Equal("remote!", File.ReadAllText(L("r.txt")));
        Assert.Equal("in", File.ReadAllText(L("dir/inner.txt")));
    }

    [Fact]
    public async Task 다른_서버로의_복사는_스트림으로_바로_옮긴다()
    {
        _a.File("/docs/a.txt", "from alpha");
        _b.Dir("/in");

        var result = await _ops.CopyAsync([A + "/docs/a.txt"], B + "/in", NeverAsk);

        Assert.Empty(result.Failures);
        Assert.Equal("from alpha", _b.Read("/in/a.txt"));
        Assert.True(_a.Exists("/docs/a.txt"));
    }

    // --- 이동 ---

    [Fact]
    public async Task 같은_서버_안의_이동은_이름_바꾸기로_한다()
    {
        _a.File("/docs/m.txt", "move me").Dir("/dest");

        var result = await _ops.MoveAsync([A + "/docs/m.txt"], A + "/dest", NeverAsk);

        Assert.Equal(1, result.Processed);
        Assert.False(_a.Exists("/docs/m.txt"));
        Assert.Equal("move me", _a.Read("/dest/m.txt"));
    }

    [Fact]
    public async Task 다른_서버로_이동하면_복사한_뒤_원본을_지운다()
    {
        _a.File("/docs/m.txt", "move me");
        _b.Dir("/in");

        await _ops.MoveAsync([A + "/docs/m.txt"], B + "/in", NeverAsk);

        Assert.Equal("move me", _b.Read("/in/m.txt"));
        Assert.False(_a.Exists("/docs/m.txt"));
    }

    [Fact]
    public async Task 로컬에서_원격으로_이동하면_로컬_원본이_사라진다()
    {
        File.WriteAllText(L("up.txt"), "up");
        await _ops.MoveAsync([L("up.txt")], A + "/docs", NeverAsk);
        Assert.Equal("up", _a.Read("/docs/up.txt"));
        Assert.False(File.Exists(L("up.txt")));
    }

    [Fact]
    public async Task 같은_폴더로의_이동은_할_일이_없다()
    {
        _a.File("/docs/s.txt", "same");
        var result = await _ops.MoveAsync([A + "/docs/s.txt"], A + "/docs", NeverAsk);
        Assert.Equal(0, result.Processed);
        Assert.Equal("same", _a.Read("/docs/s.txt"));
    }

    // --- 충돌 ---

    [Fact]
    public async Task 같은_폴더로_복사하면_묻지_않고_이름_1로_복제한다()
    {
        _a.File("/docs/d.txt", "dup");
        await _ops.CopyAsync([A + "/docs/d.txt"], A + "/docs", NeverAsk);
        Assert.Equal("dup", _a.Read("/docs/d (1).txt"));
    }

    [Fact]
    public async Task 이름이_겹치면_고른_대로_덮어쓰기_건너뛰기_둘다_유지한다()
    {
        File.WriteAllText(L("c.txt"), "new");
        _a.File("/docs/c.txt", "old");

        await _ops.CopyAsync([L("c.txt")], A + "/docs", (_, _, _) => Answer(ConflictChoice.Skip));
        Assert.Equal("old", _a.Read("/docs/c.txt"));

        await _ops.CopyAsync([L("c.txt")], A + "/docs", (_, _, _) => Answer(ConflictChoice.KeepBoth));
        Assert.Equal("old", _a.Read("/docs/c.txt"));
        Assert.Equal("new", _a.Read("/docs/c (1).txt"));

        await _ops.CopyAsync([L("c.txt")], A + "/docs", (_, _, _) => Answer(ConflictChoice.Overwrite));
        Assert.Equal("new", _a.Read("/docs/c.txt"));
    }

    [Fact]
    public async Task 모두_적용은_남은_충돌에_같은_선택을_쓴다()
    {
        File.WriteAllText(L("1.txt"), "n1");
        File.WriteAllText(L("2.txt"), "n2");
        _a.File("/docs/1.txt", "o1").File("/docs/2.txt", "o2");
        var asked = 0;

        await _ops.CopyAsync([L("1.txt"), L("2.txt")], A + "/docs", (_, _, _) => { asked++; return Answer(ConflictChoice.Overwrite, all: true); });

        Assert.Equal(1, asked);
        Assert.Equal("n1", _a.Read("/docs/1.txt"));
        Assert.Equal("n2", _a.Read("/docs/2.txt"));
    }

    [Fact]
    public async Task 폴더끼리는_합친다()
    {
        Directory.CreateDirectory(L("merge"));
        File.WriteAllText(L("merge/new.txt"), "N");
        _a.File("/docs/merge/old.txt", "O");

        await _ops.CopyAsync([L("merge")], A + "/docs", NeverAsk);

        Assert.Equal("O", _a.Read("/docs/merge/old.txt"));
        Assert.Equal("N", _a.Read("/docs/merge/new.txt"));
    }

    [Fact]
    public async Task 취소를_고르면_작업_전체가_취소된다()
    {
        File.WriteAllText(L("k.txt"), "n");
        _a.File("/docs/k.txt", "o");
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _ops.CopyAsync([L("k.txt")], A + "/docs", (_, _, _) => Answer(ConflictChoice.Cancel)));
    }

    [Fact]
    public async Task 폴더를_자기_안으로_옮기면_그_항목은_실패로_남는다()
    {
        _a.File("/docs/inside/x.txt", "x");
        var copy = await _ops.CopyAsync([A + "/docs"], A + "/docs/inside", NeverAsk);
        Assert.Equal(new FileError.IntoItself("docs"), Assert.Single(copy.Failures).Error);

        var move = await _ops.MoveAsync([A + "/docs"], A + "/docs/inside", NeverAsk);
        Assert.Equal(new FileError.IntoItself("docs"), Assert.Single(move.Failures).Error);
        Assert.True(_a.Exists("/docs/inside/x.txt"));
    }

    [Fact]
    public async Task 연결하지_않은_서버는_실패로_알린다()
    {
        File.WriteAllText(L("n.txt"), "n");
        var result = await _ops.CopyAsync([L("n.txt")], "sftp://me@gamma/docs", NeverAsk);
        Assert.Equal(new FileError.RemoteNotConnected("sftp://me@gamma"), Assert.Single(result.Failures).Error);
    }

    [Fact]
    public async Task 항목_하나가_실패해도_나머지는_계속한다()
    {
        File.WriteAllText(L("ok.txt"), "ok");
        var result = await _ops.CopyAsync([L("missing.txt"), L("ok.txt")], A + "/docs", NeverAsk);
        Assert.Equal(1, result.Processed);
        Assert.Single(result.Failures);
        Assert.Equal("ok", _a.Read("/docs/ok.txt"));
    }

    [Fact]
    public async Task 진행률은_전체_바이트로_보고한다()
    {
        File.WriteAllText(L("p.txt"), new string('x', 1000));
        var reports = new List<OperationProgress>();
        await _ops.CopyAsync([L("p.txt")], A + "/docs", NeverAsk, reports.Add);
        Assert.Equal(1000, reports[^1].Total);
        Assert.Equal(1000, reports[^1].Done);
    }

    // --- 삭제·이름·새로 만들기 ---

    [Fact]
    public async Task 원격_파일과_폴더를_지운다()
    {
        _a.File("/docs/x/y.txt", "y").File("/docs/z.txt", "z");
        var result = await _ops.DeleteAsync([A + "/docs/x", A + "/docs/z.txt"], toTrash: false);
        Assert.Equal(2, result.Processed);
        Assert.False(_a.Exists("/docs/x"));
        Assert.False(_a.Exists("/docs/x/y.txt"));
        Assert.False(_a.Exists("/docs/z.txt"));
    }

    [Fact]
    public async Task 이름을_바꾼다_이미_있으면_거부한다()
    {
        _a.File("/docs/old.txt", "o").File("/docs/taken.txt", "t");
        var renamed = await _ops.RenameAsync(A + "/docs/old.txt", "new.txt");
        Assert.Equal(A + "/docs/new.txt", renamed);
        Assert.True(_a.Exists("/docs/new.txt"));

        var e = await Assert.ThrowsAsync<FileOperationException>(() => _ops.RenameAsync(A + "/docs/new.txt", "taken.txt"));
        Assert.Equal(new FileError.AlreadyExists("taken.txt"), e.Error);

        var bad = await Assert.ThrowsAsync<FileOperationException>(() => _ops.RenameAsync(A + "/docs/new.txt", "a/b"));
        Assert.IsType<FileError.NameInvalidChar>(bad.Error);
    }

    [Fact]
    public async Task 새_폴더와_새_파일을_만든다()
    {
        var dir = await _ops.CreateDirectoryAsync(A + "/docs", "fresh");
        Assert.Equal(A + "/docs/fresh", dir);
        Assert.True(_a.Exists("/docs/fresh"));
        var file = await _ops.CreateFileAsync(A + "/docs/fresh", "n.txt");
        Assert.Equal(A + "/docs/fresh/n.txt", file);
        Assert.Equal("", _a.Read("/docs/fresh/n.txt"));
        await Assert.ThrowsAsync<FileOperationException>(() => _ops.CreateDirectoryAsync(A + "/docs", "fresh"));
    }

    [Fact]
    public async Task 서버에_원격_작업기가_없으면_지원하지_않는다는_오류다()
    {
        var plain = new FileOperations(); // 원격 세션 없이 만든 것
        var e = await Assert.ThrowsAsync<FileOperationException>(() => plain.CreateDirectoryAsync(A + "/docs", "x"));
        Assert.Equal(new FileError.RemoteUnsupported(), e.Error);
    }

    // --- 세션 ---

    [Fact]
    public async Task 연결을_끊으면_그_서버의_경로는_연결_안_됨이다()
    {
        Assert.True(_sessions.IsConnected(A + "/docs"));
        _sessions.Disconnect(A + "/docs");
        Assert.False(_sessions.IsConnected(A + "/docs"));
        var e = Assert.Throws<FileOperationException>(() => _sessions.FileSystemFor(A + "/docs"));
        Assert.Equal(new FileError.RemoteNotConnected(A), e.Error);
        Assert.True(_sessions.IsConnected(B + "/x")); // 다른 서버는 그대로
        await Task.CompletedTask;
    }

    [Fact]
    public async Task 연결하면_홈_경로를_돌려주고_알린다()
    {
        var changes = 0;
        _sessions.Changed += () => changes++;
        var home = await _sessions.ConnectAsync(new RemoteConnectRequest("sftp", "alpha", 2222, "you", "pw"));
        Assert.Equal("sftp://you@alpha:2222/", home);
        Assert.Equal(1, changes);
        Assert.Contains("sftp://you@alpha:2222", _sessions.Connected);
    }
}
