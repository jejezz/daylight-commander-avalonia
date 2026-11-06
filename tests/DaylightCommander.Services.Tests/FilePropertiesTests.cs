using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public sealed class FilePropertiesTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("dc-prop-test").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    string Write(string rel, int size)
    {
        var path = Path.Combine(_dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    [Fact]
    public void 파일_속성을_읽는다()
    {
        var path = Write("a.txt", 123);
        var p = FileProperties.Read(path);
        Assert.Equal("a.txt", p.Name);
        Assert.Equal(_dir, p.Parent);
        Assert.False(p.IsDirectory);
        Assert.Equal(123, p.FileSize);
        Assert.NotNull(p.Modified);
        Assert.Null(p.LinkTarget);
    }

    [Fact]
    public void 폴더_속성은_크기가_비어_있다()
    {
        var p = FileProperties.Read(_dir);
        Assert.True(p.IsDirectory);
        Assert.Null(p.FileSize);
    }

    [Fact]
    public void 폴더_끝의_구분자가_있어도_이름을_찾는다()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "sub"));
        var p = FileProperties.Read(Path.Combine(_dir, "sub") + Path.DirectorySeparatorChar);
        Assert.Equal("sub", p.Name);
        Assert.Equal(_dir, p.Parent);
    }

    [Fact]
    public void 없는_경로는_FileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => FileProperties.Read(Path.Combine(_dir, "missing")));
    }

    [Fact]
    public async Task 폴더_합계는_안쪽까지_센다()
    {
        Write("top.bin", 10);
        Write("d1/a.bin", 100);
        Write("d1/d2/b.bin", 1000);
        Directory.CreateDirectory(Path.Combine(_dir, "empty"));

        var t = await FileProperties.MeasureAsync([_dir]);
        Assert.Equal(1110, t.Bytes);
        Assert.Equal(3, t.Files);
        Assert.Equal(3, t.Folders); // d1, d2, empty (전달한 폴더 자신은 세지 않는다)
        Assert.True(t.Complete);
    }

    [Fact]
    public async Task 여러_항목의_합계는_더해진다()
    {
        var a = Write("a.bin", 5);
        var b = Write("b.bin", 7);
        var d = Path.Combine(_dir, "d");
        Write("d/x.bin", 20);

        var t = await FileProperties.MeasureAsync([a, b, d]);
        Assert.Equal(32, t.Bytes);
        Assert.Equal(3, t.Files);
        Assert.Equal(0, t.Folders);
    }

    [Fact]
    public async Task 없는_항목은_합계에서_빠지고_불완전으로_표시된다()
    {
        var a = Write("a.bin", 5);
        var t = await FileProperties.MeasureAsync([a, Path.Combine(_dir, "gone.bin")]);
        Assert.Equal(5, t.Bytes);
        Assert.Equal(1, t.Files);
        Assert.False(t.Complete);
    }

    [Fact]
    public async Task 취소하면_OperationCanceledException()
    {
        Write("a.bin", 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileProperties.MeasureAsync([_dir], ct: cts.Token));
    }

    [Fact]
    public async Task 마지막에_최종_합계를_알린다()
    {
        Write("a.bin", 9);
        PropertiesTotals? last = null;
        var t = await FileProperties.MeasureAsync([_dir], new Progress<PropertiesTotals>(p => last = p));
        await Task.Delay(50); // Progress는 비동기로 전달된다
        Assert.Equal(t, last);
    }
}
