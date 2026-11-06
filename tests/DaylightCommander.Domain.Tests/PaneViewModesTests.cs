using DaylightCommander.Domain.Settings;

namespace DaylightCommander.Domain.Tests;

public class PaneViewModesTests
{
    [Fact]
    public void 기본은_목록이고_패널별로_따로_둔다()
    {
        var s = new AppSettings();
        Assert.Equal(PaneViewMode.List, s.LeftView);
        Assert.Equal(PaneViewMode.List, s.RightView);
        var changed = s with { RightView = PaneViewMode.LargeThumbs };
        Assert.Equal(PaneViewMode.List, changed.LeftView);
        Assert.Equal(PaneViewMode.LargeThumbs, changed.RightView);
    }

    [Fact]
    public void 격자는_목록_밖의_두_방식이다()
    {
        Assert.False(PaneViewMode.List.IsGrid());
        Assert.True(PaneViewMode.SmallThumbs.IsGrid());
        Assert.True(PaneViewMode.LargeThumbs.IsGrid());
        Assert.True(PaneViewMode.LargeThumbs.IconSize() > PaneViewMode.SmallThumbs.IconSize());
        Assert.Equal(0, PaneViewMode.List.TileWidth());
    }

    // 단계는 부호만 본다: 목록 → 작은 → 큰, 끝에서 멈춘다.
    [Theory]
    [InlineData(PaneViewMode.List, 1, PaneViewMode.SmallThumbs)]
    [InlineData(PaneViewMode.SmallThumbs, 1, PaneViewMode.LargeThumbs)]
    [InlineData(PaneViewMode.LargeThumbs, 1, PaneViewMode.LargeThumbs)]
    [InlineData(PaneViewMode.LargeThumbs, -1, PaneViewMode.SmallThumbs)]
    [InlineData(PaneViewMode.SmallThumbs, -5, PaneViewMode.List)]
    [InlineData(PaneViewMode.List, -1, PaneViewMode.List)]
    public void 휠_한_칸은_보기_방식_한_단계(PaneViewMode from, int direction, PaneViewMode expected) =>
        Assert.Equal(expected, from.Step(direction));

    [Theory]
    [InlineData(PaneViewMode.List, 800, 1)]
    [InlineData(PaneViewMode.LargeThumbs, 700, 5)]
    [InlineData(PaneViewMode.LargeThumbs, 100, 1)]   // 좁아도 최소 한 열
    [InlineData(PaneViewMode.SmallThumbs, 880, 10)]
    [InlineData(PaneViewMode.LargeThumbs, double.NaN, 1)]
    public void 폭에_들어가는_열_수(PaneViewMode mode, double width, int expected) =>
        Assert.Equal(expected, mode.Columns(width));
}
