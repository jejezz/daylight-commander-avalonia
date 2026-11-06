using DaylightCommander.Domain;

namespace DaylightCommander.Domain.Tests;

public sealed class LocationsTests
{
    [Theory]
    [InlineData("sftp://me@host/a/b", true)]
    [InlineData("SFTP://me@host", true)]
    [InlineData("ftp://host/pub", true)]
    [InlineData(@"C:\Users\me", false)]
    [InlineData("/home/me", false)]
    [InlineData("sftpx://host", false)]
    public void 원격_경로를_알아본다(string path, bool expected) => Assert.Equal(expected, Locations.IsRemote(path));

    [Fact]
    public void 계정_호스트_포트_경로로_나눈다()
    {
        var loc = Locations.TryParse("sftp://me@Example.com:2222/home/me/my dir")!;
        Assert.Equal("sftp", loc.Scheme);
        Assert.Equal("me", loc.User);
        Assert.Equal("Example.com", loc.Host);
        Assert.Equal(2222, loc.Port);
        Assert.Equal("/home/me/my dir", loc.Path);
        Assert.Equal("sftp://me@Example.com:2222", loc.Authority);
    }

    [Fact]
    public void 포트와_경로를_생략하면_기본값이다()
    {
        var loc = Locations.TryParse("sftp://me@host")!;
        Assert.Equal(22, loc.Port);
        Assert.Equal("/", loc.Path);
        Assert.Equal("sftp://me@host", loc.Authority); // 기본 포트는 적지 않는다
        Assert.Equal("sftp://me@host/", Locations.Format(loc));
    }

    [Fact]
    public void FTP는_기본_포트가_21이고_익명도_계정_없이_쓸_수_있다()
    {
        var loc = Locations.TryParse("ftp://host/pub/x")!;
        Assert.Equal(("ftp", null, "host", 21, "/pub/x"), (loc.Scheme, loc.User, loc.Host, loc.Port, loc.Path));
        Assert.Equal("ftp://host", loc.Authority);
        Assert.Equal("ftp://me@host:2121", Locations.FormatAuthority("ftp", "me", "host", 2121));
        Assert.False(Locations.SameServer("ftp://me@host/a", "sftp://me@host/a")); // 같은 호스트라도 프로토콜이 다르면 다른 서버
        Assert.Equal("ftp://host/pub", Locations.Parent("ftp://host/pub/x"));
    }

    [Fact]
    public void 호스트가_없으면_해석하지_않는다()
    {
        Assert.Null(Locations.TryParse("sftp://"));
        Assert.Null(Locations.TryParse("sftp://me@/x"));
        Assert.Null(Locations.TryParse(@"C:\x"));
    }

    [Fact]
    public void 서버_안_경로는_점과_중복_슬래시를_정리한다()
    {
        Assert.Equal("sftp://me@host/a/c", Locations.NormalizeRemote("sftp://me@host/a//b/../c/./"));
        Assert.Equal("sftp://me@host/", Locations.NormalizeRemote("sftp://me@host/a/.."));
        Assert.Equal("sftp://me@host/", Locations.NormalizeRemote("sftp://me@host/../.."));
        Assert.Equal("sftp://me@host:2200/x", Locations.NormalizeRemote("sftp://me@host:2200/x/"));
        Assert.Equal(@"C:\a", Locations.NormalizeRemote(@"C:\a")); // 로컬은 그대로
    }

    [Fact]
    public void 위_폴더는_서버_루트에서_끝난다()
    {
        Assert.Equal("sftp://me@host/a", Locations.Parent("sftp://me@host/a/b"));
        Assert.Equal("sftp://me@host/", Locations.Parent("sftp://me@host/a"));
        Assert.Null(Locations.Parent("sftp://me@host/"));
        Assert.Null(Locations.Parent("sftp://me@host"));
        Assert.Equal(@"C:\", Locations.Parent(@"C:\a"));
        Assert.Null(Locations.Parent(@"C:\"));
    }

    [Fact]
    public void 합치기와_이름()
    {
        Assert.Equal("sftp://me@host/a/b.txt", Locations.Combine("sftp://me@host/a", "b.txt"));
        Assert.Equal("sftp://me@host/b.txt", Locations.Combine("sftp://me@host/", "b.txt"));
        Assert.Equal("sftp://me@host:22022/a/한글 이름.txt", Locations.Combine("sftp://me@host:22022/a", "한글 이름.txt"));
        Assert.Equal("b.txt", Locations.NameOf("sftp://me@host/a/b.txt"));
        Assert.Equal("sftp://me@host", Locations.NameOf("sftp://me@host/")); // 서버 루트의 이름은 서버
        Assert.Equal("b.txt", Locations.NameOf(@"C:\a\b.txt"));
        Assert.Equal("a", Locations.NameOf(@"C:\a\"));
    }

    [Fact]
    public void 같은_서버는_스킴_계정_호스트_포트가_같을_때다()
    {
        Assert.True(Locations.SameServer("sftp://me@host/a", "sftp://me@HOST/b"));
        Assert.False(Locations.SameServer("sftp://me@host/a", "sftp://you@host/a"));
        Assert.False(Locations.SameServer("sftp://me@host/a", "sftp://me@host:2222/a"));
        Assert.False(Locations.SameServer("sftp://me@host/a", @"C:\a"));
        Assert.False(Locations.SameServer(@"C:\a", @"C:\b"));
    }

    [Fact]
    public void 서버_이름을_저장했다가_같은_서버를_바꿔치기한다()
    {
        var a = new Settings.NetworkProfile { Host = "h", Port = 22, User = "me" };
        var s = new Settings.AppSettings().WithNetworkProfile(a);
        Assert.Single(s.NetworkProfiles);
        s = s.WithNetworkProfile(a with { Host = "H" }); // 대소문자만 다른 호스트는 같은 서버
        Assert.Single(s.NetworkProfiles);
        s = s.WithNetworkProfile(a with { User = "you" });
        Assert.Equal(2, s.NetworkProfiles.Count);
        Assert.Single(s.WithoutNetworkProfile(a with { Host = "H" }).NetworkProfiles);
    }
}
