using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>패턴(*, ?) 선택.</summary>
public sealed class PatternSelectTests : IAsyncLifetime
{
    [Theory]
    [InlineData("*.jpg", "a.JPG", true)]
    [InlineData("*.jpg", "a.jpg.bak", false)]
    [InlineData("IMG_*.png", "img_001.png", true)]
    [InlineData("a?c.txt", "abc.txt", true)]
    [InlineData("a?c.txt", "ac.txt", false)]
    [InlineData("a?c.txt", "abbc.txt", false)]
    [InlineData("*", "anything", true)]
    [InlineData("*.", "file.", true)]
    [InlineData("*.", "file.txt", false)]
    [InlineData("a.b", "aXb", false)] // 점은 글자 그대로
    [InlineData("(1)*", "(1) copy", true)] // 정규식 특수문자는 그대로
    [InlineData("[x]*.txt", "[x] a.txt", true)]
    public void 와일드카드는_이름_전체에_대소문자_무시로_맞는다(string pattern, string name, bool expected)
    {
        Assert.Equal(expected, GlobPattern.ToRegex(pattern).IsMatch(name));
    }

    readonly string _root = Directory.CreateTempSubdirectory("dc-pattern-test").FullName;
    readonly ListLocalDirectory _lister = new();
    HomeState _home = null!;

    public PatternSelectTests()
    {
        foreach (var n in new[] { "a.txt", "b.txt", "c.jpg", "d.JPG", ".hidden.txt" })
            File.WriteAllText(Path.Combine(_root, n), "x");
        Directory.CreateDirectory(Path.Combine(_root, "dir.txt"));
    }

    public async Task InitializeAsync()
    {
        _home = new HomeState(_root, _root, (p, ct) => _lister.InvokeAsync(p, ct));
        await _home.Left.StartAsync();
        await _home.Right.StartAsync();
    }

    public Task DisposeAsync()
    {
        _home.Dispose();
        Directory.Delete(_root, recursive: true);
        return Task.CompletedTask;
    }

    static string[] Names(PaneController c) => c.State.Selected.Select(Path.GetFileName).OfType<string>().Order().ToArray();

    [Fact]
    public void 선택_추가는_맞는_항목을_기존_선택에_더한다()
    {
        _home.Left.ClickEntry(Path.Combine(_root, "c.jpg"), ctrl: true);
        _home.Left.SelectByPattern("*.txt", add: true);
        Assert.Equal(["a.txt", "b.txt", "c.jpg", "dir.txt"], Names(_home.Left)); // 폴더도 이름이 맞으면 들어간다 (원본과 같다)
    }

    [Fact]
    public void 선택_해제는_맞는_항목만_뺀다()
    {
        _home.Left.SelectByPattern("*", add: true);
        _home.Left.SelectByPattern("*.jpg", add: false);
        Assert.DoesNotContain("c.jpg", Names(_home.Left));
        Assert.DoesNotContain("d.JPG", Names(_home.Left));
        Assert.Contains("a.txt", Names(_home.Left));
    }

    [Fact]
    public void 보이지_않는_숨김_항목은_선택하지_않는다()
    {
        _home.Left.SelectByPattern("*", add: true);
        Assert.DoesNotContain(".hidden.txt", Names(_home.Left));
    }

    [Fact]
    public void 빈_패턴은_아무것도_바꾸지_않는다()
    {
        _home.Left.SelectByPattern("  ", add: true);
        Assert.Empty(_home.Left.State.Selected);
    }

    [Fact]
    public void 대화상자는_확정하면_활성_패널에_적용하고_닫힌다()
    {
        _home.RequestPatternSelect();
        Assert.IsType<HomeDialog.PatternSelect>(_home.Dialog);
        _home.ConfirmPatternSelect("*.jpg", add: true);
        Assert.Null(_home.Dialog);
        Assert.Equal(["c.jpg", "d.JPG"], Names(_home.Left));
        Assert.Empty(_home.Right.State.Selected);
    }

    [Fact]
    public void 대화상자가_없으면_확정은_아무_일도_하지_않는다()
    {
        _home.ConfirmPatternSelect("*", add: true);
        Assert.Empty(_home.Left.State.Selected);
    }
}
