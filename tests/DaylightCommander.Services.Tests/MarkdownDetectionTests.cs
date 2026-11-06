using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public sealed class MarkdownDetectionTests
{
    [Theory]
    [InlineData("README.md", true)]
    [InlineData("notes.MARKDOWN", true)]
    [InlineData("a.mkd", true)]
    [InlineData("main.cs", false)]
    [InlineData("md", false)]
    [InlineData("readme.md.bak", false)]
    public void 마크다운_문서만_미리보기_대상이다(string name, bool expected)
    {
        Assert.Equal(expected, EditorLanguages.IsMarkdown(name));
    }
}
