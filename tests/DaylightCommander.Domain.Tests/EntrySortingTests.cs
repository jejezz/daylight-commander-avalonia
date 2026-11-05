using DaylightCommander.Domain;

namespace DaylightCommander.Domain.Tests;

public class EntrySortingTests
{
    static FileEntry File(string name, long? size = 0, long? modified = 0) =>
        new($"/{name}", name, false, size, modified is { } m ? DateTimeOffset.FromUnixTimeSeconds(m) : null);

    static FileEntry Dir(string name) => new($"/{name}", name, true);

    static string[] Names(IEnumerable<FileEntry> list) => list.Select(e => e.Name).ToArray();

    [Fact]
    public void 이름_정렬은_폴더_먼저_대소문자_무시()
    {
        var sorted = EntrySorting.Sort([File("b"), Dir("Z"), File("A"), Dir("a")], SortField.Name, true);
        Assert.Equal(["a", "Z", "A", "b"], Names(sorted));
    }

    [Fact]
    public void 내림차순에서도_폴더가_먼저고_방향은_각_그룹_안에서만_뒤집힌다()
    {
        var sorted = EntrySorting.Sort([File("a"), Dir("x"), File("b"), Dir("y")], SortField.Name, false);
        Assert.Equal(["y", "x", "b", "a"], Names(sorted));
    }

    [Fact]
    public void 크기_정렬에서_null은_마이너스_1로_본다()
    {
        var sorted = EntrySorting.Sort([File("big", 100), File("none", null), File("small", 1)], SortField.Size, true);
        Assert.Equal(["none", "small", "big"], Names(sorted));
    }

    [Fact]
    public void 수정일_정렬()
    {
        var sorted = EntrySorting.Sort([File("new", modified: 20), File("old", modified: 10)], SortField.Modified, true);
        Assert.Equal(["old", "new"], Names(sorted));
    }

    [Fact]
    public void 수정일이_하나라도_null이면_같은_것으로_보고_이름_순으로_정리된다()
    {
        var sorted = EntrySorting.Sort([File("b", modified: 5), File("a", modified: null)], SortField.Modified, true);
        Assert.Equal(["a", "b"], Names(sorted));
    }

    [Fact]
    public void 확장자_정렬은_소문자_비교이고_점으로_시작하는_이름은_확장자_없음()
    {
        var sorted = EntrySorting.Sort(
            [File("x.TXT"), File(".gitignore"), File("y.md"), File("noext")], SortField.Extension, true);
        Assert.Equal([".gitignore", "noext", "y.md", "x.TXT"], Names(sorted));
    }

    [Fact]
    public void 확장자_추출()
    {
        Assert.Equal("gz", EntrySorting.ExtensionOf("a.tar.gz"));
        Assert.Equal("", EntrySorting.ExtensionOf(".bashrc"));
        Assert.Equal("", EntrySorting.ExtensionOf("README"));
        Assert.Equal("", EntrySorting.ExtensionOf("trailing."));
    }

    [Fact]
    public void 같은_값이면_원래_순서가_아니라_이름_순으로_고정된다()
    {
        var sorted = EntrySorting.Sort([File("c", 5), File("a", 5), File("b", 5)], SortField.Size, true);
        Assert.Equal(["a", "b", "c"], Names(sorted));
    }
}
