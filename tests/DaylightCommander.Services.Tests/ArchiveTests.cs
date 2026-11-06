using System.IO.Compression;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>압축(zip 만들기)·압축 풀기와 HomeState의 압축 흐름.</summary>
public sealed class ArchiveTests : IAsyncLifetime
{
    readonly string _root = Directory.CreateTempSubdirectory("dc-archive-test").FullName;
    readonly string _left;
    readonly string _right;
    readonly ListLocalDirectory _lister = new();
    readonly ArchiveService _archive = new();
    HomeState _home = null!;

    public ArchiveTests()
    {
        _left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
        _right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
        File.WriteAllText(Path.Combine(_left, "a.txt"), "A");
        Directory.CreateDirectory(Path.Combine(_left, "dir", "sub"));
        File.WriteAllText(Path.Combine(_left, "dir", "inner.txt"), "I");
        Directory.CreateDirectory(Path.Combine(_left, "empty"));
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

    string L(string name) => Path.Combine(_left, name);

    static string[] Names(string zip)
    {
        using var z = ZipFile.OpenRead(zip);
        return z.Entries.Select(e => e.FullName).Order().ToArray();
    }

    [Fact]
    public void 기본_이름과_풀_폴더_이름()
    {
        Assert.Equal("a.txt.zip", ArchiveService.DefaultZipName([L("a.txt")]));
        Assert.Equal("dir.zip", ArchiveService.DefaultZipName([L("dir") + Path.DirectorySeparatorChar]));
        Assert.Equal("archive.zip", ArchiveService.DefaultZipName([L("a.txt"), L("dir")]));
        Assert.Equal("photos", ArchiveService.ExtractFolderName("photos.ZIP"));
        Assert.True(ArchiveService.IsZip("X.Zip"));
        Assert.False(ArchiveService.IsZip("x.tar"));
    }

    [Fact]
    public async Task 파일과_폴더를_묶으면_폴더_이름부터_담긴다()
    {
        var zip = Path.Combine(_right, "out.zip");
        await _archive.CompressAsync([L("a.txt"), L("dir"), L("empty")], zip);
        Assert.Equal(["a.txt", "dir/inner.txt", "dir/sub/", "empty/"], Names(zip));
    }

    [Fact]
    public async Task 압축했다_풀면_내용이_같다()
    {
        var zip = Path.Combine(_right, "out.zip");
        await _archive.CompressAsync([L("a.txt"), L("dir")], zip);
        var dest = await _archive.ExtractAsync(zip, Path.Combine(_right, "out"));
        Assert.Equal("A", File.ReadAllText(Path.Combine(dest, "a.txt")));
        Assert.Equal("I", File.ReadAllText(Path.Combine(dest, "dir", "inner.txt")));
        Assert.True(Directory.Exists(Path.Combine(dest, "dir", "sub")));
    }

    [Fact]
    public async Task 같은_폴더에_만드는_zip_자신은_담지_않는다()
    {
        var zip = L("self.zip");
        await _archive.CompressAsync([L("a.txt"), L("dir")], zip);
        Assert.DoesNotContain("self.zip", Names(zip));
    }

    [Fact]
    public async Task 이미_있는_이름이면_거부하고_기존_파일을_건드리지_않는다()
    {
        File.WriteAllText(L("keep.zip"), "x");
        var e = await Assert.ThrowsAsync<FileOperationException>(() => _archive.CompressAsync([L("a.txt")], L("keep.zip")));
        Assert.Equal(new FileError.AlreadyExists("keep.zip"), e.Error);
        Assert.Equal("x", File.ReadAllText(L("keep.zip")));
    }

    [Fact]
    public async Task 경로를_벗어나는_항목은_풀지_않는다()
    {
        var zip = Path.Combine(_right, "evil.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using var w = new StreamWriter(z.CreateEntry("../escaped.txt").Open());
            w.Write("bad");
        }
        await Assert.ThrowsAsync<FileOperationException>(() => _archive.ExtractAsync(zip, Path.Combine(_right, "evil")));
        Assert.False(File.Exists(Path.Combine(_right, "escaped.txt")));
    }

    [Fact]
    public async Task 이미_있는_파일은_덮어쓰지_않고_멈춘다()
    {
        var zip = Path.Combine(_right, "out.zip");
        await _archive.CompressAsync([L("a.txt")], zip);
        var dest = Path.Combine(_right, "out");
        await _archive.ExtractAsync(zip, dest);
        File.WriteAllText(Path.Combine(dest, "a.txt"), "changed");
        await Assert.ThrowsAsync<FileOperationException>(() => _archive.ExtractAsync(zip, dest));
        Assert.Equal("changed", File.ReadAllText(Path.Combine(dest, "a.txt")));
    }

    // --- HomeState 흐름 ---

    [Fact]
    public async Task 압축_대화상자는_기본_이름을_제안하고_확인하면_zip을_만든다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        _home.RequestCompress();
        var d = Assert.IsType<HomeDialog.NameInput>(_home.Dialog);
        Assert.Equal(NameMode.Compress, d.Mode);
        Assert.Equal("a.txt.zip", d.InitialText);

        await _home.ConfirmNameAsync("mine"); // .zip은 자동으로 붙는다
        Assert.Null(_home.Dialog);
        Assert.Equal(["a.txt"], Names(L("mine.zip")));
        Assert.Contains(_home.Left.State.Entries, e => e.Name == "mine.zip");
    }

    [Fact]
    public async Task 이미_있는_zip_이름이면_대화상자에_오류를_남긴다()
    {
        File.WriteAllText(L("dup.zip"), "x");
        _home.Left.ClickEntry(L("a.txt"));
        _home.RequestCompress();
        await _home.ConfirmNameAsync("dup.zip");
        var d = Assert.IsType<HomeDialog.NameInput>(_home.Dialog);
        Assert.Equal(new FileError.AlreadyExists("dup.zip"), d.Error);
    }

    [Fact]
    public async Task 압축풀기는_zip_이름의_폴더에_푼다()
    {
        await _archive.CompressAsync([L("a.txt")], L("pack.zip"));
        await _home.Left.RefreshAsync();
        _home.Left.ClickEntry(L("pack.zip"));
        await _home.ExtractAsync();
        Assert.Equal("A", File.ReadAllText(L("pack/a.txt")));
        Assert.Null(_home.Left.State.Error);
    }

    [Fact]
    public async Task 깨진_zip을_풀면_패널에_오류가_뜬다()
    {
        File.WriteAllText(L("broken.zip"), "not a zip");
        await _home.Left.RefreshAsync();
        _home.Left.ClickEntry(L("broken.zip"));
        await _home.ExtractAsync();
        Assert.NotNull(_home.Left.State.Error);
    }

    [Fact]
    public async Task zip이_아닌_파일에서는_풀기가_아무것도_하지_않는다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        await _home.ExtractAsync();
        Assert.False(Directory.Exists(L("a")));
    }
}
