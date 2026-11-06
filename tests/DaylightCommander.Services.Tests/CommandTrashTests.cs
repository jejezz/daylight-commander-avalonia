using System.ComponentModel;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>macOS·Linux 휴지통: 부를 명령줄과, 실패했을 때 다음 후보로 넘어가는 규칙.</summary>
public sealed class CommandTrashTests
{
    [Fact]
    public void macOS는_Finder에_시키는_osascript를_쓰고_따옴표와_역슬래시를_이스케이프한다()
    {
        var c = Assert.Single(Trash.MacCommands("/Users/me/a \"quoted\" \\ file.txt"));
        Assert.Equal("osascript", c[0]);
        Assert.Equal("-e", c[1]);
        Assert.Equal("tell application \"Finder\" to delete POSIX file \"/Users/me/a \\\"quoted\\\" \\\\ file.txt\"", c[2]);
    }

    [Fact]
    public void Linux는_gio를_먼저_쓰고_trash_put으로_물러난다_경로가_옵션으로_읽히지_않게_한다()
    {
        var c = Trash.LinuxCommands("/home/me/-rf");
        Assert.Equal(["gio", "trash", "--", "/home/me/-rf"], c[0]);
        Assert.Equal(["trash-put", "--", "/home/me/-rf"], c[1]);
    }

    static string FullOf(string p) => Path.GetFullPath(p);

    [Fact]
    public void 첫_명령이_성공하면_거기서_멈춘다()
    {
        var tried = new List<string>();
        var trash = new CommandTrash(p => [["a", p], ["b", p]], () => true, cmd => { tried.Add(cmd[0]); return (0, ""); });
        trash.MoveToTrash("x.txt");
        Assert.Equal(["a"], tried);
    }

    [Fact]
    public void 명령이_없거나_실패하면_다음_후보를_시도한다()
    {
        var tried = new List<string>();
        var trash = new CommandTrash(p => [["missing", p], ["fails", p], ["works", p]], () => true, cmd =>
        {
            tried.Add(cmd[0]);
            return cmd[0] switch
            {
                "missing" => throw new Win32Exception("not found"),
                "fails" => (1, "boom"),
                _ => (0, ""),
            };
        });
        trash.MoveToTrash("x.txt");
        Assert.Equal(["missing", "fails", "works"], tried);
    }

    [Fact]
    public void 모두_실패하면_마지막_오류와_함께_휴지통_실패다()
    {
        var trash = new CommandTrash(p => [["a", p], ["b", p]], () => true, cmd => cmd[0] == "a" ? (1, "first") : (2, "  second error \n"));
        var e = Assert.Throws<FileOperationException>(() => trash.MoveToTrash("dir/file.txt"));
        Assert.Equal(new FileError.TrashFailed("file.txt", "second error"), e.Error);
    }

    [Fact]
    public void 명령에는_전체_경로를_넘기고_지원_여부는_주어진_판단을_따른다()
    {
        string? seen = null;
        var trash = new CommandTrash(p => { seen = p; return [["a", p]]; }, () => false, _ => (0, ""));
        Assert.False(trash.IsSupported);
        trash.MoveToTrash("relative.txt");
        Assert.Equal(FullOf("relative.txt"), seen);
    }

    [Fact]
    public void 이_OS의_기본_휴지통은_지원_여부를_알려_준다()
    {
        // Windows는 항상 지원한다. 다른 OS는 도구가 있느냐에 달렸으니 예외 없이 값만 나오면 된다.
        var supported = Trash.Default.IsSupported;
        if (OperatingSystem.IsWindows()) Assert.True(supported);
    }
}
