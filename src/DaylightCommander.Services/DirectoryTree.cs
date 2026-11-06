namespace DaylightCommander.Services;

/// <summary>디렉터리 트리(원본의 <c>DirectoryTree</c>)가 쓰는 화면과 무관한 규칙.</summary>
public static class DirectoryTree
{
    /// <summary>
    /// <paramref name="path"/> 바로 아래 폴더들의 전체 경로를 이름순(대소문자 무시)으로. 숨김(점으로 시작) 폴더와 링크는 뺀다.
    /// 읽을 수 없으면(권한 없음 등) 빈 목록 — 자식이 없는 것으로 취급한다.
    /// </summary>
    public static IReadOnlyList<string> ListSubfolders(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path)
                .Where(d => !Path.GetFileName(d).StartsWith('.') && new DirectoryInfo(d).LinkTarget is null)
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary><paramref name="path"/>의 조상들 (루트부터, 자기 자신은 제외). 트리에서 그 경로가 보이도록 펼칠 폴더들이다.</summary>
    public static IReadOnlyList<string> Ancestors(string path)
    {
        var result = new List<string>();
        var current = path.Length > 1 ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
        if (current.Length == 2 && current[1] == ':') current += Path.DirectorySeparatorChar; // "C:" → "C:\"
        while (Path.GetDirectoryName(current) is { Length: > 0 } parent && parent != current)
        {
            result.Add(parent);
            current = parent;
        }
        result.Reverse();
        return result;
    }

    /// <summary>같은 폴더인가 (끝 구분자와 Windows·macOS의 대소문자 차이는 무시).</summary>
    public static bool SamePath(string a, string b)
    {
        static string Norm(string p) => p.Length > 1 && Path.GetPathRoot(p) != p
            ? p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : p;
        return string.Equals(Norm(a), Norm(b), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }
}
