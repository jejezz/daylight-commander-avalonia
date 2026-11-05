using DaylightCommander.Domain;

namespace DaylightCommander.Domain.Tests;

public class AppIdentityTests
{
    [Fact]
    public void 표시_이름과_파일_이름은_규약대로다()
    {
        Assert.Equal("Daylight Commander", AppIdentity.DisplayName);
        Assert.Equal("DaylightCommander", AppIdentity.FileName);
        Assert.DoesNotContain(' ', AppIdentity.FileName);
    }

    [Fact]
    public void 식별자는_Kotlin_Compose_판과_다르다()
    {
        // 같은 PC에 나란히 설치해 비교하고 설정 폴더가 섞이지 않게 하려는 의도.
        Assert.Equal("art.zoomon.daylightcommander.avalonia", AppIdentity.Id);
        Assert.NotEqual("art.zoomon.daylightcommander.compose", AppIdentity.Id);
        Assert.NotEqual("art.zoomon.daylightcommander", AppIdentity.Id);
    }

    [Fact]
    public void 저작권_문구는_첫_릴리스_연도를_쓴다()
    {
        Assert.Equal(2026, AppIdentity.FirstReleaseYear);
        Assert.Equal("Copyright © 2026 Jongyun Ahn", AppIdentity.Copyright);
    }
}
