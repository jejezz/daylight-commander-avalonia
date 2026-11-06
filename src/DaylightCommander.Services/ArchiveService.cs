using System.IO.Compression;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 압축(zip 만들기)과 압축 풀기 (원본 <c>ArchiveService</c>). zip만 지원한다. 표준 라이브러리(System.IO.Compression)만 쓰므로
/// NativeAOT에서도 그대로 동작한다. 압축 풀기는 경로 탈출(zip slip)을 라이브러리가 막는다.
/// </summary>
public sealed class ArchiveService
{
    public static bool IsZip(string name) => name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>기본 압축 파일 이름: 항목이 하나면 "이름.zip", 여럿이면 "archive.zip".</summary>
    public static string DefaultZipName(IReadOnlyList<string> sourcePaths) =>
        sourcePaths.Count == 1 ? Path.GetFileName(sourcePaths[0].TrimEnd('/', '\\')) + ".zip" : "archive.zip";

    /// <summary>풀 폴더 이름: zip 이름에서 확장자를 뗀 것.</summary>
    public static string ExtractFolderName(string zipName) => Path.GetFileNameWithoutExtension(zipName);

    /// <summary>
    /// <paramref name="sources"/>(파일·폴더)를 <paramref name="zipPath"/> 하나로 묶는다. 폴더는 폴더 이름부터 담는다.
    /// 이미 있는 파일이면 <see cref="FileError.AlreadyExists"/>. 실패하면 만들다 만 zip은 지운다.
    /// </summary>
    public Task CompressAsync(IReadOnlyList<string> sources, string zipPath, CancellationToken ct = default) => Task.Run(() =>
    {
        var zipName = Path.GetFileName(zipPath);
        FileOperations.ValidateName(zipName);
        if (File.Exists(zipPath) || Directory.Exists(zipPath)) throw new FileOperationException(new FileError.AlreadyExists(zipName));

        try
        {
            using var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
            var zipFull = Path.GetFullPath(zipPath);
            foreach (var source in sources)
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(source.TrimEnd('/', '\\'));
                if (Directory.Exists(source)) AddDirectory(zip, source, name, zipFull, ct);
                else if (File.Exists(source)) AddFile(zip, source, name, zipFull);
            }
        }
        catch
        {
            try { File.Delete(zipPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }, ct);

    static void AddDirectory(ZipArchive zip, string dir, string entryName, string zipFull, CancellationToken ct)
    {
        var children = Directory.EnumerateFileSystemEntries(dir).ToList();
        if (children.Count == 0) zip.CreateEntry(entryName + "/"); // 빈 폴더도 남긴다
        foreach (var child in children)
        {
            ct.ThrowIfCancellationRequested();
            var childName = entryName + "/" + Path.GetFileName(child);
            if (Directory.Exists(child))
            {
                if (new DirectoryInfo(child).LinkTarget is not null) continue; // 링크를 따라 들어가 무한 반복하지 않는다
                AddDirectory(zip, child, childName, zipFull, ct);
            }
            else AddFile(zip, child, childName, zipFull);
        }
    }

    static void AddFile(ZipArchive zip, string file, string entryName, string zipFull)
    {
        if (string.Equals(Path.GetFullPath(file), zipFull, StringComparison.OrdinalIgnoreCase)) return; // 만드는 zip 자신
        zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
    }

    /// <summary>
    /// <paramref name="zipPath"/>를 <paramref name="destinationDir"/>에 푼다 (없으면 만든다). 이미 있는 파일은 덮어쓰지 않고
    /// <see cref="FileError.AlreadyExists"/>로 멈춘다. 풀린 폴더 경로를 돌려준다.
    /// </summary>
    public Task<string> ExtractAsync(string zipPath, string destinationDir, CancellationToken ct = default) => Task.Run(() =>
    {
        var full = Path.GetFullPath(destinationDir);
        var root = full.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.GetFullPath(Path.Combine(full, entry.FullName));
            if (!target.StartsWith(root, StringComparison.Ordinal) && target != full)
                throw new FileOperationException(new FileError.Other(entry.FullName)); // 밖으로 빠져나가는 경로
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target)) throw new FileOperationException(new FileError.AlreadyExists(entry.FullName));
            entry.ExtractToFile(target);
        }
        Directory.CreateDirectory(full); // 빈 zip이어도 폴더는 만든다
        return full;
    }, ct);
}
