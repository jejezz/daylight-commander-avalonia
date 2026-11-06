using System.ComponentModel;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>터미널 열기: OS별 후보 명령줄, 되는 것이 나올 때까지 시도, 모두 실패하면 오류.</summary>
public sealed class TerminalTests
{
    [Fact]
    public void Windows는_Windows_Terminal을_먼저_시도하고_cmd로_물러난다()
    {
        var c = new TerminalLauncher(isWindows: true, isMac: false).CandidatesFor(@"C:\My Dir");
        Assert.Equal(["wt.exe", "-d", @"C:\My Dir"], c[0]);
        Assert.Equal("cmd.exe", c[1][0]);
        Assert.Contains("/K", c[1]);
    }

    [Fact]
    public void macOS는_Terminal_앱을_연다()
    {
        var c = new TerminalLauncher(isWindows: false, isMac: true).CandidatesFor("/a b");
        Assert.Equal([["open", "-a", "Terminal", "/a b"]], c);
    }

    [Fact]
    public void Linux는_터미널_프로그램들을_차례로_시도한다()
    {
        var c = new TerminalLauncher(isWindows: false, isMac: false).CandidatesFor("/home/me");
        Assert.Equal(["gnome-terminal", "konsole", "xfce4-terminal", "xterm"], c.Select(x => x[0]));
        Assert.Equal("--working-directory=/home/me", c[0][1]);
    }

    [Fact]
    public void 첫_후보가_실패하면_다음을_시도하고_되는_것에서_멈춘다()
    {
        var tried = new List<string>();
        var launcher = new TerminalLauncher(isWindows: true, isMac: false, start: cmd =>
        {
            tried.Add(cmd[0]);
            if (cmd[0] == "wt.exe") throw new Win32Exception("not found");
        });
        launcher.Open(@"C:\x");
        Assert.Equal(["wt.exe", "cmd.exe"], tried);
    }

    [Fact]
    public void 되는_후보가_없으면_터미널을_찾지_못했다는_오류다()
    {
        var launcher = new TerminalLauncher(isWindows: false, isMac: false, start: _ => throw new Win32Exception("none"));
        var e = Assert.Throws<FileOperationException>(() => launcher.Open("/x"));
        Assert.Equal(new FileError.TerminalNotFound(), e.Error);
    }

    // --- HomeState ---

    sealed class RecordingTerminal(Action<string>? onOpen = null) : ITerminalLauncher
    {
        public List<string> Opened { get; } = [];

        public void Open(string directory)
        {
            onOpen?.Invoke(directory);
            Opened.Add(directory);
        }
    }

    [Fact]
    public async Task 활성_패널의_폴더에서_열고_실패하면_그_패널에_오류가_뜬다()
    {
        var root = Directory.CreateTempSubdirectory("dc-terminal-test").FullName;
        try
        {
            var left = Directory.CreateDirectory(Path.Combine(root, "l")).FullName;
            var right = Directory.CreateDirectory(Path.Combine(root, "r")).FullName;
            var lister = new ListLocalDirectory();

            var ok = new RecordingTerminal();
            using (var home = new HomeState(left, right, (p, ct) => lister.InvokeAsync(p, ct), terminal: ok))
            {
                await home.Left.StartAsync();
                await home.Right.StartAsync();
                home.OpenTerminal(PaneSide.Right);
                Assert.Equal([right], ok.Opened);
            }

            var failing = new RecordingTerminal(_ => throw new FileOperationException(new FileError.TerminalNotFound()));
            using (var home = new HomeState(left, right, (p, ct) => lister.InvokeAsync(p, ct), terminal: failing))
            {
                await home.Left.StartAsync();
                await home.Right.StartAsync();
                home.OpenTerminal(PaneSide.Left);
                Assert.Equal(new FileError.TerminalNotFound(), home.Left.State.Error);
                Assert.Null(home.Right.State.Error);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
