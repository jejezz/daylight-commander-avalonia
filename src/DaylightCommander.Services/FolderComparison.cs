using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>비교 결과: 이쪽에만 있다 / 양쪽에 있지만 내용이 다르다 (같은 것은 결과에 넣지 않는다).</summary>
public enum FileDiffStatus { OnlyHere, Differs }

/// <summary>좌우 폴더를 이름으로 비교한 결과. 키는 항목의 전체 경로.</summary>
public sealed record FolderComparison(
    IReadOnlyDictionary<string, FileDiffStatus> Left,
    IReadOnlyDictionary<string, FileDiffStatus> Right)
{
    static readonly StringComparer Names = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// 이름 기준 비교 (원본 <c>compareFolders</c>). 한쪽에만 있으면 <see cref="FileDiffStatus.OnlyHere"/>, 양쪽에 있는 <b>파일</b>은 크기나
    /// 수정 시각이 다르면 <see cref="FileDiffStatus.Differs"/>. 폴더는 양쪽에 있으면 같다고 본다(안쪽은 보지 않는다).
    /// Windows·macOS는 이름의 대소문자를 구분하지 않는다.
    /// </summary>
    public static FolderComparison Compare(IReadOnlyList<FileEntry> left, IReadOnlyList<FileEntry> right)
    {
        var leftByName = ByName(left);
        var rightByName = ByName(right);
        return new FolderComparison(Diff(leftByName, rightByName), Diff(rightByName, leftByName));
    }

    /// <summary>양쪽에 다 있지만 내용이 다른 파일을 이름으로 짝지은 것 (양방향 동기화에서 하나씩 물어볼 대상).</summary>
    public static IReadOnlyList<(FileEntry Left, FileEntry Right)> DiffPairs(IReadOnlyList<FileEntry> left, IReadOnlyList<FileEntry> right)
    {
        var rightByName = ByName(right);
        var pairs = new List<(FileEntry, FileEntry)>();
        foreach (var entry in left)
            if (rightByName.TryGetValue(entry.Name, out var other) && ContentDiffers(entry, other)) pairs.Add((entry, other));
        return pairs;
    }

    static Dictionary<string, FileEntry> ByName(IReadOnlyList<FileEntry> entries)
    {
        var map = new Dictionary<string, FileEntry>(Names);
        foreach (var e in entries) map[e.Name] = e;
        return map;
    }

    static Dictionary<string, FileDiffStatus> Diff(Dictionary<string, FileEntry> here, Dictionary<string, FileEntry> there)
    {
        var result = new Dictionary<string, FileDiffStatus>(StringComparer.Ordinal);
        foreach (var entry in here.Values)
        {
            if (!there.TryGetValue(entry.Name, out var other)) result[entry.Path] = FileDiffStatus.OnlyHere;
            else if (ContentDiffers(entry, other)) result[entry.Path] = FileDiffStatus.Differs;
        }
        return result;
    }

    static bool ContentDiffers(FileEntry a, FileEntry b) =>
        !a.IsDirectory && !b.IsDirectory &&
        (a.SizeBytes != b.SizeBytes || a.ModifiedAt?.ToUnixTimeMilliseconds() != b.ModifiedAt?.ToUnixTimeMilliseconds());
}
