using System.Net.Sockets;
using DaylightCommander.Domain;
using Renci.SshNet;
using FluentFTP;
using FluentFTP.Exceptions;
using Renci.SshNet.Common;

namespace DaylightCommander.Services;

/// <summary>연결 요청. 비밀번호는 연결하는 순간에만 쓰고 어디에도 저장하지 않는다.</summary>
public sealed record RemoteConnectRequest(string Scheme, string Host, int Port, string User, string Password)
{
    /// <summary>이 서버의 열쇠 (<c>sftp://user@host[:port]</c>).</summary>
    public string Authority => Locations.FormatAuthority(Scheme, User, Host, Port);
}

/// <summary>서버의 호스트 키 지문을 기억해 두었다가 다음 접속 때 같은지 본다 (처음 보는 서버는 믿고 기억한다 — SSH의 TOFU).</summary>
public interface IHostKeyStore
{
    string? Get(string hostPort);
    void Set(string hostPort, string fingerprint);
}

/// <summary>호스트 키가 전에 본 것과 다르다 (서버가 바뀌었거나 중간에서 가로채는 중일 수 있다).</summary>
public sealed class HostKeyChangedException(string hostPort) : IOException($"host key changed: {hostPort}")
{
    public string HostPort { get; } = hostPort;
}

/// <summary>
/// 연결된 원격 서버들 (원본 <c>*SessionManager</c>). 서버(스킴·계정·호스트·포트)마다 연결 하나를 두고 여러 창·작업이 나눠 쓴다.
/// 연결은 <see cref="ConnectAsync"/>로 하고, 경로로 <see cref="FileSystemFor"/>를 부르면 그 서버의 <see cref="IFileSystem"/>을 준다.
/// 연결하지 않은 서버의 경로면 <see cref="FileError.RemoteNotConnected"/>.
/// </summary>
public sealed class RemoteSessions : IDisposable
{
    public delegate Task<IFileSystem> Connector(RemoteConnectRequest request, CancellationToken ct);

    readonly Connector _connector;
    readonly object _gate = new();
    readonly Dictionary<string, IFileSystem> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="connector">테스트용으로 바꿀 수 있다. 기본은 SFTP.</param>
    public RemoteSessions(IHostKeyStore? hostKeys = null, Connector? connector = null)
    {
        _connector = connector ?? ((request, ct) => request.Scheme == "ftp" ? ConnectFtpAsync(request, ct) : ConnectSftpAsync(request, hostKeys, ct));
    }

    /// <summary>연결·해제가 있었다.</summary>
    public event Action? Changed;

    public IReadOnlyList<string> Connected { get { lock (_gate) return [.. _sessions.Keys]; } }

    public bool IsConnected(string pathOrAuthority)
    {
        var key = KeyOf(pathOrAuthority);
        lock (_gate) return key is not null && _sessions.ContainsKey(key);
    }

    static string? KeyOf(string pathOrAuthority) => Locations.TryParse(pathOrAuthority)?.Authority;

    /// <summary>
    /// 서버에 연결하고, 로그인 직후의 폴더(홈)의 전체 경로를 돌려준다. 이미 연결된 서버면 연결을 새로 한다.
    /// 실패는 <see cref="FileOperationException"/> (<see cref="FileError.RemoteAuthFailed"/> / <see cref="FileError.RemoteConnectFailed"/>).
    /// </summary>
    public async Task<string> ConnectAsync(RemoteConnectRequest request, CancellationToken ct = default)
    {
        var fs = await _connector(request, ct);
        IFileSystem? old;
        lock (_gate)
        {
            _sessions.TryGetValue(request.Authority, out old);
            _sessions[request.Authority] = fs;
        }
        (old as IDisposable)?.Dispose();
        Changed?.Invoke();

        var home = fs switch
        {
            SftpFileSystem sftp => sftp.WorkingDirectory,
            FtpFileSystem ftp => await ftp.GetWorkingDirectoryAsync(ct),
            _ => "/",
        };
        return Locations.Format(new RemoteLocation(request.Scheme, request.User, request.Host, request.Port, Locations.NormalizeServerPath(home)));
    }

