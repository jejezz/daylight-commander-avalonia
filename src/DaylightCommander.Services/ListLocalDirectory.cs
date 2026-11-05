using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 로컬 디렉터리 목록을 읽어 <see cref="FileEntry"/> 리스트로 변환한다 (원본 <c>ListLocalDirectory</c>).
/// <para>
/// 폴더가 먼저, 그다음 이름 순(대소문자 무시). 속성을 읽지 못하는 항목(깨진 링크, 권한 없음)은 건너뛴다.
/// 디렉터리 자체를 열 수 없으면 <see cref="IOException"/>이 그대로 올라간다
/// (없음: <see cref="DirectoryNotFoundException"/>, 권한 없음: <see cref="UnauthorizedAccessException"/>).
/// </para>
/// 원본(dart:io)과 한 가지 다르다: 심볼릭 링크가 폴더를 가리키면 폴더로 취급한다.
/// </summary>
public sealed class ListLocalDirectory
{
    public Task<IReadOnlyList<FileEntry>> InvokeAsync(string directoryPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => List(directoryPath, cancellationToken), cancellationToken);

    internal static IReadOnlyList<FileEntry> List(string directoryPath, CancellationToken cancellationToken = default)
    {
        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists) throw new DirectoryNotFoundException($"찾을 수 없는 폴더: {directoryPath}");

        var entries = new List<FileEntry>();
        foreach (var info in directory.EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = ToEntry(info);
            if (entry is not null) entries.Add(entry);
        }
        return EntrySorting.Sort(entries, SortField.Name, ascending: true);
    }

    static FileEntry? ToEntry(FileSystemInfo info)
    {
        try
        {
            // 깨진 심볼릭 링크는 건너뛴다.
            if (info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { Exists: false })
                return null;

            var isDirectory = info.Attributes.HasFlag(FileAttributes.Directory);
            return new FileEntry(
                Path: info.FullName,
                Name: info.Name,
                IsDirectory: isDirectory,
                SizeBytes: isDirectory ? null : ((FileInfo)info).Length,
                ModifiedAt: new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                IsHidden: info.Name.StartsWith('.'));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null; // 속성을 읽지 못하는 항목은 목록에서 뺀다
        }
    }
}
