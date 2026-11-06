using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>로컬 디스크를 <see cref="IFileSystem"/>으로 보이게 한 것 (원격과 주고받는 전송의 한쪽 끝).</summary>
public sealed class LocalFileSystem : IFileSystem
{
    public static readonly LocalFileSystem Instance = new();

    const int BufferSize = 256 * 1024;

    public Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct = default) =>
        Task.Run(() => ListLocalDirectory.List(path, ct), ct);

    public Task<FileEntry?> StatAsync(string path, CancellationToken ct = default) => Task.Run<FileEntry?>(() =>
    {
        var name = Locations.NameOf(path);
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            return new FileEntry(path, name, false, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), name.StartsWith('.'));
        }
        if (Directory.Exists(path))
            return new FileEntry(path, name, true, null, new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero), name.StartsWith('.'));
        return null;
    }, ct);

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, useAsync: true));

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new FileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true));

    public Task CreateDirectoryAsync(string path, CancellationToken ct = default) =>
        Task.Run(() => Directory.CreateDirectory(path), ct);

    public Task DeleteAsync(string path, CancellationToken ct = default) =>
        Task.Run(() => FileOperations.DeleteTree(path), ct);

    public Task RenameAsync(string from, string to, CancellationToken ct = default) => Task.Run(() =>
    {
        if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to);
    }, ct);

    public Task SetModifiedAsync(string path, DateTimeOffset modified, CancellationToken ct = default) => Task.Run(() =>
    {
        try
        {
            if (Directory.Exists(path)) Directory.SetLastWriteTimeUtc(path, modified.UtcDateTime);
            else File.SetLastWriteTimeUtc(path, modified.UtcDateTime);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }, ct);
}
