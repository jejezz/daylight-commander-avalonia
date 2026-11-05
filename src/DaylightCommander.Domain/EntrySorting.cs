namespace DaylightCommander.Domain;

/// <summary>패널 정렬 기준 (원본 <c>SortField</c>).</summary>
public enum SortField { Name, Size, Modified, Extension }

public static class EntrySorting
{
    static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// 정렬 규칙 (원본 <c>pane_controller.dart</c>의 <c>_sorted</c>).
    /// <list type="bullet">
    /// <item>폴더는 오름/내림과 무관하게 항상 먼저. 방향은 폴더끼리, 파일끼리 각각에 적용된다.</item>
    /// <item>크기: <c>null</c>은 -1. 수정일: 하나라도 <c>null</c>이면 같은 것으로 본다.</item>
    /// <item>확장자: 소문자로 비교. 점이 맨 앞인 이름(<c>.gitignore</c>)은 확장자 없음.</item>
    /// <item>같은 값이면 이름(대소문자 무시) 오름차순으로 고정해 순서가 흔들리지 않게 한다. 정렬은 안정적이다.</item>
    /// </list>
    /// </summary>
    public static List<FileEntry> Sort(IEnumerable<FileEntry> entries, SortField field, bool ascending)
    {
        Comparison<FileEntry> byField = field switch
        {
            SortField.Name => ByName,
            SortField.Size => (a, b) => (a.SizeBytes ?? -1L).CompareTo(b.SizeBytes ?? -1L),
            SortField.Modified => (a, b) =>
                a.ModifiedAt is { } x && b.ModifiedAt is { } y ? x.CompareTo(y) : 0,
            SortField.Extension => (a, b) =>
                NameComparer.Compare(ExtensionOf(a.Name), ExtensionOf(b.Name)),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        Comparison<FileEntry> directed = ascending ? byField : (a, b) => byField(b, a);

        int Compare(FileEntry a, FileEntry b)
        {
            var c = (!a.IsDirectory).CompareTo(!b.IsDirectory); // 폴더 먼저
            if (c != 0) return c;
            c = directed(a, b);
            return c != 0 ? c : ByName(a, b);
        }

        return entries.OrderBy(e => e, Comparer<FileEntry>.Create(Compare)).ToList(); // OrderBy는 안정 정렬
    }

    static int ByName(FileEntry a, FileEntry b) => NameComparer.Compare(a.Name, b.Name);

    /// <summary>마지막 점 뒤의 문자열(소문자). 점이 없거나 맨 앞에만 있으면 빈 문자열.</summary>
    public static string ExtensionOf(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot <= 0 ? "" : name[(dot + 1)..].ToLowerInvariant();
    }
}
