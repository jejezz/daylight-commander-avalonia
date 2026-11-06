using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>
/// 실제 SFTP 서버(파이썬 asyncssh, <c>support/sftp_test_server.py</c>)를 띄워 SSH.NET으로 만든 클라이언트를 시험한다.
/// 파이썬과 asyncssh가 있어야 돈다 (없으면 건너뜀). 다른 파이썬을 쓰려면 환경 변수 <c>DC_TEST_PYTHON</c>에 실행 파일 경로를 준다.
/// </summary>
public sealed class FactIfSftpServerAttribute : FactAttribute
{
    internal static string? PythonPath => TestPython.With("asyncssh");

    public FactIfSftpServerAttribute()
    {
        if (PythonPath is null) Skip = "asyncssh가 있는 파이썬이 없어 건너뜀 (DC_TEST_PYTHON으로 지정할 수 있다)";
    }
}

/// <summary>통합 테스트가 쓰는 파이썬 찾기: 지정한 모듈을 불러올 수 있는 첫 파이썬 (없으면 null).</summary>
internal static class TestPython
{
    static readonly Dictionary<string, string?> Cache = [];

    public static string? With(string module)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(module, out var cached)) return cached;
            return Cache[module] = Find(module);
        }
    }

    static string? Find(string module)
    {
        foreach (var candidate in new[] { Environment.GetEnvironmentVariable("DC_TEST_PYTHON"), "python", "python3" })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                var info = new ProcessStartInfo(candidate, $"-c \"import {module}\"") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
                using var p = Process.Start(info);
                if (p is null) continue;
                if (!p.WaitForExit(15000)) { p.Kill(); continue; }
                if (p.ExitCode == 0) return candidate;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
        return null;
    }
}

public sealed class SftpServerFixture : IDisposable
{
    public int Port { get; }
    public string Root { get; } = Directory.CreateTempSubdirectory("dc-sftp-root").FullName;
    Process? _process;

    public SftpServerFixture()
    {
        Port = FreePort();
        Start();
    }

    static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>서버를 (다시) 띄운다. 호스트 키는 시작할 때마다 새로 만들어지므로, 다시 띄우면 다른 서버가 된다.</summary>
    public void Start()
    {
        var python = FactIfSftpServerAttribute.PythonPath;
        if (python is null) return;
        var script = Path.Combine(AppContext.BaseDirectory, "support", "sftp_test_server.py");
        var info = new ProcessStartInfo(python) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in new[] { script, "127.0.0.1", Port.ToString(), Root, "user", "pw" }) info.ArgumentList.Add(a);
        _process = Process.Start(info)!;
        var ready = _process.StandardOutput.ReadLineAsync();
        if (!ready.Wait(TimeSpan.FromSeconds(20)) || ready.Result != "READY") throw new InvalidOperationException("SFTP 시험 서버가 뜨지 않았다");
    }

    public void Stop()
    {
        try { _process?.Kill(entireProcessTree: true); _process?.WaitForExit(5000); } catch (InvalidOperationException) { }
        _process = null;
    }

    /// <summary>서버 안의 경로 (루트 없이 파일 시스템을 그대로 보여 주므로 Windows는 <c>/C:/…</c> 꼴).</summary>
    public string Url(string relative = "") =>
        $"sftp://user@127.0.0.1:{Port}/" + Root.Replace('\\', '/').TrimStart('/') + (relative.Length == 0 ? "" : "/" + relative);

    public void Dispose()
    {
        Stop();
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}

public sealed class SftpIntegrationTests : IClassFixture<SftpServerFixture>, IDisposable
{
    sealed class MemoryHostKeys : IHostKeyStore
    {
        public Dictionary<string, string> Keys { get; } = [];
        public string? Get(string hostPort) => Keys.GetValueOrDefault(hostPort);
        public void Set(string hostPort, string fingerprint) => Keys[hostPort] = fingerprint;
    }

    readonly SftpServerFixture _server;
    readonly MemoryHostKeys _keys = new();
    readonly RemoteSessions _sessions;
    readonly FileOperations _ops;
    readonly string _local = Directory.CreateTempSubdirectory("dc-sftp-local").FullName;

