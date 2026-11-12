using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>디렉터리 트리의 동작: 폴더 붙여넣기·이름 바꾸기·삭제 확인, 사라진 폴더에서 상위로 물러나기.</summary>
public sealed class TreeActionsTests : IAsyncLifetime
{
    readonly string _root = Directory.CreateTempSubdirectory("dc-tree-actions").FullName;
    readonly string _left;
    readonly string _right;
    readonly ListLocalDirectory _lister = new();
    HomeState _home = null!;

    public TreeActionsTests()
    {
        _left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
        _right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
        Directory.CreateDirectory(Path.Combine(_left, "dir", "sub"));
        File.WriteAllText(Path.Combine(_left, "dir", "inner.txt"), "I");
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

    [Fact]
    public async Task 복사한_폴더를_다른_폴더에_붙이면_복사되고_클립보드는_남는다()
    {
        _home.CopyToClipboard([L("dir")], cut: false);
        _home.PasteClipboard(_right);
        await WaitQueueIdle();
        Assert.True(File.Exists(Path.Combine(_right, "dir", "inner.txt")));
        Assert.True(Directory.Exists(L("dir")));
        Assert.NotNull(_home.Clipboard);
    }

    [Fact]
    public async Task 잘라낸_폴더를_붙이면_옮겨지고_클립보드가_비워진다()
    {
        _home.CopyToClipboard([L("dir")], cut: true);
        _home.PasteClipboard(_right);
        await WaitQueueIdle();
        Assert.True(File.Exists(Path.Combine(_right, "dir", "inner.txt")));
        Assert.False(Directory.Exists(L("dir")));
        Assert.Null(_home.Clipboard);
    }

    [Fact]
    public void 클립보드가_비어_있으면_붙여넣기는_아무것도_하지_않는다()
    {
        _home.PasteClipboard(_right);
        Assert.Empty(_home.Queue.Jobs);
    }

    [Fact]
    public void 폴더를_자기_안에_붙이면_작업_대신_오류다()
    {
        _home.CopyToClipboard([L("dir")], cut: true);
        _home.PasteClipboard(L("dir/sub"));
        Assert.Empty(_home.Queue.Jobs);
        Assert.Equal(new FileError.IntoItself("dir"), _home.Left.State.Error);
    }

    [Fact]
    public void 트리의_새_폴더와_이름_바꾸기는_지정한_경로를_대상으로_대화상자를_연다()
    {
        _home.RequestNewFolderIn(L("dir"));
        var created = Assert.IsType<HomeDialog.NameInput>(_home.Dialog);
        Assert.Equal((NameMode.NewFolder, L("dir")), (created.Mode, created.Parent));

        _home.RequestRenamePath(L("dir"));
        var renamed = Assert.IsType<HomeDialog.NameInput>(_home.Dialog);
        Assert.Equal(NameMode.Rename, renamed.Mode);
        Assert.Equal(L("dir"), renamed.OriginalPath);
        Assert.Equal("dir", renamed.InitialText);
        Assert.Equal(_left, renamed.Parent);
    }

    [Fact]
    public async Task 트리에서_이름을_바꾸면_실제로_바뀐다()
    {
        _home.RequestRenamePath(L("dir"));
        await _home.ConfirmNameAsync("renamed");
        Assert.True(Directory.Exists(L("renamed")));
        Assert.False(Directory.Exists(L("dir")));
    }

    [Fact]
    public void 트리의_삭제는_지정한_경로를_확인한다()
    {
        _home.RequestDeletePaths([L("dir")]);
        var d = Assert.IsType<HomeDialog.Delete>(_home.Dialog);
        Assert.Equal([L("dir")], d.Paths);
    }

    [Fact]
    public void 트리의_속성은_지정한_경로로_알린다()
    {
        IReadOnlyList<string>? got = null;
        _home.PropertiesRequested += p => got = p;
        _home.RequestPropertiesOf([L("dir")]);
        Assert.Equal([L("dir")], got);
    }

    [Fact]
    public async Task 패널이_보던_폴더가_사라지면_가장_가까운_상위_폴더로_간다()
    {
        await _home.Left.NavigateToAsync(L("dir/sub"));
        Directory.Delete(L("dir"), recursive: true);
        await _home.Left.RefreshAsync();
        Assert.Equal(_left, _home.Left.State.CurrentPath);
    }

    [Fact]
    public void 가장_가까운_존재하는_폴더_찾기()
    {
        Assert.Equal(_left, DirectoryTree.NearestExisting(L("nope/deeper/still")));
        Assert.Equal(L("dir"), DirectoryTree.NearestExisting(L("dir")));
    }
}
