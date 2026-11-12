using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>끌어다 놓기 규칙(DropPlanner)과 HomeState.Drop·복제·파일 관리자에서 보기.</summary>
public sealed class DropTests : IAsyncLifetime
{
    sealed class RecordingRevealer(Action<string>? onReveal = null) : IFileRevealer
    {
        public List<string> Revealed { get; } = [];

        public void Reveal(string path)
        {
            onReveal?.Invoke(path);
            Revealed.Add(path);
        }
    }

    readonly string _root = Directory.CreateTempSubdirectory("dc-drop-test").FullName;
    readonly string _left;
    readonly string _right;
    readonly ListLocalDirectory _lister = new();
    readonly IReadOnlyList<DriveEntry> _drives = new ListDrives().Invoke();
    HomeState _home = null!;

    public DropTests()
    {
        _left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
        _right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
        File.WriteAllText(Path.Combine(_left, "a.txt"), "A");
        File.WriteAllText(Path.Combine(_left, "b.txt"), "B");
        Directory.CreateDirectory(Path.Combine(_left, "dir"));
        File.WriteAllText(Path.Combine(_left, "dir", "inner.txt"), "I");
    }

    public async Task InitializeAsync()
    {
        _home = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), revealer: new RecordingRevealer());
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

    Task WaitQueueIdle() => WaitUntil(() => _home.Queue.Jobs.Count > 0 && _home.Queue.Jobs.All(j => j.IsFinished));

    // --- 계획 (순수) ---

    [Fact]
    public void 같은_드라이브는_이동이다()
    {
        Assert.Equal(DropAction.Move, DropPlanner.Plan([L("a.txt")], _right, _drives).Action);
    }

    [Fact]
    public void Ctrl은_복사_Shift는_이동으로_바꾼다()
    {
        Assert.Equal(DropAction.Copy, DropPlanner.Plan([L("a.txt")], _right, _drives, DropMode.ForceCopy).Action);
        Assert.Equal(DropAction.Move, DropPlanner.Plan([L("a.txt")], _right, _drives, DropMode.ForceMove).Action);
    }

    [Fact]
    public void 이미_그_폴더에_있는_항목의_이동은_할_일이_없다()
    {
        Assert.Equal(DropAction.None, DropPlanner.Plan([L("a.txt")], _left, _drives).Action);
        Assert.Equal(DropAction.None, DropPlanner.Plan([L("a.txt")], _left + Path.DirectorySeparatorChar, _drives, DropMode.ForceMove).Action);
    }

    [Fact]
    public void 같은_폴더로의_복사는_복제본을_만든다()
    {
        Assert.Equal(DropAction.Copy, DropPlanner.Plan([L("a.txt")], _left, _drives, DropMode.ForceCopy).Action);
    }

    [Fact]
    public void 폴더를_자기_자신이나_안쪽에_놓으면_오류다()
    {
        var self = DropPlanner.Plan([L("dir")], L("dir"), _drives);
        Assert.Equal(DropAction.Error, self.Action);
        Assert.Equal(new FileError.IntoItself("dir"), self.Error);

        Directory.CreateDirectory(L("dir/sub"));
        Assert.Equal(DropAction.Error, DropPlanner.Plan([L("dir")], L("dir/sub"), _drives, DropMode.ForceCopy).Action);
    }

    [Fact]
    public void 이름만_비슷한_폴더는_안쪽이_아니다()
    {
        Directory.CreateDirectory(L("dir2"));
        Assert.Equal(DropAction.Move, DropPlanner.Plan([L("dir")], L("dir2"), _drives).Action);
    }

    [Fact]
    public void 드라이브를_알_수_없으면_복사다()
    {
        Assert.False(DropPlanner.SameDrive(L("a.txt"), _right, []));
        Assert.Equal(DropAction.Copy, DropPlanner.Plan([L("a.txt")], _right, []).Action);
    }

    [Fact]
    public void 항목이_없으면_아무것도_하지_않는다()
    {
        Assert.Equal(DropAction.None, DropPlanner.Plan([], _right, _drives).Action);
    }

    // --- HomeState.Drop ---

    [Fact]
    public async Task 다른_패널로_끌어다_놓으면_이동한다()
    {
        _home.Drop([L("a.txt")], _right);
        Assert.Empty(_home.Queue.Jobs); // 이동은 확인을 받기 전에는 시작하지 않는다
        Assert.IsType<HomeDialog.ConfirmDropMove>(_home.Dialog);
        _home.ConfirmDropMove();
        await WaitQueueIdle();
        Assert.False(File.Exists(L("a.txt")));
        Assert.Equal("A", File.ReadAllText(R("a.txt")));
    }

    [Fact]
    public async Task Ctrl로_끌어다_놓으면_복사한다()
    {
        _home.Drop([L("a.txt"), L("b.txt")], _right, DropMode.ForceCopy);
        await WaitQueueIdle();
        Assert.True(File.Exists(L("a.txt")));
        Assert.Equal("B", File.ReadAllText(R("b.txt")));
    }

    [Fact]
    public async Task 폴더_위에_놓으면_그_폴더_안으로_들어간다()
    {
        _home.Drop([L("a.txt")], L("dir"));
        _home.ConfirmDropMove();
        await WaitQueueIdle();
        Assert.Equal("A", File.ReadAllText(L("dir/a.txt")));
        Assert.False(File.Exists(L("a.txt")));
    }

    [Fact]
    public void 같은_폴더에_이동으로_놓으면_작업을_만들지_않는다()
    {
        _home.Drop([L("a.txt")], _left);
        Assert.Empty(_home.Queue.Jobs);
    }

    [Fact]
    public async Task 같은_폴더에_복사로_놓으면_복제본을_만든다()
    {
        _home.Drop([L("a.txt")], _left, DropMode.ForceCopy);
        await WaitQueueIdle();
        Assert.Equal("A", File.ReadAllText(L("a (1).txt")));
    }

    [Fact]
    public void 폴더를_자기_안에_놓으면_작업_대신_오류를_알린다()
    {
        _home.Drop([L("dir")], L("dir"), errorSide: PaneSide.Right);
        Assert.Empty(_home.Queue.Jobs);
        Assert.Equal(new FileError.IntoItself("dir"), _home.Right.State.Error);
        Assert.Null(_home.Left.State.Error);
    }

    [Fact]
    public async Task 끌어다_놓으면_끌어_온_항목의_선택이_풀린다()
    {
        _home.Left.ClickEntry(L("a.txt"), ctrl: true);
        _home.Left.ClickEntry(L("b.txt"), ctrl: true);
        Assert.Equal(2, _home.Left.State.Selected.Count);
        _home.Drop([L("a.txt"), L("b.txt")], _right);
        _home.ConfirmDropMove();
        Assert.Empty(_home.Left.State.Selected);
        await WaitQueueIdle();
    }

    // --- 클립보드 (Ctrl+C·X·V) ---

    [Fact]
    public async Task 복사한_선택을_반대_패널에_붙이면_복사되고_클립보드는_남는다()
    {
        _home.Left.ClickEntry(L("a.txt"), ctrl: true);
        _home.Left.ClickEntry(L("b.txt"), ctrl: true);
        var copied = _home.CopySelectionToClipboard(cut: false);
        Assert.Equal([L("a.txt"), L("b.txt")], copied);

        _home.PasteClipboard(_right);
        await WaitQueueIdle();
        Assert.Equal("A", File.ReadAllText(R("a.txt")));
        Assert.Equal("B", File.ReadAllText(R("b.txt")));
        Assert.True(File.Exists(L("a.txt")));
        Assert.NotNull(_home.Clipboard);
    }

    [Fact]
    public async Task 잘라낸_항목을_붙이면_묻지_않고_옮기고_클립보드가_비워진다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        _home.CopySelectionToClipboard(cut: true);
        _home.PasteClipboard(_right);
        Assert.Null(_home.Dialog); // 이동 확인 대화상자 없이
        await WaitQueueIdle();
        Assert.True(File.Exists(R("a.txt")));
        Assert.False(File.Exists(L("a.txt")));
        Assert.Null(_home.Clipboard);
    }

    [Fact]
    public async Task 복사한_항목을_같은_폴더에_붙이면_이름_1로_복제한다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        _home.CopySelectionToClipboard(cut: false);
        _home.PasteClipboard(_left);
        await WaitQueueIdle();
        Assert.Equal("A", File.ReadAllText(L("a (1).txt")));
    }

    [Fact]
    public void 담을_항목이_없으면_클립보드는_그대로다()
    {
        using var empty = new HomeState(_root, _right, (p, ct) => _lister.InvokeAsync(p, ct));
        Assert.Empty(empty.CopySelectionToClipboard(cut: true)); // 아직 목록을 읽지 않아 커서가 없다
        Assert.Null(empty.Clipboard);
    }

    [Fact]
    public async Task 바깥_앱에서_복사한_파일은_복사로_붙인다()
    {
        _home.PasteExternal([L("a.txt")], _right);
        await WaitQueueIdle();
        Assert.True(File.Exists(R("a.txt")));
        Assert.True(File.Exists(L("a.txt")));
    }

    // --- 복제·파일 관리자에서 보기 ---

    [Fact]
    public async Task 복제는_같은_폴더에_이름_1로_복사한다()
    {
        _home.Left.ClickEntry(L("a.txt"));
        _home.DuplicateInPlace();
        await WaitQueueIdle();
        Assert.Equal("A", File.ReadAllText(L("a (1).txt")));
        Assert.True(File.Exists(L("a.txt")));
    }

    [Fact]
    public void 파일_관리자에서_보기는_해당_경로를_넘기고_실패하면_그_패널에_오류가_뜬다()
    {
        var ok = new RecordingRevealer();
        using var h1 = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), revealer: ok);
        h1.RevealInFileManager(L("a.txt"), PaneSide.Left);
        Assert.Equal([L("a.txt")], ok.Revealed);

        var failing = new RecordingRevealer(_ => throw new FileOperationException(new FileError.Other("없음")));
        using var h2 = new HomeState(_left, _right, (p, ct) => _lister.InvokeAsync(p, ct), revealer: failing);
        h2.RevealInFileManager(L("a.txt"), PaneSide.Right);
        Assert.Equal(new FileError.Other("없음"), h2.Right.State.Error);
        Assert.Null(h2.Left.State.Error);
    }

    [Fact]
    public void 파일_관리자_명령줄은_OS마다_다르다()
    {
        Assert.Equal(["explorer.exe", @"/select,C:\a\b.txt"], new FileReveal(isWindows: true, isMac: false).CommandFor(@"C:\a\b.txt"));
        Assert.Equal(["open", "-R", "/a/b.txt"], new FileReveal(isWindows: false, isMac: true).CommandFor("/a/b.txt"));
        Assert.Equal(["xdg-open", "/a"], new FileReveal(isWindows: false, isMac: false).CommandFor("/a/b.txt"));
    }
}