    public IFileSystem FileSystemFor(string path)
    {
        var loc = Locations.TryParse(path) ?? throw new FileOperationException(new FileError.Other(path));
        lock (_gate)
        {
            if (_sessions.TryGetValue(loc.Authority, out var fs) && fs is not (SftpFileSystem { IsConnected: false } or FtpFileSystem { IsConnected: false })) return fs;
        }
        throw new FileOperationException(new FileError.RemoteNotConnected(loc.Authority));
    }

    public void Disconnect(string pathOrAuthority)
    {
        var key = KeyOf(pathOrAuthority);
        IFileSystem? fs = null;
        lock (_gate)
        {
            if (key is not null && _sessions.Remove(key, out var found)) fs = found;
        }
        if (fs is null) return;
        (fs as IDisposable)?.Dispose();
        Changed?.Invoke();
    }

    public void Dispose()
    {
        List<IFileSystem> all;
        lock (_gate)
        {
            all = [.. _sessions.Values];
            _sessions.Clear();
        }
        foreach (var fs in all) (fs as IDisposable)?.Dispose();
    }

    // --- FTP ---

    static async Task<IFileSystem> ConnectFtpAsync(RemoteConnectRequest request, CancellationToken ct)
    {
        var client = new AsyncFtpClient(request.Host, request.User, request.Password, request.Port);
        client.Config.ConnectTimeout = 15000;
        client.Config.DataConnectionConnectTimeout = 15000;
        client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
        client.Config.EncryptionMode = FtpEncryptionMode.None;
        client.Config.DownloadDataType = FtpDataType.Binary;
        client.Config.UploadDataType = FtpDataType.Binary;
        if (Environment.GetEnvironmentVariable("DC_FTP_LOG") == "1") client.Config.LogToConsole = true; // 문제 조사용
        try
        {
            await client.Connect(ct);
            return new FtpFileSystem(client, request.Authority);
        }
        catch (Exception e)
        {
            client.Dispose();
            throw e switch
            {
                OperationCanceledException => e,
                FtpAuthenticationException => new FileOperationException(new FileError.RemoteAuthFailed()),
                FtpCommandException { CompletionCode: "530" } => new FileOperationException(new FileError.RemoteAuthFailed()),
                FtpException or SocketException or TimeoutException or IOException => new FileOperationException(new FileError.RemoteConnectFailed(e.Message)),
                _ => e,
            };
        }
    }

    // --- SFTP ---

    static Task<IFileSystem> ConnectSftpAsync(RemoteConnectRequest request, IHostKeyStore? hostKeys, CancellationToken ct) => Task.Run<IFileSystem>(() =>
    {
        var info = new ConnectionInfo(request.Host, request.Port, request.User, new PasswordAuthenticationMethod(request.User, request.Password))
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        var client = new SftpClient(info);
        var hostPort = $"{request.Host.ToLowerInvariant()}:{request.Port}";
        HostKeyChangedException? changed = null;
        client.HostKeyReceived += (_, e) =>
        {
            var fingerprint = e.FingerPrintSHA256;
            var known = hostKeys?.Get(hostPort);
            if (known is null) hostKeys?.Set(hostPort, fingerprint); // 처음 보는 서버: 믿고 기억한다
            else if (known != fingerprint)
            {
                changed = new HostKeyChangedException(hostPort);
                e.CanTrust = false;
            }
        };
        try
        {
            ct.ThrowIfCancellationRequested();
            client.Connect();
            return new SftpFileSystem(client, request.Authority);
        }
        catch (Exception e)
        {
            client.Dispose();
            if (changed is not null) throw new FileOperationException(new FileError.RemoteHostKeyChanged(hostPort));
            throw e switch
            {
                SshAuthenticationException => new FileOperationException(new FileError.RemoteAuthFailed()),
                SocketException or SshConnectionException or SshOperationTimeoutException or TimeoutException =>
                    new FileOperationException(new FileError.RemoteConnectFailed(e.Message)),
                _ => e,
            };
        }
    }, ct);
}
