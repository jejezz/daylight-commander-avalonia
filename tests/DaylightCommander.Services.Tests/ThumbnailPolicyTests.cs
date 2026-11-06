using DaylightCommander.Domain;

namespace DaylightCommander.Services.Tests;

public class ThumbnailPolicyTests
{
    static FileEntry File(string path, long? size = 1000, DateTimeOffset? at = null) =>
        new(path, Path.GetFileName(path), false, size, at);

    [Theory]
    [InlineData(@"C:\p\a.png")]
    [InlineData(@"C:\p\a.JPG")]
    [InlineData(@"C:\p\a.webp")]
    public void 로컬_이미지는_만든다(string path) => Assert.True(ThumbnailPolicy.ShouldGenerate(File(path)));

    [Theory]
    [InlineData(@"C:\p\a.txt")]
    [InlineData(@"C:\p\a.mp4")]   // 동영상은 셸 연동(2차)
    [InlineData(@"C:\p\noext")]
    public void 이미지가_아니면_형식_아이콘(string path) => Assert.False(ThumbnailPolicy.ShouldGenerate(File(path)));

    [Fact]
    public void 폴더는_건너뛴다() =>
        Assert.False(ThumbnailPolicy.ShouldGenerate(new FileEntry(@"C:\p\pics.png", "pics.png", true)));

    [Theory]
    [InlineData("sftp://u@h/a.png")]
    [InlineData("ftp://h/a.jpg")]
    [InlineData("webdavs://h/dav/a.png")]
    public void 원격_경로는_건너뛴다(string path) => Assert.False(ThumbnailPolicy.ShouldGenerate(File(path)));

    [Fact]
    public void 너무_큰_이미지는_건너뛰고_크기를_모르면_만든다()
    {
        Assert.False(ThumbnailPolicy.ShouldGenerate(File(@"C:\p\a.png", ThumbnailPolicy.MaxImageBytes + 1)));
        Assert.True(ThumbnailPolicy.ShouldGenerate(File(@"C:\p\a.png", ThumbnailPolicy.MaxImageBytes)));
        Assert.True(ThumbnailPolicy.ShouldGenerate(File(@"C:\p\a.png", null)));
    }

    [Fact]
    public void 캐시_키는_경로_시각_크기_요청크기가_모두_같을_때만_같다()
    {
        var t = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var a = ThumbnailPolicy.CacheKey(File(@"C:\p\a.png", 10, t), 256);
        Assert.Equal(a, ThumbnailPolicy.CacheKey(File(@"C:\p\a.png", 10, t), 256));
        Assert.NotEqual(a, ThumbnailPolicy.CacheKey(File(@"C:\p\b.png", 10, t), 256));
        Assert.NotEqual(a, ThumbnailPolicy.CacheKey(File(@"C:\p\a.png", 11, t), 256));
        Assert.NotEqual(a, ThumbnailPolicy.CacheKey(File(@"C:\p\a.png", 10, t.AddSeconds(1)), 256));
        Assert.NotEqual(a, ThumbnailPolicy.CacheKey(File(@"C:\p\a.png", 10, t), 128));
        Assert.Equal(32, a.Length);
        Assert.Matches("^[0-9a-f]+$", a);
    }

    [Theory]
    [InlineData(48, 128)]
    [InlineData(112, 256)]
    public void 디코딩_폭은_타일의_두_배를_128_단위로_올린다(int tile, int expected) =>
        Assert.Equal(expected, ThumbnailPolicy.DecodeWidth(tile));
}
