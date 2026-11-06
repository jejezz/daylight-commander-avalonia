using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 원격 파일을 로컬 임시 폴더로 내려받는다 — 보기(F3)·기본 앱으로 열기는 원격 파일을 직접 열 수 없으므로 먼저 받아서 그 사본을 연다.
/// 받은 파일은 <c>임시폴더/DaylightCommander/remote/&lt;번호&gt;/이름</c>에 두고, 오래된 것은 앱이 시작할 때 지운다 (<see cref="CleanOld"/>).
/// </summary>
public static class RemoteFiles
{
    static string Root => Path.Combine(Path.GetTempPath(), "DaylightCommander", "remote");

    public static async Task<string> DownloadToTempAsync(IFileSystem fs, string url, CancellationToken ct = default)
    {
        var dir = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var local = Path.Combine(dir, SafeName(Locations.NameOf(url)));
        try
        {
            await using var input = await fs.OpenReadAsync(url, ct);
            await using var output = new FileStream(local, FileMode.CreateNew, FileAccess.Write, FileShare.None, 256 * 1024, useAsync: true);
            await input.CopyToAsync(output, 256 * 1024, ct);
        }
        catch
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        return local;
    }

    /// <summary>파일 이름으로 못 쓰는 글자를 바꾼다 (서버의 이름이 로컬 규칙에 안 맞을 수 있다).</summary>
    internal static string SafeName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "file" : cleaned;
    }

    /// <summary>하루 넘은 임시 사본을 지운다 (열어 둔 파일은 지워지지 않으면 그냥 둔다).</summary>
    public static void CleanOld()
    {
        try
        {
            if (!Directory.Exists(Root)) return;
            foreach (var dir in Directory.EnumerateDirectories(Root))
            {
                if (DateTime.UtcNow - Directory.GetCreationTimeUtc(dir) < TimeSpan.FromDays(1)) continue;
                try { Directory.Delete(dir, recursive: true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
