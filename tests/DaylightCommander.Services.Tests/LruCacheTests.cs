namespace DaylightCommander.Services.Tests;

public class LruCacheTests
{
    [Fact]
    public void 용량이_차면_가장_오래_안_쓴_것부터_버린다()
    {
        var c = new LruCache<string, int>(2);
        c.Set("a", 1);
        c.Set("b", 2);
        Assert.True(c.TryGet("a", out _)); // a를 쓰면 b가 가장 오래된 것
        c.Set("c", 3);
        Assert.False(c.TryGet("b", out _));
        Assert.True(c.TryGet("a", out var a)); Assert.Equal(1, a);
        Assert.True(c.TryGet("c", out _));
        Assert.Equal(2, c.Count);
    }

    [Fact]
    public void 같은_키를_다시_넣으면_값을_바꾸고_개수는_그대로()
    {
        var c = new LruCache<string, int>(2);
        c.Set("a", 1);
        c.Set("a", 9);
        Assert.Equal(1, c.Count);
        Assert.True(c.TryGet("a", out var v)); Assert.Equal(9, v);
    }

    [Fact]
    public void Clear는_모두_비운다()
    {
        var c = new LruCache<string, int>(2);
        c.Set("a", 1);
        c.Clear();
        Assert.Equal(0, c.Count);
        Assert.False(c.TryGet("a", out _));
    }
}
