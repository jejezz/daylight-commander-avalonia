using System.Text.RegularExpressions;
using DaylightCommander.Domain;

namespace DaylightCommander.Domain.Tests;

public class AppVersionTests
{
    [Fact]
    public void 버전은_MAJOR_MINOR_PATCH_형식이고_MAJOR는_1_이상이다()
    {
        // macOS 패키징이 0.y.z를 거부한다.
        var m = Regex.Match(AppVersion.Version, @"^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.]+)?$");
        Assert.True(m.Success, AppVersion.Version);
        Assert.True(int.Parse(m.Groups[1].Value) >= 1, AppVersion.Version);
    }

    [Fact]
    public void 빌드_번호는_양수이고_표시는_버전과_빌드를_함께_보인다()
    {
        Assert.True(AppVersion.Build >= 1);
        Assert.Equal($"{AppVersion.Version} ({AppVersion.Build})", AppVersion.Display);
    }
}