    public SftpIntegrationTests(SftpServerFixture server)
    {
        _server = server;
        _sessions = new RemoteSessions(_keys);
        _ops = new FileOperations(remote: _sessions);
    }

    public void Dispose()
    {
        _sessions.Dispose();
        Directory.Delete(_local, recursive: true);
    }

    static readonly ConflictResolver NeverAsk = (_, _, _) => throw new InvalidOperationException("충돌을 물으면 안 된다");

    Task<string> ConnectAsync(string password = "pw", string user = "user") =>
        _sessions.ConnectAsync(new RemoteConnectRequest("sftp", "127.0.0.1", _server.Port, user, password));

    string ServerFile(string relative) => Path.Combine(_server.Root, relative);

    [FactIfSftpServer]
    public async Task 연결하고_목록을_읽는다()
    {
        File.WriteAllText(ServerFile("hello.txt"), "hi");
        Directory.CreateDirectory(ServerFile("sub"));
        await ConnectAsync();

        var entries = await _sessions.FileSystemFor(_server.Url()).ListAsync(_server.Url());

        Assert.Equal(["sub", "hello.txt"], entries.Select(e => e.Name)); // 폴더가 먼저
        Assert.True(entries[0].IsDirectory);
        Assert.Equal(2, entries[1].SizeBytes);
        Assert.Equal(_server.Url("hello.txt"), entries[1].Path);
    }

    [FactIfSftpServer]
    public async Task 비밀번호가_틀리면_인증_실패다()
    {
        var e = await Assert.ThrowsAsync<FileOperationException>(() => ConnectAsync("wrong"));
        Assert.Equal(new FileError.RemoteAuthFailed(), e.Error);
    }

    [FactIfSftpServer]
    public async Task 서버가_없으면_연결_실패다()
    {
        var e = await Assert.ThrowsAsync<FileOperationException>(() =>
            _sessions.ConnectAsync(new RemoteConnectRequest("sftp", "127.0.0.1", 1, "user", "pw")));
        Assert.IsType<FileError.RemoteConnectFailed>(e.Error);
    }

    [FactIfSftpServer]
    public async Task 올리고_내려받아도_내용과_수정_시각이_같다()
    {
        await ConnectAsync();
        File.WriteAllText(Path.Combine(_local, "up.txt"), "uploaded content");
        var when = new DateTime(2025, 6, 7, 8, 9, 10, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(_local, "up.txt"), when);

        var up = await _ops.CopyAsync([Path.Combine(_local, "up.txt")], _server.Url(), NeverAsk);
        Assert.Empty(up.Failures);
        Assert.Equal("uploaded content", File.ReadAllText(ServerFile("up.txt")));
        var stat = await _sessions.FileSystemFor(_server.Url()).StatAsync(_server.Url("up.txt"));
        Assert.Equal(new DateTimeOffset(when), stat!.ModifiedAt);

        var down = await _ops.CopyAsync([_server.Url("up.txt")], Path.Combine(_local, "back"), NeverAsk);
        // 대상 폴더가 없으니 항목이 실패로 남는다
        Assert.Single(down.Failures);
        Directory.CreateDirectory(Path.Combine(_local, "back"));
        down = await _ops.CopyAsync([_server.Url("up.txt")], Path.Combine(_local, "back"), NeverAsk);
        Assert.Empty(down.Failures);
        Assert.Equal("uploaded content", File.ReadAllText(Path.Combine(_local, "back", "up.txt")));
    }

    [FactIfSftpServer]
    public async Task 폴더를_통째로_올리고_내려받는다()
    {
        await ConnectAsync();
        Directory.CreateDirectory(Path.Combine(_local, "tree/a/b"));
        File.WriteAllText(Path.Combine(_local, "tree/root.txt"), "R");
        File.WriteAllText(Path.Combine(_local, "tree/a/b/deep.txt"), "D");
        Directory.CreateDirectory(Path.Combine(_local, "tree/blank"));

        var up = await _ops.CopyAsync([Path.Combine(_local, "tree")], _server.Url(), NeverAsk);

        Assert.Empty(up.Failures);
        Assert.Equal("D", File.ReadAllText(ServerFile("tree/a/b/deep.txt")));
        Assert.True(Directory.Exists(ServerFile("tree/blank")));

        Directory.CreateDirectory(Path.Combine(_local, "down"));
        var down = await _ops.CopyAsync([_server.Url("tree")], Path.Combine(_local, "down"), NeverAsk);
        Assert.Empty(down.Failures);
        Assert.Equal("R", File.ReadAllText(Path.Combine(_local, "down/tree/root.txt")));
        Assert.True(Directory.Exists(Path.Combine(_local, "down/tree/blank")));
    }

