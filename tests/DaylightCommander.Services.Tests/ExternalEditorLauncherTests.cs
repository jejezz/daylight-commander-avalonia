using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public sealed class ExternalEditorLauncherTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("dc-editor-test").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    string Fake(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void 명령줄은_실행_파일과_파일_경로_하나다()
    {
        var exe = Fake("editor.exe");
        var launcher = new ExternalEditorLauncher(isMac: false);
        Assert.Equal([exe, "C:\\a b\\파일 (1).txt"], launcher.CommandFor(exe, "C:\\a b\\파일 (1).txt"));
    }

    [Fact]
    public void 맥의_app_꾸러미는_open_a로_연다()
    {
        var app = Path.Combine(_dir, "Editor.app");
        Directory.CreateDirectory(app);
        Assert.Equal(["open", "-a", app, "/x/y.txt"], new ExternalEditorLauncher(isMac: true).CommandFor(app, "/x/y.txt"));
        // 맥이 아니면 .app이어도 그냥 실행 파일로 취급한다
        Assert.Equal([app, "/x/y.txt"], new ExternalEditorLauncher(isMac: false).CommandFor(app, "/x/y.txt"));
    }

    [Fact]
    public void 실행_파일이_없으면_EditorNotFound()
    {
        var e = Assert.Throws<FileOperationException>(() =>
            new ExternalEditorLauncher(isMac: false).Open(Path.Combine(_dir, "nope.exe"), "x.txt"));
        Assert.Equal(new FileError.EditorNotFound(Path.Combine(_dir, "nope.exe")), e.Error);
    }

    [Fact]
    public void 프로세스_시작에_실패하면_EditorLaunchFailed()
    {
        var exe = Fake("editor.exe");
        var launcher = new ExternalEditorLauncher(isMac: false, start: _ => throw new IOException("못 띄움"));
        var e = Assert.Throws<FileOperationException>(() => launcher.Open(exe, "x.txt"));
        Assert.Equal(new FileError.EditorLaunchFailed("못 띄움"), e.Error);
    }

    [Fact]
    public void 열면_명령줄이_그대로_시작_함수에_간다()
    {
        var exe = Fake("editor.exe");
        IReadOnlyList<string>? started = null;
        new ExternalEditorLauncher(isMac: false, start: c => started = c).Open(exe, "C:\\f.txt");
        Assert.Equal([exe, "C:\\f.txt"], started);
    }
}
