using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>대화상자 흐름과 파일 작업 연결을 실제 임시 폴더로 확인한다.</summary>
public sealed class HomeStateTests : IAsyncLifetime
{
    sealed class FakeTrash(bool supported) : ITrash
    {
        public bool IsSupported => supported;
        public void MoveToTrash(string path) { }
    }

    readonly string _root = Directory.CreateTempSubdirectory("dc-home-test").FullName;
    readonly string _left;
    readonly string _right;
    readonly ListLocalDirectory _lister = new();
    HomeState _home = null!;

    public HomeStateTests()
    {
        _left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
        _right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
        File.WriteAllText(Path.Combine(_left, "a.txt"), "A");
        File.WriteAllText(Path.Combine(_left, "b.txt"), "B");
        Directory.CreateDirectory(Path.Combine(_left, "sub"));
    }

    HomeState Create(bool canTrash = false) =>
        new(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), new FileOperations(new FakeTrash(canTrash)));

    public async Task InitializeAsync()
    {
        _home = Create();
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
    string R(string name) => Path.Combine(_right, name);

    static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    Task WaitQueueIdle() =>
        WaitUntil(() => _home.Queue.Jobs.Count > 0 && _home.Queue.Jobs.All(j => j.IsFinished));

    [Fact]
    public async Task 폴더를_이동하면_최근_경로에_기록되고_시작_경로는_기록되지_않는다()
    {
        using var settings = new SettingsController(new DaylightCommander.Data.InMemorySettingsRepository(), TimeSpan.FromMinutes(1));
        using var h = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), settings: settings);
        await h.Left.StartAsync();
        await h.Left.NavigateToAsync(L("sub"));
        await h.Right.StartAsync();
        await h.Right.NavigateToAsync(_right);
        await h.Left.GoBackAsync();
        Assert.Equal([_left, L("sub")], settings.Value.RecentPaths);
    }

    [Fact]
    public async Task F4는_커서가_파일이고_외부_편집기가_정해져_있을_때만_연다()
    {
        var exe = Path.Combine(_root, "editor.exe");
        File.WriteAllText(exe, "");
        var opened = new List<IReadOnlyList<string>>();
        var launcher = new ExternalEditorLauncher(isMac: false, start: opened.Add);

        // 내장 모드: 아무것도 하지 않는다
        using var builtin = new SettingsController(new DaylightCommander.Data.InMemorySettingsRepository(), TimeSpan.FromMinutes(1));
        using var h1 = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), settings: builtin, editor: launcher);
        await h1.Left.StartAsync();
        h1.Left.ClickEntry(L("a.txt"));
        h1.RequestEdit();
        await Task.Delay(100);
        Assert.Empty(opened);

        // 외부 편집기를 고르면 커서 파일을 연다. 폴더 위에서는 열지 않는다.
        builtin.Update(s => s with
        {
            EditorMode = DaylightCommander.Domain.Settings.EditorMode.External,
            EditorExecutable = exe,
        });
        h1.Left.ClickEntry(L("sub"));
        h1.RequestEdit();
        await Task.Delay(100);
        Assert.Empty(opened);

        h1.Left.ClickEntry(L("a.txt"));
        h1.RequestEdit();
        await WaitUntil(() => opened.Count == 1);
        Assert.Equal([exe, L("a.txt")], opened[0]);
    }

    [Fact]
    public async Task F4_실행이_실패하면_활성_패널_상태줄에_오류가_뜬다()
    {
        var exe = Path.Combine(_root, "editor.exe");
        File.WriteAllText(exe, "");
        using var settings = new SettingsController(new DaylightCommander.Data.InMemorySettingsRepository(
            new DaylightCommander.Domain.Settings.AppSettings
            {
                EditorMode = DaylightCommander.Domain.Settings.EditorMode.External,
                EditorExecutable = exe,
            }), TimeSpan.FromMinutes(1));
        var launcher = new ExternalEditorLauncher(isMac: false, start: _ => throw new IOException("싫음"));
        using var h = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), settings: settings, editor: launcher);
        await h.Left.StartAsync();
        h.Left.ClickEntry(L("a.txt"));
        h.RequestEdit();
        await WaitUntil(() => h.Left.State.Error is not null);
        Assert.Equal(new FileError.EditorLaunchFailed("싫음"), h.Left.State.Error);
    }

    [Fact]
    public void 좌우_패널은_따로_받은_시작_경로로_시작한다()
    {
        Assert.Equal(_left, _home.Left.State.CurrentPath);
        Assert.Equal(_right, _home.Right.State.CurrentPath);
    }

    [Fact]
    public void 복사_대화상자는_커서_항목과_반대편_패널_경로로_열린다()
    {
        _home.RequestTransfer(FileOperationKind.Copy);
        var d = Assert.IsType<HomeDialog.Transfer>(_home.Dialog);
        Assert.Equal([L("sub")], d.Sources); // 정렬상 폴더가 첫 항목
        Assert.Equal(_right, d.Target);
    }

    [Fact]
    public async Task 선택이_있으면_선택한_항목이_대상이고_확정하면_선택이_풀린다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        _home.Left.ClickEntry(L("b.txt"), ctrl: true);
        _home.RequestTransfer(FileOperationKind.Copy);
        Assert.Equal([L("b.txt")], Assert.IsType<HomeDialog.Transfer>(_home.Dialog).Sources);

        _home.Left.ClickEntry(L("a.txt"), ctrl: true);
        _home.RequestTransfer(FileOperationKind.Copy);
        Assert.Equal(new HashSet<string> { L("a.txt"), L("b.txt") },
            Assert.IsType<HomeDialog.Transfer>(_home.Dialog).Sources.ToHashSet());

        _home.ConfirmTransfer(_right);
        Assert.Null(_home.Dialog);
        Assert.Empty(_home.Left.State.Selected);
        await WaitQueueIdle();
        Assert.Equal("A", File.ReadAllText(R("a.txt")));
        Assert.Equal("B", File.ReadAllText(R("b.txt")));
    }

    [Fact]
    public async Task 복사가_끝나면_두_패널이_새로고침된다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        _home.RequestTransfer(FileOperationKind.Copy);
        _home.ConfirmTransfer(_right);
        await WaitQueueIdle();
        await WaitUntil(() => _home.Right.State.Entries.Any(e => e.Name == "a.txt"));
    }

    [Fact]
    public void 없는_대상_폴더는_대화상자에_오류를_남기고_작업을_만들지_않는다()
    {
        _home.RequestTransfer(FileOperationKind.Move);
        _home.ConfirmTransfer(Path.Combine(_root, "nowhere"));
        var d = Assert.IsType<HomeDialog.Transfer>(_home.Dialog);
        Assert.Equal(new FileError.TargetFolderMissing(), d.Error);
        Assert.Empty(_home.Queue.Jobs);
    }

    [Fact]
    public async Task 이동은_원본을_옮긴다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        _home.RequestTransfer(FileOperationKind.Move);
        _home.ConfirmTransfer(_right);
        await WaitQueueIdle();
        Assert.False(File.Exists(L("a.txt")));
        Assert.True(File.Exists(R("a.txt")));
    }

    [Fact]
    public async Task 휴지통을_못_쓰면_삭제_대화상자는_영구_삭제로_열리고_확정하면_지운다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        _home.RequestDelete();
        Assert.False(Assert.IsType<HomeDialog.Delete>(_home.Dialog).ToTrash);
        _home.ConfirmDelete();
        await WaitQueueIdle();
        Assert.False(File.Exists(L("a.txt")));
        Assert.Equal(JobStatus.Done, Assert.Single(_home.Queue.Jobs).Status);
    }

    [Fact]
    public async Task 휴지통을_쓸_수_있으면_기본은_휴지통이고_Shift는_영구_삭제()
    {
        using var withTrash = Create(canTrash: true);
        await withTrash.Left.StartAsync();
        withTrash.RequestDelete();
        Assert.True(Assert.IsType<HomeDialog.Delete>(withTrash.Dialog).ToTrash);
        withTrash.RequestDelete(permanent: true);
        Assert.False(Assert.IsType<HomeDialog.Delete>(withTrash.Dialog).ToTrash);
    }

    [Fact]
    public async Task 새_폴더를_만들면_목록이_갱신되고_커서가_거기로_간다()
    {
        _home.RequestName(NameMode.NewFolder);
        await _home.ConfirmNameAsync("만든 폴더");
        await WaitUntil(() => _home.Left.State.CursorEntry?.Name == "만든 폴더");
        Assert.Null(_home.Dialog);
        Assert.True(Directory.Exists(L("만든 폴더")));
    }

    [Fact]
    public async Task 이미_있는_이름이면_대화상자가_남고_오류가_표시된다()
    {
        _home.RequestName(NameMode.NewFile);
        await _home.ConfirmNameAsync("a.txt");
        var d = Assert.IsType<HomeDialog.NameInput>(_home.Dialog);
        Assert.Equal(new FileError.AlreadyExists("a.txt"), d.Error);
        Assert.Equal("A", File.ReadAllText(L("a.txt")));
    }

    [Fact]
    public async Task 이름변경_대화상자는_커서_항목의_이름으로_열리고_확정하면_바뀐다()
    {
        _home.Left.ClickEntry(L("b.txt"));
        _home.RequestName(NameMode.Rename);
        Assert.Equal("b.txt", Assert.IsType<HomeDialog.NameInput>(_home.Dialog).InitialText);
        await _home.ConfirmNameAsync("c.txt");
        await WaitUntil(() => _home.Left.State.CursorEntry?.Name == "c.txt");
        Assert.False(File.Exists(L("b.txt")));
        Assert.True(File.Exists(L("c.txt")));
    }

    [Fact]
    public async Task 대상이_없으면_대화상자를_열지_않는다()
    {
        var empty = Directory.CreateDirectory(Path.Combine(_root, "empty")).FullName;
        await _home.Left.NavigateToAsync(empty);
        _home.RequestTransfer(FileOperationKind.Copy);
        _home.RequestDelete();
        _home.RequestName(NameMode.Rename);
        Assert.Null(_home.Dialog);
    }

    [Fact]
    public void 대화상자를_닫으면_ModalOpen이_풀린다()
    {
        _home.RequestDelete();
        Assert.True(_home.ModalOpen);
        _home.CloseDialog();
        Assert.False(_home.ModalOpen);
    }

    [Fact]
    public void 활성_패널은_HomeState가_가진다()
    {
        Assert.Equal(PaneSide.Left, _home.Active);
        _home.Activate(PaneSide.Right);
        Assert.Equal(PaneSide.Right, _home.Active);
        Assert.NotSame(_home.Controller(PaneSide.Left), _home.Controller(PaneSide.Right));
        _home.SwitchActive();
        Assert.Equal(PaneSide.Left, _home.Active);
        _home.SwitchActive();
        Assert.Equal(PaneSide.Right, _home.Active);
    }
}
