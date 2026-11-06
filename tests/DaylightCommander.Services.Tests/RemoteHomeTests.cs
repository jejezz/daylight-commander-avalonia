using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>원격 서버가 낀 홈 화면 동작: 연결 대화상자, 패널 이동, 끌어다 놓기 규칙, 아직 안 되는 기능 안내.</summary>
public sealed class RemoteHomeTests : IAsyncLifetime
{
    const string Srv = "sftp://me@alpha";

    readonly string _root = Directory.CreateTempSubdirectory("dc-remote-home").FullName;
    readonly string _left;
    readonly string _right;
    readonly ListLocalDirectory _lister = new();
    readonly MemoryFileSystem _fs = new(Srv);
    bool _failAuth;
    RemoteSessions _sessions = null!;
    HomeState _home = null!;

    public RemoteHomeTests()
    {
        _left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
        _right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
        File.WriteAllText(Path.Combine(_left, "local.txt"), "L");
        _fs.File("/home/me/note.txt", "remote note").File("/home/me/pics/a.png", "png").File("/readme.md", "# hi");
    }

    public async Task InitializeAsync()
    {
        _sessions = new RemoteSessions(connector: (req, ct) =>
            _failAuth ? throw new FileOperationException(new FileError.RemoteAuthFailed()) : Task.FromResult<IFileSystem>(_fs));
        _home = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), remote: _sessions);
        await _home.Left.StartAsync();
        await _home.Right.StartAsync();
    }

    public Task DisposeAsync()
    {
        _home.Dispose();
        Directory.Delete(_root, recursive: true);
        return Task.CompletedTask;
    }

    async Task ConnectAndGoAsync(string path = Srv + "/home/me")
    {
        _home.RequestConnect();
        await _home.ConfirmConnectAsync("alpha", 22, "me", "pw", save: false);
        await _home.Left.NavigateToAsync(path);
    }

    static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    // --- 연결 대화상자 ---

    [Fact]
    public async Task 연결하면_대화상자가_닫히고_활성_패널이_서버로_간다()
    {
        _home.RequestConnect();
        var dialog = Assert.IsType<HomeDialog.Connect>(_home.Dialog);
        Assert.Equal(PaneSide.Left, dialog.Side);

        await _home.ConfirmConnectAsync("alpha", 22, "me", "pw", save: false);

        Assert.Null(_home.Dialog);
        Assert.Equal(Srv + "/", _home.Left.State.CurrentPath);
        Assert.Contains(_home.Left.State.Entries, e => e.Name == "home" && e.IsDirectory);
        Assert.True(_home.Remote.IsConnected(Srv + "/"));
    }

    [Fact]
    public async Task 연결에_실패하면_대화상자가_남고_오류와_입력값을_보여_준다()
    {
        _failAuth = true;
        _home.RequestConnect();
        await _home.ConfirmConnectAsync("alpha", 2200, "me", "wrong", save: false);

        var d = Assert.IsType<HomeDialog.Connect>(_home.Dialog);
        Assert.Equal(new FileError.RemoteAuthFailed(), d.Error);
        Assert.False(d.Busy);
        Assert.Equal("alpha", d.Profile!.Host);
        Assert.Equal(2200, d.Profile.Port);
        Assert.Equal(_left, _home.Left.State.CurrentPath);
    }

    [Fact]
    public async Task 호스트나_계정이_비면_연결을_시도하지_않고_오류다()
    {
        _home.RequestConnect();
        await _home.ConfirmConnectAsync("  ", 22, "me", "pw", save: false);
        Assert.IsType<FileError.RemoteConnectFailed>(Assert.IsType<HomeDialog.Connect>(_home.Dialog).Error);
        await _home.ConfirmConnectAsync("alpha", 22, "", "pw", save: false);
        Assert.IsType<FileError.RemoteConnectFailed>(Assert.IsType<HomeDialog.Connect>(_home.Dialog).Error);
        Assert.Empty(_home.Remote.Connected);
    }

    [Fact]
    public async Task 연결을_끊으면_그_서버를_보던_패널은_로컬로_돌아간다()
    {
        await ConnectAndGoAsync();
        Assert.StartsWith("sftp://", _home.Left.State.CurrentPath);
        await _home.DisconnectAsync(_home.Left.State.CurrentPath);
        Assert.False(_home.Remote.IsConnected(Srv));
        Assert.False(Locations.IsRemote(_home.Left.State.CurrentPath));
    }

    // --- 탐색 ---

    [Fact]
    public async Task 서버_안에서_폴더로_들어가고_위로_올라가고_루트에서_멈춘다()
    {
        await ConnectAndGoAsync();
        Assert.Equal(Srv + "/home/me", _home.Left.State.CurrentPath);
        Assert.Equal(["note.txt", "pics"], _home.Left.State.Entries.Select(e => e.Name).Order());

        await _home.Left.NavigateToAsync(Srv + "/home/me/pics");
        Assert.Equal(["a.png"], _home.Left.State.Entries.Select(e => e.Name));

        await _home.Left.GoUpAsync();
        await _home.Left.GoUpAsync();
        await _home.Left.GoUpAsync();
        Assert.Equal(Srv + "/", _home.Left.State.CurrentPath);
        Assert.False(_home.Left.State.CanGoUp);
    }

    [Fact]
    public async Task 주소를_직접_쓰면_다듬어서_이동한다()
    {
        await ConnectAndGoAsync();
        await _home.Left.NavigateToAsync("  sftp://me@alpha/home//me/pics/../pics/  ");
        Assert.Equal(Srv + "/home/me/pics", _home.Left.State.CurrentPath);
    }

    [Fact]
    public async Task 연결하지_않은_서버로_가면_연결_안_됨_오류가_뜨고_제자리다()
    {
        await _home.Left.NavigateToAsync("sftp://me@nowhere/x");
        Assert.Equal(new FileError.RemoteNotConnected("sftp://me@nowhere"), _home.Left.State.Error);
        Assert.Equal(_left, _home.Left.State.CurrentPath);
    }

    [Fact]
    public async Task 없는_원격_폴더로_가면_찾을_수_없다는_오류다()
    {
        await ConnectAndGoAsync();
        await _home.Left.NavigateToAsync(Srv + "/nope");
        Assert.IsType<FileError.NotFound>(_home.Left.State.Error);
    }

    // --- 파일 작업 ---

    [Fact]
    public async Task 원격_폴더로_끌어다_놓으면_올라간다()
    {
        await ConnectAndGoAsync();
        _home.Drop([Path.Combine(_left, "local.txt")], Srv + "/home/me/pics", DropMode.Auto);
        await WaitUntil(() => _home.Queue.Jobs.Count > 0 && _home.Queue.Jobs.All(j => j.IsFinished));
        Assert.Equal("L", _fs.Read("/home/me/pics/local.txt"));
        Assert.True(File.Exists(Path.Combine(_left, "local.txt"))); // 로컬→원격 기본은 복사
    }

    [Fact]
    public void 끌어다_놓기_기본은_같은_서버면_이동_아니면_복사다()
    {
        Assert.Equal(DropAction.Move, DropPlanner.Plan([Srv + "/a.txt"], Srv + "/b", []).Action);
        Assert.Equal(DropAction.Copy, DropPlanner.Plan([Srv + "/a.txt"], _right, []).Action);
        Assert.Equal(DropAction.Copy, DropPlanner.Plan([Path.Combine(_left, "x")], Srv + "/b", []).Action);
        Assert.Equal(DropAction.Copy, DropPlanner.Plan([Srv + "/a.txt"], "sftp://me@other/b", []).Action);
    }

    [Fact]
    public void 같은_폴더로의_이동은_없고_원격_폴더를_자기_안에_놓으면_오류다()
    {
        Assert.Equal(DropAction.None, DropPlanner.Plan([Srv + "/home/a.txt"], Srv + "/home", []).Action);
        var plan = DropPlanner.Plan([Srv + "/home"], Srv + "/home/me", []);
        Assert.Equal(DropAction.Error, plan.Action);
        Assert.Equal(new FileError.IntoItself("home"), plan.Error);
        Assert.Equal(DropAction.Move, DropPlanner.Plan([Srv + "/home"], Srv + "/homework", []).Action); // 이름만 비슷
    }

    [Fact]
    public async Task 원격_삭제_대화상자는_휴지통을_쓰지_않는다()
    {
        await ConnectAndGoAsync();
        _home.Left.ClickEntry(Srv + "/home/me/note.txt");
        _home.RequestDelete();
        Assert.False(Assert.IsType<HomeDialog.Delete>(_home.Dialog).ToTrash);
    }

    [Fact]
    public async Task 복사_대화상자는_연결_안_된_원격_대상을_거부한다()
    {
        await ConnectAndGoAsync();
        _home.Left.ClickEntry(Srv + "/home/me/note.txt");
        _home.RequestTransfer(FileOperationKind.Copy);
        _home.ConfirmTransfer("sftp://me@elsewhere/x");
        Assert.Equal(new FileError.RemoteNotConnected("sftp://me@elsewhere"), Assert.IsType<HomeDialog.Transfer>(_home.Dialog).Error);
    }

    // --- 원격 파일 열기·보기 ---

    [Fact]
    public async Task 원격_파일을_보기는_임시_폴더로_받은_사본을_연다()
    {
        await ConnectAndGoAsync();
        _home.Left.ClickEntry(Srv + "/home/me/note.txt");
        var got = new TaskCompletionSource<string>();
        _home.ViewRequested += p => got.TrySetResult(p);

        _home.RequestView();
        var local = await got.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(Locations.IsRemote(local));
        Assert.Equal("note.txt", Path.GetFileName(local));
        Assert.Equal("remote note", File.ReadAllText(local));
    }

    [Fact]
    public async Task 원격_파일을_기본_앱으로_열면_받은_사본의_경로를_넘긴다()
    {
        var opened = new TaskCompletionSource<string>();
        using var home = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), remote: _sessions,
            opener: new Opener(p => opened.TrySetResult(p)));
        await _sessions.ConnectAsync(new RemoteConnectRequest("sftp", "alpha", 22, "me", "pw"));

        home.OpenFile(Srv + "/readme.md", PaneSide.Left);
        var local = await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("# hi", File.ReadAllText(local));
    }

    sealed class Opener(Action<string> onOpen) : IFileOpener
    {
        public void Open(string path) => onOpen(path);
    }

    [Fact]
    public async Task 받을_수_없는_원격_파일은_그_패널에_오류가_뜬다()
    {
        await ConnectAndGoAsync();
        _home.OpenFile(Srv + "/home/me/ghost.txt", PaneSide.Left);
        await WaitUntil(() => _home.Left.State.Error is not null);
        Assert.IsType<FileError.NotFound>(_home.Left.State.Error);
    }

    // --- 아직 안 되는 기능은 안내한다 ---

    [Fact]
    public async Task 편집_속성_압축_터미널_탐색기는_원격에서_지원하지_않는다는_오류다()
    {
        await ConnectAndGoAsync();
        var unsupported = new FileError.RemoteUnsupported();

        _home.Left.ClickEntry(Srv + "/home/me/note.txt");
        _home.RequestEdit();
        Assert.Equal(unsupported, _home.Left.State.Error);

        _home.Left.ClearError();
        _home.RequestProperties();
        Assert.Equal(unsupported, _home.Left.State.Error);

        _home.Left.ClearError();
        _home.RequestCompress();
        Assert.Equal(unsupported, _home.Left.State.Error);
        Assert.Null(_home.Dialog);

        _home.Left.ClearError();
        _home.OpenTerminal(PaneSide.Left);
        Assert.Equal(unsupported, _home.Left.State.Error);

        _home.Left.ClearError();
        _home.RevealInFileManager(Srv + "/home/me/note.txt", PaneSide.Left);
        Assert.Equal(unsupported, _home.Left.State.Error);
    }

    [Fact]
    public async Task 원격에서_이름_바꾸기와_새_폴더와_삭제가_된다()
    {
        await ConnectAndGoAsync();

        _home.RequestName(NameMode.NewFolder);
        await _home.ConfirmNameAsync("made");
        Assert.True(_fs.Exists("/home/me/made"));

        _home.Left.ClickEntry(Srv + "/home/me/note.txt");
        _home.RequestName(NameMode.Rename);
        await _home.ConfirmNameAsync("renamed.txt");
        Assert.True(_fs.Exists("/home/me/renamed.txt"));
        Assert.False(_fs.Exists("/home/me/note.txt"));

        _home.RequestDeletePaths([Srv + "/home/me/renamed.txt"]);
        _home.ConfirmDelete();
        await WaitUntil(() => !_fs.Exists("/home/me/renamed.txt"));
    }
}