    [FactIfSftpServer]
    public async Task 이름이_겹치면_덮어쓴다()
    {
        await ConnectAsync();
        File.WriteAllText(ServerFile("c.txt"), "old");
        File.WriteAllText(Path.Combine(_local, "c.txt"), "new");

        await _ops.CopyAsync([Path.Combine(_local, "c.txt")], _server.Url(), (_, _, _) => Task.FromResult(new ConflictDecision(ConflictChoice.Overwrite)));

        Assert.Equal("new", File.ReadAllText(ServerFile("c.txt")));
    }

    [FactIfSftpServer]
    public async Task 새_폴더_이름_바꾸기_이동_삭제()
    {
        await ConnectAsync();
        var made = await _ops.CreateDirectoryAsync(_server.Url(), "made");
        Assert.True(Directory.Exists(ServerFile("made")));
        var file = await _ops.CreateFileAsync(made, "f.txt");
        Assert.True(File.Exists(ServerFile("made/f.txt")));

        var renamed = await _ops.RenameAsync(file, "g.txt");
        Assert.Equal(_server.Url("made/g.txt"), renamed);
        Assert.True(File.Exists(ServerFile("made/g.txt")));

        Directory.CreateDirectory(ServerFile("elsewhere"));
        var move = await _ops.MoveAsync([renamed], _server.Url("elsewhere"), NeverAsk);
        Assert.Empty(move.Failures);
        Assert.True(File.Exists(ServerFile("elsewhere/g.txt")));
        Assert.False(File.Exists(ServerFile("made/g.txt")));

        var del = await _ops.DeleteAsync([_server.Url("made"), _server.Url("elsewhere")], toTrash: false);
        Assert.Equal(2, del.Processed);
        Assert.False(Directory.Exists(ServerFile("made")));
        Assert.False(Directory.Exists(ServerFile("elsewhere")));
    }

    [FactIfSftpServer]
    public async Task 연결을_끊은_뒤의_접근은_연결_안_됨이다()
    {
        await ConnectAsync();
        _sessions.Disconnect(_server.Url());
        var e = Assert.Throws<FileOperationException>(() => _sessions.FileSystemFor(_server.Url()));
        Assert.IsType<FileError.RemoteNotConnected>(e.Error);
    }

    [FactIfSftpServer]
    public async Task 호스트_키가_바뀌면_연결을_거부한다()
    {
        await ConnectAsync(); // 처음 보는 서버: 키를 기억한다
        Assert.Single(_keys.Keys);
        _sessions.Disconnect(_server.Url());

        _server.Stop();
        _server.Start(); // 같은 주소에 다른 키로 다시 뜬다

        var e = await Assert.ThrowsAsync<FileOperationException>(() => ConnectAsync());
        Assert.IsType<FileError.RemoteHostKeyChanged>(e.Error);
    }

    [FactIfSftpServer]
    public async Task 원격_편집_연결은_덮어쓰고_서버의_변경을_알아챈다()
    {
        await ConnectAsync();
        File.WriteAllText(ServerFile("edit.txt"), "original");
        var fs = _sessions.FileSystemFor(_server.Url());
        var url = _server.Url("edit.txt");
        var link = new RemoteEditLink(fs, url, await fs.StatAsync(url));

        File.WriteAllText(Path.Combine(_local, "edit.txt"), "edited and longer");
        Assert.False(await link.ChangedOnServerAsync());
        await link.UploadAsync(Path.Combine(_local, "edit.txt"));
        Assert.Equal("edited and longer", File.ReadAllText(ServerFile("edit.txt")));
        Assert.False(await link.ChangedOnServerAsync());

        File.WriteAllText(ServerFile("edit.txt"), "changed by someone else!!");
        Assert.True(await link.ChangedOnServerAsync());
    }
}
