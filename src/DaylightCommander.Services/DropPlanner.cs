using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>끌어다 놓기(드래그 앤 드롭)로 복사할지 이동할지 사용자가 정했는가. Ctrl은 복사, Shift는 이동 (탐색기와 같다).</summary>
public enum DropMode { Auto, ForceCopy, ForceMove }

public enum DropAction { None, Copy, Move, Error }

/// <summary>드롭을 어떻게 처리할지의 결정. <see cref="Action"/>이 <see cref="DropAction.Error"/>면 <see cref="Error"/>가 있다.</summary>
public sealed record DropPlan(DropAction Action, FileError? Error = null)
{
    public static readonly DropPlan None = new(DropAction.None);
}

/// <summary>
/// 드롭 규칙 (원본 <c>transferEntries</c>): 기본은 <b>같은 드라이브면 이동, 다르면 복사</b>. 이미 그 폴더에 있는 항목을 같은
/// 폴더에 놓으면 이동은 할 일이 없고(무시), 복사는 "이름 (1)" 복제본을 만든다(충돌 질문 없이). 폴더를 자기 자신이나 그 안쪽에
/// 놓으면 오류. 화면과 무관한 순수 함수라 테스트할 수 있다.
/// </summary>
public static class DropPlanner
{
    public static DropPlan Plan(IReadOnlyList<string> sources, string destinationDir, IReadOnlyList<DriveEntry> drives, DropMode mode = DropMode.Auto)
    {
        if (sources.Count == 0) return DropPlan.None;

        var isMove = mode switch
        {
            DropMode.ForceCopy => false,
            DropMode.ForceMove => true,
            _ => SameDrive(sources[0], destinationDir, drives),
        };

        var dest = Full(destinationDir);
        foreach (var source in sources)
        {
            var src = Full(source);
            if (Equal(Parent(src), dest))
            {
                if (isMove) return DropPlan.None; // 이미 그 위치에 있다
                continue;                          // 복사: 같은 폴더에 복제본
            }
            // 원격 항목은 폴더인지 지금 알 수 없다: 놓을 곳이 자기 자신·안쪽이면 (파일은 그럴 수 없으므로) 폴더로 보고 막는다.
            if ((Locations.IsRemote(source) || Directory.Exists(source)) && (Equal(dest, src) || Within(src, dest)))
                return new DropPlan(DropAction.Error, new FileError.IntoItself(Name(src)));
        }
        return new DropPlan(isMove ? DropAction.Move : DropAction.Copy);
    }

    public static bool SameDrive(string a, string b, IReadOnlyList<DriveEntry> drives)
    {
        // 원격은 같은 서버(같은 계정)면 같은 곳으로 본다 — 서버 안에서는 이동, 로컬↔원격이나 다른 서버는 복사.
        if (Locations.IsRemote(a) || Locations.IsRemote(b)) return Locations.SameServer(a, b);
        var da = ListDrives.DriveOf(a, drives);
        var db = ListDrives.DriveOf(b, drives);
        // 어느 쪽이든 드라이브를 못 찾으면(네트워크 경로 등) 같은 곳이라고 단정하지 않는다 → 복사.
        return da is not null && db is not null && da.Path == db.Path;
    }

    static StringComparison Comparison => OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    static StringComparison ComparisonFor(string path) => Locations.IsRemote(path) ? StringComparison.Ordinal : Comparison;

    static string Full(string path)
    {
        if (Locations.IsRemote(path)) return Locations.NormalizeRemote(path);
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        return full == root ? full : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    static string Parent(string path) => Locations.Parent(path) ?? path;

    static bool Equal(string a, string b) => string.Equals(a, b, ComparisonFor(a));

    /// <summary><paramref name="inner"/>가 <paramref name="outer"/> 폴더의 안쪽(하위)인가.</summary>
    static bool Within(string outer, string inner)
    {
        var sep = Locations.IsRemote(outer) ? '/' : Path.DirectorySeparatorChar;
        return inner.StartsWith(outer.TrimEnd(sep) + sep, ComparisonFor(outer));
    }

    static string Name(string path) => Locations.NameOf(path);
}
