using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public sealed class ListLocalDirectoryTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("dc-list-test").FullName;
    readonly ListLocalDirectory _list = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    string P(string name) => Path.Combine(_dir, name);

    async Task<Dictionary<string, Domain.FileEntry>> ByNameAsync() =>
        (await _list.InvokeAsync(_dir)).ToDictionary(e => e.Name);

    [Fact]
    public async Task 폴더가_먼저_오고_이름은_대소문자를_무시하고_정렬한다()
    {
        File.WriteAllText(P("b.txt"), "");
        File.WriteAllText(P("A.txt"), "");
        File.WriteAllText(P("c.txt"), "");
        Directory.CreateDirectory(P("Zdir"));
        Directory.CreateDirectory(P("adir"));

        var names = (await _list.InvokeAsync(_dir)).Select(e => e.Name);
        Assert.Equal(["adir", "Zdir", "A.txt", "b.txt", "c.txt"], names);
    }

    [Fact]
    public async Task 파일은_크기를_갖고_폴더는_크기가_없다()
    {
        File.WriteAllText(P("data.bin"), "12345");
        Directory.CreateDirectory(P("sub"));

        var byName = await ByNameAsync();
        Assert.Equal(5L, byName["data.bin"].SizeBytes);
        Assert.False(byName["data.bin"].IsDirectory);
        Assert.Null(byName["sub"].SizeBytes);
        Assert.True(byName["sub"].IsDirectory);
    }

    [Fact]
    public async Task 점으로_시작하는_이름은_숨김이고_경로는_절대_경로다()
    {
        File.WriteAllText(P(".hidden"), "");
        File.WriteAllText(P("shown"), "");

        var byName = await ByNameAsync();
        Assert.True(byName[".hidden"].IsHidden);
        Assert.False(byName["shown"].IsHidden);
        Assert.Equal(P("shown"), byName["shown"].Path);
        Assert.NotNull(byName["shown"].ModifiedAt);
    }

    [Fact]
    public async Task Windows의_숨김과_시스템_속성_폴더도_숨김이다()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(P("hid"));
        Directory.CreateDirectory(P("sys"));
        Directory.CreateDirectory(P("plain"));
        File.SetAttributes(P("hid"), FileAttributes.Hidden | FileAttributes.Directory);
        File.SetAttributes(P("sys"), FileAttributes.System | FileAttributes.Directory);

        var byName = await ByNameAsync();
        Assert.True(byName["hid"].IsHidden);
        Assert.True(byName["sys"].IsHidden);
        Assert.False(byName["plain"].IsHidden);
    }

    [Fact]
    public async Task 빈_폴더는_빈_목록() => Assert.Empty(await _list.InvokeAsync(_dir));

    [Fact]
    public async Task 없는_폴더는_DirectoryNotFoundException() =>
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _list.InvokeAsync(P("missing")));

    [Fact]
    public async Task 깨진_심볼릭_링크는_건너뛰고_폴더_링크는_폴더로_본다()
    {
        Directory.CreateDirectory(P("real"));
        File.WriteAllText(P("file.txt"), "");
        try
        {
            Directory.CreateSymbolicLink(P("dirlink"), P("real"));
            File.CreateSymbolicLink(P("broken"), P("nowhere"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return; // 권한이 없는 환경(Windows 일반 사용자 등)에서는 심볼릭 링크를 만들 수 없다
        }

        var byName = await ByNameAsync();
        Assert.DoesNotContain("broken", byName.Keys);
        Assert.True(byName["dirlink"].IsDirectory);
    }

    [Fact]
    public async Task 취소하면_OperationCanceledException()
    {
        for (var i = 0; i < 20; i++) File.WriteAllText(P($"f{i}"), "");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _list.InvokeAsync(_dir, cts.Token));
    }
}
