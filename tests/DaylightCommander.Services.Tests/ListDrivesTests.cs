using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public class ListDrivesTests
{
    [Fact]
    public void 이_컴퓨터의_드라이브_목록은_비어_있지_않고_이름과_경로가_있다()
    {
        var drives = new ListDrives().Invoke();
        Assert.NotEmpty(drives);
        Assert.All(drives, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Name));
            Assert.False(string.IsNullOrWhiteSpace(d.Path));
        });
    }

    [Fact]
    public void 현재_폴더가_속한_드라이브를_찾는다()
    {
        var drives = new ListDrives().Invoke();
        var here = Directory.GetCurrentDirectory();
        Assert.NotNull(ListDrives.DriveOf(here, drives));
    }

    [Fact]
    public void 가장_길게_일치하는_마운트를_드라이브로_본다()
    {
        if (OperatingSystem.IsWindows()) return; // Windows는 경로의 루트로 정한다
        DriveEntry[] drives = [new("/", "/"), new("USB", "/Volumes/USB")];
        Assert.Equal("USB", ListDrives.DriveOf("/Volumes/USB/a/b", drives)!.Name);
        Assert.Equal("/", ListDrives.DriveOf("/Users/x", drives)!.Name);
        Assert.Equal("/", ListDrives.DriveOf("/Volumes/USB2/a", drives)!.Name); // 이름만 비슷한 다른 폴더
    }

    [Fact]
    public void Windows는_경로의_루트로_드라이브를_고르고_대소문자를_가리지_않는다()
    {
        if (!OperatingSystem.IsWindows()) return;
        DriveEntry[] drives = [new("C:\\", "C:\\"), new("D:\\", "D:\\")];
        Assert.Equal("D:\\", ListDrives.DriveOf(@"d:\games\x", drives)!.Path);
        Assert.Null(ListDrives.DriveOf(@"E:\x", drives));
    }
}
