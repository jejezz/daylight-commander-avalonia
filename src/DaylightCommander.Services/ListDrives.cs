using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 현재 OS에서 전환 가능한 드라이브/볼륨 목록 (원본 <c>ListDrives</c>).
/// Windows는 드라이브 이름만 읽고(<see cref="Directory.GetLogicalDrives"/>) 드라이브에 접근하지 않는다 — 연결이 끊긴
/// 네트워크 드라이브가 있어도 수 초씩 멈추지 않는다. 실제 접근 가능 여부는 그 드라이브로 이동할 때 확인한다.
/// macOS는 <c>/Volumes</c> 아래에 외장 디스크와 마운트된 SMB 공유가 함께 잡힌다.
/// </summary>
public sealed class ListDrives
{
    public IReadOnlyList<DriveEntry> Invoke()
    {
        if (OperatingSystem.IsWindows()) return WindowsDrives();
        if (OperatingSystem.IsMacOS()) return MacDrives();
        return LinuxDrives();
    }

    static IReadOnlyList<DriveEntry> WindowsDrives()
    {
        var drives = new List<DriveEntry>();
        foreach (var root in Directory.GetLogicalDrives())
        {
            DriveKind kind;
            try
            {
                // DriveType은 드라이브 종류 정보만 읽는다 (IsReady·TotalSize와 달리 디스크에 접근하지 않는다).
                kind = new DriveInfo(root).DriveType switch
                {
                    System.IO.DriveType.Fixed => DriveKind.Fixed,
                    System.IO.DriveType.Removable => DriveKind.Removable,
                    System.IO.DriveType.Network => DriveKind.Network,
                    _ => DriveKind.Unknown,
                };
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
            {
                kind = DriveKind.Unknown;
            }
            drives.Add(new DriveEntry(root, root, kind));
        }
        return drives;
    }

    static IReadOnlyList<DriveEntry> MacDrives()
    {
        var drives = new List<DriveEntry> { new("Macintosh HD", "/") };
        AddChildren(drives, "/Volumes");
        return drives;
    }

    static IReadOnlyList<DriveEntry> LinuxDrives()
    {
        var drives = new List<DriveEntry> { new("/", "/") };
        var user = Environment.UserName;
        foreach (var baseDir in new[] { $"/media/{user}", "/mnt", $"/run/media/{user}" }) AddChildren(drives, baseDir);
        return drives;
    }

    static void AddChildren(List<DriveEntry> drives, string baseDir)
    {
        try
        {
            if (!Directory.Exists(baseDir)) return;
            foreach (var dir in Directory.EnumerateDirectories(baseDir))
                drives.Add(new DriveEntry(Path.GetFileName(dir), dir));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 읽을 수 없는 마운트 위치는 건너뛴다.
        }
    }

    /// <summary>
    /// <paramref name="path"/>가 속한 드라이브. Windows는 경로의 루트(<c>C:\</c>), 그 밖에는 목록 중 가장 길게 일치하는 마운트 경로.
    /// 목록에 없으면 null.
    /// </summary>
    public static DriveEntry? DriveOf(string path, IReadOnlyList<DriveEntry> drives)
    {
        if (OperatingSystem.IsWindows())
        {
            var root = Path.GetPathRoot(path);
            return drives.FirstOrDefault(d => string.Equals(d.Path, root, StringComparison.OrdinalIgnoreCase));
        }
        return drives
            .Where(d => path == d.Path || path.StartsWith(d.Path.TrimEnd('/') + "/", StringComparison.Ordinal))
            .OrderByDescending(d => d.Path.Length)
            .FirstOrDefault();
    }
}
