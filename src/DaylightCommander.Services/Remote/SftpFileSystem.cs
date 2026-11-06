using DaylightCommander.Domain;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DaylightCommander.Services;

/// <summary>
/// SFTP 서버 하나(연결 하나)를 <see cref="IFileSystem"/>으로 보이게 한 것. SSH.NET의 동기 호출을 작업 스레드에서 부르고, 한 번에 하나씩만
/// 보낸다 (한 연결을 여러 작업이 나눠 쓰므로). SSH.NET 예외는 여기서 <see cref="FileOperationException"/>으로 바꿔 위로 올린다 —
/// 서비스 위쪽은 SSH.NET을 모른다.
/// </summary>
public sealed class SftpFileSystem(SftpClient client, string authority) : IFileSystem, IDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsConnected => client.IsConnected;

    /// <summary>로그인 직후의 작업 폴더 (서버 안의 경로).</summary>
    public string WorkingDirectory => client.WorkingDirectory;

    string ServerPath(string path) => Locations.TryParse(path)?.Path ?? throw new FileOperationException(new FileError.Other(path));

    string UrlFor(string serverPath) =>
        Locations.TryParse(authority + "/") is { } loc ? Locations.Format(loc with { Path = Locations.NormalizeServerPath(serverPath) }) : serverPath;

    /// <summary>한 번에 하나씩, 작업 스레드에서 부른다. SSH.NET 예외를 앱의 오류로 바꾼다.</summary>
    async Task<T> Run<T>(Func<T> work, CancellationToken ct, string? name = null)
    {
        await _gate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                try
                {
                    return work();
                }
                catch (SftpPathNotFoundException)
                {
                    throw new FileOperationException(new FileError.NotFound(name ?? ""));
                }
                catch (SftpPermissionDeniedException)
                {
                    throw new FileOperationException(new FileError.AccessDenied(name ?? ""));
                }
                catch (Exception e) when (e is SshConnectionException or SshOperationTimeoutException or ObjectDisposedException)
                {
                    throw new FileOperationException(new FileError.RemoteNotConnected(authority));
                }
                catch (InvalidOperationException e) when (e.Message.Contains("not connected", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FileOperationException(new FileError.RemoteNotConnected(authority));
                }
                catch (SshException e)
                {
                    throw new FileOperationException(new FileError.Other(e.Message));
                }
            }, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct = default) => Run<IReadOnlyList<FileEntry>>(() =>
    {
        var entries = new List<FileEntry>();
        foreach (var item in client.ListDirectory(ServerPath(path)))
        {
            if (item.Name is "." or "..") continue;
            ct.ThrowIfCancellationRequested();
            entries.Add(ToEntry(item));
        }
        return EntrySorting.Sort(entries, SortField.Name, ascending: true);
    }, ct, Locations.NameOf(path));

    FileEntry ToEntry(Renci.SshNet.Sftp.ISftpFile item) => new(
        Path: UrlFor(item.FullName),
        Name: item.Name,
        IsDirectory: item.IsDirectory,
        SizeBytes: item.IsDirectory ? null : item.Length,
        ModifiedAt: new DateTimeOffset(item.LastWriteTimeUtc, TimeSpan.Zero),
        IsHidden: item.Name.StartsWith('.'));

    public Task<FileEntry?> StatAsync(string path, CancellationToken ct = default) => Run<FileEntry?>(() =>
    {
        var server = ServerPath(path);
        if (!client.Exists(server)) return null;
        return ToEntry(client.Get(server));
    }, ct, Locations.NameOf(path));

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
        Run<Stream>(() => client.OpenRead(ServerPath(path)), ct, Locations.NameOf(path));

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct = default) =>
        Run<Stream>(() => client.Open(ServerPath(path), overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write), ct, Locations.NameOf(path));

    public Task CreateDirectoryAsync(string path, CancellationToken ct = default) =>
        Run(() => { client.CreateDirectory(ServerPath(path)); return 0; }, ct, Locations.NameOf(path));

    public Task DeleteAsync(string path, CancellationToken ct = default) =>
        Run(() => { DeleteRecursive(ServerPath(path), ct); return 0; }, ct, Locations.NameOf(path));

    void DeleteRecursive(string serverPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var item = client.Get(serverPath);
        if (item.IsDirectory && !item.IsSymbolicLink)
        {
            foreach (var child in client.ListDirectory(serverPath))
                if (child.Name is not ("." or "..")) DeleteRecursive(child.FullName, ct);
            client.DeleteDirectory(serverPath);
        }
        else
        {
            client.DeleteFile(serverPath);
        }
    }

    public Task RenameAsync(string from, string to, CancellationToken ct = default) =>
        Run(() => { client.RenameFile(ServerPath(from), ServerPath(to)); return 0; }, ct, Locations.NameOf(from));

    public Task SetModifiedAsync(string path, DateTimeOffset modified, CancellationToken ct = default) => Run(() =>
    {
        try { client.SetLastWriteTimeUtc(ServerPath(path), modified.UtcDateTime); }
        catch (SshException) { } // 서버가 거부해도 복사 자체는 성공이다
        return 0;
    }, ct, Locations.NameOf(path));

    public void Dispose()
    {
        try { if (client.IsConnected) client.Disconnect(); } catch (Exception e) when (e is SshException or ObjectDisposedException) { }
        client.Dispose();
        _gate.Dispose();
    }
}
