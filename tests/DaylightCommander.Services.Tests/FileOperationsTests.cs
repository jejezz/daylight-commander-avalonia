using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public sealed class FileOperationsTests : IDisposable
{
    sealed class FakeTrash(Action<string>? onTrash = null) : ITrash
    {
        public List<string> Trashed { get; } = [];
        public bool IsSupported => true;

        public void MoveToTrash(string path)
        {
            onTrash?.Invoke(path);
            Trashed.Add(path);
        }
    }

    readonly string _root = Directory.CreateTempSubdirectory("dc-ops-test").FullName;
    readonly string _src;
    readonly string _dst;
    readonly FakeTrash _trash = new();
    readonly FileOperations _ops;

    public FileOperationsTests()
    {
        _src = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
        _dst = Directory.CreateDirectory(Path.Combine(_root, "dst")).FullName;
        _ops = new FileOperations(_trash);
    }

    public void Dispose()
    {
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    string S(string rel) => Path.Combine(_src, rel);
    string D(string rel) => Path.Combine(_dst, rel);

    static string Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    static ConflictResolver Always(ConflictChoice choice, bool applyToAll = false) =>
        (_, _, _) => Task.FromResult(new ConflictDecision(choice, applyToAll));

    static readonly ConflictResolver NeverAsk = (s, t, _) => throw new InvalidOperationException($"묻지 않아야 한다: {s} → {t}");

    Task<OperationResult> Copy(string[] sources, string? to = null, ConflictResolver? resolver = null) =>
        _ops.CopyAsync(sources, to ?? _dst, resolver ?? NeverAsk);

    Task<OperationResult> Move(string[] sources, string? to = null, ConflictResolver? resolver = null) =>
        _ops.MoveAsync(sources, to ?? _dst, resolver ?? NeverAsk);

    // --- 복사 ---

    [Fact]
    public async Task 파일과_폴더_트리를_복사하고_원본은_그대로다()
    {
        Write(S("a.txt"), "A");
        Write(S("tree/b.txt"), "B");
        Write(S("tree/deep/c.txt"), "C");
        Directory.CreateDirectory(S("empty"));

        var result = await Copy([S("a.txt"), S("tree"), S("empty")]);

        Assert.Equal(3, result.Processed);
        Assert.Empty(result.Failures);
        Assert.Equal("A", File.ReadAllText(D("a.txt")));
        Assert.Equal("B", File.ReadAllText(D("tree/b.txt")));
        Assert.Equal("C", File.ReadAllText(D("tree/deep/c.txt")));
        Assert.True(Directory.Exists(D("empty")));
        Assert.True(File.Exists(S("tree/deep/c.txt")));
    }

    [Fact]
    public async Task 충돌에서_덮어쓰기를_고르면_내용이_바뀐다()
    {
        Write(S("a.txt"), "new");
        Write(D("a.txt"), "old");
        var result = await Copy([S("a.txt")], resolver: Always(ConflictChoice.Overwrite));
        Assert.Equal(1, result.Processed);
        Assert.Equal("new", File.ReadAllText(D("a.txt")));
    }

    [Fact]
    public async Task 충돌에서_건너뛰기를_고르면_대상은_그대로고_처리_개수에_안_센다()
    {
        Write(S("a.txt"), "new");
        Write(D("a.txt"), "old");
        var result = await Copy([S("a.txt")], resolver: Always(ConflictChoice.Skip));
        Assert.Equal(0, result.Processed);
        Assert.Empty(result.Failures);
        Assert.Equal("old", File.ReadAllText(D("a.txt")));
    }

    [Fact]
    public async Task 둘_다_유지는_이름_1로_복사한다()
    {
        Write(S("a.txt"), "new");
        Write(D("a.txt"), "old");
        await Copy([S("a.txt")], resolver: Always(ConflictChoice.KeepBoth));
        Assert.Equal("old", File.ReadAllText(D("a.txt")));
        Assert.Equal("new", File.ReadAllText(D("a (1).txt")));
    }

    [Fact]
    public async Task 모두_적용을_고르면_한_번만_묻는다()
    {
        foreach (var n in new[] { "1", "2", "3" })
        {
            Write(S($"f{n}.txt"), $"new{n}");
            Write(D($"f{n}.txt"), $"old{n}");
        }
        var asked = 0;
        var result = await Copy([S("f1.txt"), S("f2.txt"), S("f3.txt")], resolver: (_, _, _) =>
        {
            asked++;
            return Task.FromResult(new ConflictDecision(ConflictChoice.Overwrite, ApplyToAll: true));
        });
        Assert.Equal(1, asked);
        Assert.Equal(3, result.Processed);
        Assert.Equal("new3", File.ReadAllText(D("f3.txt")));
    }

    [Fact]
    public async Task 충돌에서_취소를_고르면_작업이_취소되고_이후_항목은_건드리지_않는다()
    {
        Write(S("a.txt"), "new");
        Write(S("z.txt"), "z");
        Write(D("a.txt"), "old");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Copy([S("a.txt"), S("z.txt")], resolver: Always(ConflictChoice.Cancel)));
        Assert.Equal("old", File.ReadAllText(D("a.txt")));
        Assert.False(File.Exists(D("z.txt")));
    }

    [Fact]
    public async Task 같은_폴더로_복사하면_묻지_않고_이름_1로_만든다()
    {
        Write(S("a.txt"), "A");
        await Copy([S("a.txt")], to: _src);
        Assert.Equal("A", File.ReadAllText(S("a (1).txt")));
    }

    [Fact]
    public async Task 폴더끼리_이름이_같으면_묻지_않고_합친다()
    {
        Write(S("d/x.txt"), "X");
        Write(D("d/y.txt"), "Y");
        var result = await Copy([S("d")]);
        Assert.Equal(1, result.Processed);
        Assert.Equal("X", File.ReadAllText(D("d/x.txt")));
        Assert.Equal("Y", File.ReadAllText(D("d/y.txt")));
    }

    [Fact]
    public async Task 합치다_같은_파일이_있으면_그_파일만_묻는다()
    {
        Write(S("d/x.txt"), "new");
        Write(D("d/x.txt"), "old");
        var asked = 0;
        await Copy([S("d")], resolver: (_, _, _) =>
        {
            asked++;
            return Task.FromResult(new ConflictDecision(ConflictChoice.Overwrite));
        });
        Assert.Equal(1, asked);
        Assert.Equal("new", File.ReadAllText(D("d/x.txt")));
    }

    [Fact]
    public async Task 폴더를_자기_자신_안으로_복사하면_실패로_기록하고_아무것도_만들지_않는다()
    {
        Write(S("d/x.txt"), "X");
        var result = await Copy([S("d")], to: S("d"));
        Assert.Equal(0, result.Processed);
        Assert.Single(result.Failures);
        Assert.IsType<FileError.IntoItself>(result.Failures[0].Error);
        Assert.False(Directory.Exists(S("d/d")));
    }

    [Fact]
    public async Task 파일과_폴더는_서로_덮어쓰지_않는다()
    {
        Write(S("n"), "file");
        Write(D("n/inner.txt"), "keep");
        var result = await Copy([S("n")], resolver: Always(ConflictChoice.Overwrite));
        Assert.Single(result.Failures);
        Assert.IsType<FileError.DifferentTypes>(result.Failures[0].Error);
        Assert.Equal("keep", File.ReadAllText(D("n/inner.txt")));
    }

    [Fact]
    public async Task 항목_하나가_실패해도_나머지는_계속한다()
    {
        Write(S("ok.txt"), "ok");
        var result = await Copy([S("missing.txt"), S("ok.txt")]);
        Assert.Equal(1, result.Processed);
        Assert.Single(result.Failures);
        Assert.Equal("ok", File.ReadAllText(D("ok.txt")));
    }

    [Fact]
    public async Task 진행률은_전체_바이트에서_끝나고_마지막_값은_total과_같다()
    {
        Write(S("a.bin"), new string('x', 1000));
        Write(S("b.bin"), new string('y', 500));
        var reports = new List<OperationProgress>();
        await _ops.CopyAsync([S("a.bin"), S("b.bin")], _dst, NeverAsk, reports.Add);
        Assert.Equal(1500L, reports[^1].Total);
        Assert.Equal(1500L, reports[^1].Done);
        Assert.All(reports.Zip(reports.Skip(1)), p => Assert.True(p.Second.Done >= p.First.Done));
    }

    [Fact]
    public async Task 복사_중_취소하면_쓰던_불완전한_파일을_지운다()
    {
        Write(S("big.bin"), new string('z', 100_000));
        using var cts = new CancellationTokenSource();
        // 파일 복사가 시작되는 첫 보고에서 취소한다. 쓰기 전에 취소를 알아채야 한다.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _ops.CopyAsync([S("big.bin")], _dst, NeverAsk, p => { if (p.Current == "big.bin") cts.Cancel(); }, cts.Token));
        Assert.False(File.Exists(D("big.bin")));
    }

    // --- 이동 ---

    [Fact]
    public async Task 이동하면_원본이_사라진다()
    {
        Write(S("a.txt"), "A");
        Write(S("tree/b.txt"), "B");
        var result = await Move([S("a.txt"), S("tree")]);
        Assert.Equal(2, result.Processed);
        Assert.False(File.Exists(S("a.txt")));
        Assert.False(Directory.Exists(S("tree")));
        Assert.Equal("A", File.ReadAllText(D("a.txt")));
        Assert.Equal("B", File.ReadAllText(D("tree/b.txt")));
    }

    [Fact]
    public async Task 같은_폴더로의_이동은_아무_일도_하지_않는다()
    {
        Write(S("a.txt"), "A");
        var result = await Move([S("a.txt")], to: _src);
        Assert.Equal(0, result.Processed);
        Assert.Empty(result.Failures);
        Assert.Equal("A", File.ReadAllText(S("a.txt")));
    }

    [Fact]
    public async Task 이동에서_건너뛰면_원본을_남긴다()
    {
        Write(S("a.txt"), "new");
        Write(D("a.txt"), "old");
        var result = await Move([S("a.txt")], resolver: Always(ConflictChoice.Skip));
        Assert.Equal(0, result.Processed);
        Assert.Equal("new", File.ReadAllText(S("a.txt")));
        Assert.Equal("old", File.ReadAllText(D("a.txt")));
    }

    [Fact]
    public async Task 이동에서_덮어쓰면_원본이_사라지고_대상이_바뀐다()
    {
        Write(S("a.txt"), "new");
        Write(D("a.txt"), "old");
        await Move([S("a.txt")], resolver: Always(ConflictChoice.Overwrite));
        Assert.False(File.Exists(S("a.txt")));
        Assert.Equal("new", File.ReadAllText(D("a.txt")));
    }

    [Fact]
    public async Task 폴더를_합치며_이동하면_원본_폴더가_사라진다()
    {
        Write(S("d/x.txt"), "X");
        Write(D("d/y.txt"), "Y");
        await Move([S("d")]);
        Assert.False(Directory.Exists(S("d")));
        Assert.Equal("X", File.ReadAllText(D("d/x.txt")));
        Assert.Equal("Y", File.ReadAllText(D("d/y.txt")));
    }

    [Fact]
    public async Task 폴더를_자기_자신_안으로_이동하면_실패로_기록한다()
    {
        Write(S("d/x.txt"), "X");
        var result = await Move([S("d")], to: S("d"));
        Assert.Single(result.Failures);
        Assert.True(File.Exists(S("d/x.txt")));
    }

    // --- 삭제 ---

    [Fact]
    public async Task 영구_삭제는_폴더_트리까지_지운다()
    {
        Write(S("a.txt"), "A");
        Write(S("tree/deep/b.txt"), "B");
        var result = await _ops.DeleteAsync([S("a.txt"), S("tree")], toTrash: false);
        Assert.Equal(2, result.Processed);
        Assert.False(File.Exists(S("a.txt")));
        Assert.False(Directory.Exists(S("tree")));
        Assert.Empty(_trash.Trashed);
    }

    [Fact]
    public async Task 휴지통_삭제는_휴지통_함수에_맡기고_원본을_직접_지우지_않는다()
    {
        Write(S("a.txt"), "A");
        var result = await _ops.DeleteAsync([S("a.txt")], toTrash: true);
        Assert.Equal(1, result.Processed);
        Assert.Equal([S("a.txt")], _trash.Trashed);
        Assert.True(File.Exists(S("a.txt"))); // 가짜 휴지통이라 그대로
    }

    [Fact]
    public async Task 읽기_전용_파일도_지운다()
    {
        var file = Write(S("ro.txt"), "R");
        File.SetAttributes(file, FileAttributes.ReadOnly);
        var result = await _ops.DeleteAsync([file], toTrash: false);
        Assert.Equal(1, result.Processed);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task 삭제_실패는_모아서_돌려주고_나머지는_계속한다()
    {
        Write(S("a.txt"), "A");
        var failing = new FileOperations(new FakeTrash(p =>
        {
            if (Path.GetFileName(p) == "bad") throw new IOException("싫음");
        }));
        var result = await failing.DeleteAsync([S("bad"), S("a.txt")], toTrash: true);
        Assert.Equal(1, result.Processed);
        Assert.Single(result.Failures);
        Assert.Equal(new FileError.Other("싫음"), result.Failures[0].Error);
    }

    [Fact]
    public async Task 삭제도_취소를_존중한다()
    {
        Write(S("a.txt"), "A");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _ops.DeleteAsync([S("a.txt")], toTrash: false, ct: cts.Token));
        Assert.True(File.Exists(S("a.txt")));
    }

    // --- 이름 ---

    [Fact]
    public async Task 이름_바꾸기()
    {
        Write(S("a.txt"), "A");
        var renamed = await _ops.RenameAsync(S("a.txt"), "b.txt");
        Assert.Equal(S("b.txt"), renamed);
        Assert.False(File.Exists(S("a.txt")));
        Assert.Equal("A", File.ReadAllText(S("b.txt")));
    }

    [Fact]
    public async Task 폴더_이름도_바꾼다()
    {
        Write(S("old/x.txt"), "X");
        await _ops.RenameAsync(S("old"), "new");
        Assert.Equal("X", File.ReadAllText(S("new/x.txt")));
    }

    [Fact]
    public async Task 이미_있는_이름으로_바꾸면_거부하고_둘_다_그대로다()
    {
        Write(S("a.txt"), "A");
        Write(S("b.txt"), "B");
        var e = await Assert.ThrowsAsync<FileOperationException>(() => _ops.RenameAsync(S("a.txt"), "b.txt"));
        Assert.Equal(new FileError.AlreadyExists("b.txt"), e.Error);
        Assert.Equal("A", File.ReadAllText(S("a.txt")));
        Assert.Equal("B", File.ReadAllText(S("b.txt")));
    }

    [Fact]
    public async Task 대소문자만_바꾸는_이름_변경은_허용한다()
    {
        Write(S("a.txt"), "A");
        await _ops.RenameAsync(S("a.txt"), "A.txt");
        Assert.Contains("A.txt", Directory.EnumerateFiles(_src).Select(Path.GetFileName));
    }

    [Fact]
    public async Task 같은_이름으로_바꾸면_아무_일도_없다()
    {
        Write(S("a.txt"), "A");
        Assert.Equal(S("a.txt"), await _ops.RenameAsync(S("a.txt"), "a.txt"));
    }

    [Fact]
    public async Task 쓸_수_없는_이름은_거부한다()
    {
        Write(S("a.txt"), "A");
        foreach (var bad in new[] { "", "   ", ".", "..", "x/y", "x\\y", "a:b", "a*b", "a?b", "a\"b", "a<b", "a|b", "a\0b" })
            await Assert.ThrowsAsync<FileOperationException>(() => _ops.RenameAsync(S("a.txt"), bad));
        Assert.True(File.Exists(S("a.txt")));
    }

    [Fact]
    public async Task 쓸_수_없는_이름의_오류_종류()
    {
        Assert.Equal(new FileError.NameEmpty(), (await Assert.ThrowsAsync<FileOperationException>(() => _ops.CreateFileAsync(_src, " "))).Error);
        Assert.Equal(new FileError.NameReserved(".."), (await Assert.ThrowsAsync<FileOperationException>(() => _ops.CreateFileAsync(_src, ".."))).Error);
        Assert.Equal(new FileError.NameInvalidChar('*'), (await Assert.ThrowsAsync<FileOperationException>(() => _ops.CreateFileAsync(_src, "a*b"))).Error);
    }

    [Fact]
    public async Task 새_폴더와_새_파일()
    {
        var dir = await _ops.CreateDirectoryAsync(_src, "새 폴더");
        var file = await _ops.CreateFileAsync(_src, "note.txt");
        Assert.True(Directory.Exists(dir));
        Assert.True(File.Exists(file));
        Assert.Equal(0L, new FileInfo(file).Length);
        await Assert.ThrowsAsync<FileOperationException>(() => _ops.CreateDirectoryAsync(_src, "새 폴더"));
        await Assert.ThrowsAsync<FileOperationException>(() => _ops.CreateFileAsync(_src, "note.txt"));
        await Assert.ThrowsAsync<FileOperationException>(() => _ops.CreateDirectoryAsync(_src, "a/b"));
    }

    [Fact]
    public void 겹치지_않는_이름_만들기()
    {
        Write(S("a.txt"), "");
        Write(S("a (1).txt"), "");
        Write(S(".hidden"), "");
        Directory.CreateDirectory(S("dir"));
        Assert.Equal(S("a (2).txt"), FileOperations.UniqueName(S("a.txt")));
        Assert.Equal(S(".hidden (1)"), FileOperations.UniqueName(S(".hidden")));
        Assert.Equal(S("dir (1)"), FileOperations.UniqueName(S("dir")));
    }

    [Fact]
    public void 오류_변환()
    {
        Assert.Equal(new FileError.NotFound("x"), FileErrors.From(new FileNotFoundException(), Path.Combine(_src, "x")));
        Assert.Equal(new FileError.AccessDenied("x"), FileErrors.From(new UnauthorizedAccessException(), Path.Combine(_src, "x")));
        Assert.Equal(new FileError.Other("boom"), FileErrors.From(new IOException("boom")));
        Assert.Equal(new FileError.RootNotAllowed(), FileErrors.From(new FileOperationException(new FileError.RootNotAllowed())));
    }
}
