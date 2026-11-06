using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>좌우 패널 교환과 경로 맞추기.</summary>
public sealed class PaneLayoutTests : IAsyncLifetime
{
    readonly string _root = Directory.CreateTempSubdirectory("dc-layout-test").FullName;
    readonly string _left;
    readonly string _right;
    readonly ListLocalDirectory _lister = new();
    HomeState _home = null!;

    public PaneLayoutTests()
    {
        _left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
        _right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
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

    [Fact]
    public void 처음에는_왼쪽_패널이_왼쪽에_있다()
    {
        Assert.False(_home.Swapped);
        Assert.Equal(PaneSide.Left, _home.Leftmost);
        Assert.Equal(PaneSide.Right, _home.Rightmost);
    }

    [Fact]
    public void 교환하면_화면_위치만_바뀌고_패널의_경로는_그대로다()
    {
        var changes = 0;
        _home.LayoutChanged += () => changes++;
        _home.ToggleSwap();
        Assert.Equal(PaneSide.Right, _home.Leftmost);
        Assert.Equal(PaneSide.Left, _home.Rightmost);
        Assert.Equal(_left, _home.Left.State.CurrentPath);
        Assert.Equal(_right, _home.Right.State.CurrentPath);
        _home.ToggleSwap();
        Assert.Equal(PaneSide.Left, _home.Leftmost);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void 같은_값으로_설정하면_알리지_않는다()
    {
        var changes = 0;
        _home.LayoutChanged += () => changes++;
        _home.SetSwapped(false);
        Assert.Equal(0, changes);
        _home.SetSwapped(true);
        _home.SetSwapped(true);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task 경로_맞추기는_화면_위치_기준이다()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_left, "sub")).FullName;
        await _home.Left.NavigateToAsync(sub);

        await _home.SyncPathRightAsync(); // 화면 왼쪽(=Left 패널)의 경로를 오른쪽으로
        Assert.Equal(sub, _home.Right.State.CurrentPath);

        _home.SetSwapped(true); // 이제 화면 왼쪽은 Right 패널, 화면 오른쪽은 Left 패널
        var other = Directory.CreateDirectory(Path.Combine(_right, "other")).FullName;
        await _home.Right.NavigateToAsync(other);
        await _home.SyncPathRightAsync(); // 화면 왼쪽(Right 패널) → 화면 오른쪽(Left 패널)
        Assert.Equal(other, _home.Left.State.CurrentPath);
    }

    [Fact]
    public async Task 왼쪽으로_맞추기는_화면_오른쪽_패널의_경로를_가져온다()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_right, "sub")).FullName;
        await _home.Right.NavigateToAsync(sub);
        await _home.SyncPathLeftAsync();
        Assert.Equal(sub, _home.Left.State.CurrentPath);
    }

    [Fact]
    public void 처음에는_2패널이고_전환하면_알리며_패널_상태는_그대로다()
    {
        Assert.False(_home.SinglePane);
        var changes = 0;
        _home.LayoutChanged += () => changes++;
        _home.ToggleSinglePane();
        Assert.True(_home.SinglePane);
        _home.SetSinglePane(true); // 같은 값은 알리지 않는다
        Assert.Equal(1, changes);
        Assert.Equal(_left, _home.Left.State.CurrentPath);
        Assert.Equal(_right, _home.Right.State.CurrentPath);
        _home.ToggleSinglePane();
        Assert.False(_home.SinglePane);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void 교환과_1패널_모드는_서로_독립이다()
    {
        _home.ToggleSwap();
        _home.ToggleSinglePane();
        Assert.True(_home.Swapped);
        Assert.True(_home.SinglePane);
        _home.ToggleSinglePane();
        Assert.True(_home.Swapped);
    }
}
