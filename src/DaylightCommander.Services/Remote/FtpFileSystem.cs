using System.Net.Sockets;
using DaylightCommander.Domain;
using FluentFTP;
using FluentFTP.Exceptions;

namespace DaylightCommander.Services;

/// <summary>
/// FTP 서버 하나(연결 하나)를 <see cref="IFileSystem"/>으로 보이게 한 것 (FluentFTP). FTP는 연결 하나로 한 번에 하나의 데이터 전송만
/// 할 수 있으므로, 읽기·쓰기는 임시 파일을 거친다: 읽을 때는 먼저 통째로 받아 임시 파일을 열어 주고, 쓸 때는 임시 파일에 쓰다가 스트림을
/// 닫을 때(<c>DisposeAsync</c>) 서버로 올린다. 모든 호출은 한 번에 하나씩만 보낸다. FluentFTP 예외는 여기서 앱의 오류로 바꿔 올린다.
/// </summary>
public sealed class FtpFileSystem(AsyncFtpClient client, string authority) : IFileSystem, IDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);

    AsyncFtpClient Client => client;

    public bool IsConnected => client.IsConnected;

    /// <summary>로그인 직후의 작업 폴더.</summary>
    public async Task<string> GetWorkingDirectoryAsync(CancellationToken ct = default) =>
        await Run(async () => await client.GetWorkingDirectory(ct), ct, "");

    string ServerPath(string path) => Locations.TryParse(path)?.Path ?? throw new FileOperationException(new FileError.Other(path));

    string UrlFor(string serverPath) =>
        Locations.TryParse(authority + "/") is { } loc ? Locations.Format(loc with { Path = Locations.NormalizeServerPath(serverPath) }) : serverPath;

    async Task<T> Run<T>(Func<Task<T>> work, CancellationToken ct, string name)
    {
        await _gate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await work();
            }
            catch (FtpMissingObjectException)
            {
                throw new FileOperationException(new FileError.NotFound(name));
            }
            catch (FtpCommandException e) when (e.CompletionCode is { Length: > 0 } code && code[0] == '5')
            {
                var denied = e.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("permission", StringComparison.OrdinalIgnoreCase);
                throw new FileOperationException(denied ? new FileError.AccessDenied(name) : new FileError.NotFound(name));
            }
            catch (FtpAuthenticationException)
            {
                throw new FileOperationException(new FileError.RemoteAuthFailed());
            }
            catch (Exception e) when (e is FtpException or SocketException or TimeoutException or ObjectDisposedException)
            {
                // 연결이 끊겼거나 서버가 응답하지 않는다.
                throw new FileOperationException(client.IsConnected ? new FileError.Other(e.Message) : new FileError.RemoteNotConnected(authority));
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    FileEntry ToEntry(FtpListItem item)
    {
        var isDir = item.Type == FtpObjectType.Directory;
        var modified = item.Modified == DateTime.MinValue ? (DateTimeOffset?)null : new DateTimeOffset(item.Modified.ToUniversalTime(), TimeSpan.Zero);
        return new FileEntry(UrlFor(item.FullName), item.Name, isDir, isDir || item.Size < 0 ? null : item.Size, modified, item.Name.StartsWith('.'));
    }

    public Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct = default) =>
        Run<IReadOnlyList<FileEntry>>(async () =>
        {
            var items = await client.GetListing(ServerPath(path), FtpListOption.Auto, ct);
            var entries = items.Where(i => i.Name is not ("." or "..") && i.Type is FtpObjectType.File or FtpObjectType.Directory or FtpObjectType.Link).Select(ToEntry).ToList();
            return EntrySorting.Sort(entries, SortField.Name, ascending: true);
        }, ct, Locations.NameOf(path));

    public Task<FileEntry?> StatAsync(string path, CancellationToken ct = default) =>
        Run<FileEntry?>(async () =>
        {
            var server = ServerPath(path);
            if (server == "/") return new FileEntry(UrlFor("/"), authority, true);
            var item = await client.GetObjectInfo(server, true, ct);
            return item is null ? null : ToEntry(item);
        }, ct, Locations.NameOf(path));

    /// <summary>임시 파일에 통째로 받아 읽기용 스트림으로 준다 (스트림을 닫으면 임시 파일도 지워진다).</summary>
    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
        Run<Stream>(async () =>
        {
            var temp = Path.Combine(Path.GetTempPath(), "dc-ftp-" + Guid.NewGuid().ToString("N"));
            try
            {
                var status = await client.DownloadFile(temp, ServerPath(path), FtpLocalExists.Overwrite, FtpVerify.None, null, ct);
                if (status == FtpStatus.Failed) throw new FileOperationException(new FileError.NotFound(Locations.NameOf(path)));
                return new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            }
            catch
            {
                try { File.Delete(temp); } catch (IOException) { }
                throw;
            }
        }, ct, Locations.NameOf(path));

    public async Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct = default)
    {
        if (!overwrite && await StatAsync(path, ct) is not null) throw new FileOperationException(new FileError.AlreadyExists(Locations.NameOf(path)));
        return new UploadOnCloseStream(this, path, ct);
    }

    /// <summary>쓰는 동안은 임시 파일이고, 닫을 때 서버로 올린 뒤 지운다.</summary>
    sealed class UploadOnCloseStream : FileStream
    {
        readonly FtpFileSystem _owner;
        readonly string _remote;
        readonly string _temp;
        readonly CancellationToken _ct;
        bool _closingAsync; // DisposeAsync 안에서 base가 동기 Dispose를 부르기도 하므로, 그때는 임시 파일을 지우지 않는다 (올리기 전이다)

        public UploadOnCloseStream(FtpFileSystem owner, string remote, CancellationToken ct)
            : this(owner, remote, ct, Path.Combine(Path.GetTempPath(), "dc-ftp-" + Guid.NewGuid().ToString("N"))) { }

        UploadOnCloseStream(FtpFileSystem owner, string remote, CancellationToken ct, string temp)
            : base(temp, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 256 * 1024, FileOptions.Asynchronous)
        {
            _owner = owner;
            _remote = remote;
            _temp = temp;
            _ct = ct;
        }

        public override async ValueTask DisposeAsync()
        {
            _closingAsync = true;
            await base.DisposeAsync();
            try
            {
                var status = await _owner.Run<FtpStatus>(async () => await _owner.Client.UploadFile(_temp, _owner.ServerPath(_remote), FtpRemoteExists.Overwrite, false, FtpVerify.None, null, _ct), _ct, Locations.NameOf(_remote));
                if (status == FtpStatus.Failed) throw new FileOperationException(new FileError.Other($"upload failed: {Locations.NameOf(_remote)}"));
            }
            finally
            {
                try { File.Delete(_temp); } catch (IOException) { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            // 동기로 닫으면 올릴 수 없다 (await using / DisposeAsync로 닫아야 한다).
            base.Dispose(disposing);
            if (disposing && !_closingAsync) try { File.Delete(_temp); } catch (IOException) { }
        }
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct = default) =>
        Run(async () => await client.CreateDirectory(ServerPath(path), false, ct), ct, Locations.NameOf(path));

    public Task DeleteAsync(string path, CancellationToken ct = default) =>
        Run(async () =>
        {
            var server = ServerPath(path);
            var item = await client.GetObjectInfo(server, true, ct) ?? throw new FileOperationException(new FileError.NotFound(Locations.NameOf(path)));
            if (item.Type == FtpObjectType.Directory) await client.DeleteDirectory(server, FtpListOption.Auto, ct);
            else await client.DeleteFile(server, ct);
            return true;
        }, ct, Locations.NameOf(path));

    public Task RenameAsync(string from, string to, CancellationToken ct = default) =>
        Run(async () => { await client.Rename(ServerPath(from), ServerPath(to), ct); return true; }, ct, Locations.NameOf(from));

    public Task SetModifiedAsync(string path, DateTimeOffset modified, CancellationToken ct = default) =>
        Run(async () =>
        {
            // FluentFTP는 클라이언트 현지 시각을 받아 서버(UTC)로 바꿔 보낸다.
            try { await client.SetModifiedTime(ServerPath(path), modified.LocalDateTime, ct); }
            catch (FtpException) { } // 서버가 수정 시각 설정(MFMT)을 지원하지 않아도 복사 자체는 성공이다
            return true;
        }, ct, Locations.NameOf(path));

    public void Dispose()
    {
        try { if (client.IsConnected) client.Disconnect().GetAwaiter().GetResult(); } catch (Exception e) when (e is FtpException or SocketException or IOException) { }
        client.Dispose();
        _gate.Dispose();
    }
}
