using DaylightCommander.Domain;

namespace DaylightCommander.Domain.Tests;

public class BytesFormatTests
{
    [Fact]
    public void null은_두_개의_하이픈() => Assert.Equal("--", BytesFormat.Format(null));

    [Fact]
    public void 바이트는_소수점_없이()
    {
        Assert.Equal("0 B", BytesFormat.Format(0));
        Assert.Equal("1023 B", BytesFormat.Format(1023));
    }

    [Fact]
    public void _1024_단위로_올라가며_소수점_한_자리()
    {
        Assert.Equal("1.0 KB", BytesFormat.Format(1024));
        Assert.Equal("1.5 KB", BytesFormat.Format(1536));
        Assert.Equal("1.0 MB", BytesFormat.Format(1024L * 1024));
        Assert.Equal("2.0 GB", BytesFormat.Format(2L * 1024 * 1024 * 1024));
    }

    [Fact]
    public void 로캘과_무관하게_점을_쓴다()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.5 KB", BytesFormat.Format(1536));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }
}
