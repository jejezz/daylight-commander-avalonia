using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>디렉터리 트리의 규칙: 하위 폴더 나열, 조상 목록, 같은 경로 판단.</summary>
public sealed class DirectoryTreeTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("dc-tree-test").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    string P(string name) => Path.Combine(_root, name);

    [Fact]
    public void 하위_폴더는_이름순으로_나오고_파일과_숨김_폴더는_빠진다()
    {
        Directory.CreateDirectory(P("beta"));
        Directory.CreateDirectory(P("Alpha"));
        Directory.CreateDirectory(P(".hidden"));
        File.WriteAllText(P("file.txt"), "x");

        Assert.Equal([P("Alpha"), P("beta")], DirectoryTree.ListSubfolders(_root));
    }

    [Fact]
    public void 폴더가_없거나_읽을_수_없으면_빈_목록이다()
    {
        Assert.Empty(DirectoryTree.ListSubfolders(P("missing")));
    }

    [Fact]
    public void 조상은_루트부터_자기_자신_직전까지다()
    {
        var path = P(Path.Combine("a", "b"));
        var ancestors = DirectoryTree.Ancestors(path);
        Assert.Equal(P("a"), ancestors[^1]);
        Assert.Equal(Path.GetPathRoot(_root), ancestors[0]);
        Assert.DoesNotContain(path, ancestors);
        Assert.Equal(ancestors.Distinct().Count(), ancestors.Count);
    }

    [Fact]
    public void 루트에는_조상이_없다()
    {
        Assert.Empty(DirectoryTree.Ancestors(Path.GetPathRoot(_root)!));
    }

    [Fact]
    public void 끝_구분자와_대소문자는_같은_경로로_본다()
    {
        Assert.True(DirectoryTree.SamePath(_root, _root + Path.DirectorySeparatorChar));
        Assert.False(DirectoryTree.SamePath(P("a"), P("b")));
        if (!OperatingSystem.IsLinux()) Assert.True(DirectoryTree.SamePath(P("A"), P("a")));
    }
}
