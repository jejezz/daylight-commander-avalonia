using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>FTP 통합 테스트는 pyftpdlib이 있는 파이썬이 있어야 돈다 (없으면 건너뜀).</summary>
public sealed class FactIfFtpServerAttribute : FactAttribute
{
    public FactIfFtpServerAttribute()
    {
        if (TestPython.With("pyftpdlib") is null) Skip = "pyftpdlib이 있는 파이썬이 없어 건너뜀 (DC_TEST_PYTHON으로 지정할 수 있다)";
    }
}

/// <summary>실제 FTP 서버(<c>python -m pyftpdlib</c>) 하나: 사용자 이름·비밀번호가 있는 것(<paramref name="anonymous"/>가 아니면)이나 익명 서버.</summary>
public sealed class FtpServer : IDisposable
{
    public int Port { get; }
    public string Root { get; } = Directory.CreateTempSubdirectory("dc-ftp-root").FullName;
    readonly Process? _process;

    public FtpServer(bool anonymous = false)
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        Port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        var python = TestPython.With("pyftpdlib");
        if (python is null) return;
        var info = new ProcessStartInfo(python) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        var args = new List<string> { "-m", "pyftpdlib", "-i", "127.0.0.1", "-p", Port.ToString(), "-d", Root, "-w" };
        if (!anonymous) args.AddRange(["-u", "user", "-P", "pw"]);
        foreach (var a in args) info.ArgumentList.Add(a);
        _process = Process.Start(info)!;
        // pyftpdlib은 준비되었다는 줄을 따로 찍지 않으므로 포트가 열릴 때까지 기다린다.
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            try
            {
                using var probe = new TcpClient();
                probe.Connect(IPAddress.Loopback, Port);
                break;
            }
            catch (SocketException)
            {
                if (DateTime.UtcNow > deadline || _process.HasExited) throw new InvalidOperationException("FTP 시험 서버가 뜨지 않았다");
                Thread.Sleep(100);
            }
        }
    }

    public string Url(string relative = "", string user = "user") =>
        $"ftp://{user}@127.0.0.1:{Port}/" + relative;

    public void Dispose()
    {
        try { _process?.Kill(entireProcessTree: true); _process?.WaitForExit(5000); } catch (InvalidOperationException) { }
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}

public sealed class FtpIntegrationTests : IDisposable
{
    // 테스트마다 서버를 새로 띄운다 (서로의 파일이 섞이지 않게).
    readonly FtpServer _server = new();
    readonly RemoteSessions _sessions = new();
    readonly FileOperations _ops;
    readonly string _local = Directory.CreateTempSubdirectory("dc-ftp-local").FullName;

    public FtpIntegrationTests()
    {
        _ops = new FileOperations(remote: _sessions);
    }

    public void Dispose()
    {
        _sessions.Dispose();
        _server.Dispose();
        Directory.Delete(_local, recursive: true);
    }

    static readonly ConflictResolver NeverAsk = (_, _, _) => throw new InvalidOperationException("충돌을 물으면 안 된다");

    Task<string> ConnectAsync(string password = "pw", string user = "user") =>
        _sessions.ConnectAsync(new RemoteConnectRequest("ftp", "127.0.0.1", _server.Port, user, password));

    string ServerFile(string relative) => Path.Combine(_server.Root, relative);

    [FactIfFtpServer]
    public async Task 연결하고_목록을_읽는다_시각은_UTC로_맞다()
    {
        File.WriteAllText(ServerFile("hello.txt"), "hi");
        var when = new DateTime(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(ServerFile("hello.txt"), when);
        Directory.CreateDirectory(ServerFile("sub"));
        var home = await ConnectAsync();
        Assert.Equal(_server.Url(), home);

        var entries = await _sessions.FileSystemFor(home).ListAsync(home);

        Assert.Equal(["sub", "hello.txt"], entries.Select(e => e.Name));
        Assert.True(entries[0].IsDirectory);
        Assert.Equal(2, entries[1].SizeBytes);
        Assert.Equal(_server.Url("hello.txt"), entries[1].Path);
        Assert.Equal(new DateTimeOffset(when), entries[1].ModifiedAt);
    }

    [FactIfFtpServer]
    public async Task 비밀번호가_틀리면_인증_실패다()
    {
        var e = await Assert.ThrowsAsync<FileOperationException>(() => ConnectAsync("wrong"));
        Assert.Equal(new FileError.RemoteAuthFailed(), e.Error);
    }

    [FactIfFtpServer]
    public async Task 서버가_없으면_연결_실패다()
    {
        var e = await Assert.ThrowsAsync<FileOperationException>(() =>
            _sessions.ConnectAsync(new RemoteConnectRequest("ftp", "127.0.0.1", 1, "user", "pw")));
        Assert.IsType<FileError.RemoteConnectFailed>(e.Error);
    }

    [FactIfFtpServer]
    public async Task 익명으로_연결한다()
    {
        using var anon = new FtpServer(anonymous: true);
        File.WriteAllText(Path.Combine(anon.Root, "pub.txt"), "public");
        var home = await _sessions.ConnectAsync(new RemoteConnectRequest("ftp", "127.0.0.1", anon.Port, "anonymous", ""));
        var entries = await _sessions.FileSystemFor(home).ListAsync(home);
        Assert.Equal("pub.txt", Assert.Single(entries).Name);
    }

    [FactIfFtpServer]
    public async Task 올리고_내려받아도_내용과_수정_시각이_같다()
    {
        var home = await ConnectAsync();
        File.WriteAllText(Path.Combine(_local, "up.txt"), "uploaded content");
        var when = new DateTime(2025, 6, 7, 8, 9, 10, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(_local, "up.txt"), when);

        var up = await _ops.CopyAsync([Path.Combine(_local, "up.txt")], home, NeverAsk);
        Assert.Empty(up.Failures);
        Assert.Equal("uploaded content", File.ReadAllText(ServerFile("up.txt")));
        var stat = await _sessions.FileSystemFor(home).StatAsync(_server.Url("up.txt"));
        Assert.Equal(new DateTimeOffset(when), stat!.ModifiedAt);

        Directory.CreateDirectory(Path.Combine(_local, "back"));
        var down = await _ops.CopyAsync([_server.Url("up.txt")], Path.Combine(_local, "back"), NeverAsk);
        Assert.Empty(down.Failures);
        Assert.Equal("uploaded content", File.ReadAllText(Path.Combine(_local, "back", "up.txt")));
        Assert.Equal(when, File.GetLastWriteTimeUtc(Path.Combine(_local, "back", "up.txt")));
    }

    [FactIfFtpServer]
    public async Task 바이너리_파일도_그대로_오간다()
    {
        var home = await ConnectAsync();
        var bytes = new byte[300_000];
        new Random(7).NextBytes(bytes);
        File.WriteAllBytes(Path.Combine(_local, "blob.bin"), bytes);

        await _ops.CopyAsync([Path.Combine(_local, "blob.bin")], home, NeverAsk);
        Assert.Equal(bytes, File.ReadAllBytes(ServerFile("blob.bin")));

        Directory.CreateDirectory(Path.Combine(_local, "d"));
        await _ops.CopyAsync([_server.Url("blob.bin")], Path.Combine(_local, "d"), NeverAsk);
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(_local, "d", "blob.bin")));
    }

    [FactIfFtpServer]
    public async Task 폴더를_통째로_올리고_내려받는다()
    {
        var home = await ConnectAsync();
        Directory.CreateDirectory(Path.Combine(_local, "tree/a/b"));
        File.WriteAllText(Path.Combine(_local, "tree/root.txt"), "R");
        File.WriteAllText(Path.Combine(_local, "tree/a/b/deep.txt"), "D");
        Directory.CreateDirectory(Path.Combine(_local, "tree/blank"));

        var up = await _ops.CopyAsync([Path.Combine(_local, "tree")], home, NeverAsk);

        Assert.Empty(up.Failures);
        Assert.Equal("D", File.ReadAllText(ServerFile("tree/a/b/deep.txt")));
        Assert.True(Directory.Exists(ServerFile("tree/blank")));

        Directory.CreateDirectory(Path.Combine(_local, "down"));
        var down = await _ops.CopyAsync([_server.Url("tree")], Path.Combine(_local, "down"), NeverAsk);
        Assert.Empty(down.Failures);
        Assert.Equal("R", File.ReadAllText(Path.Combine(_local, "down/tree/root.txt")));
        Assert.True(Directory.Exists(Path.Combine(_local, "down/tree/blank")));
    }

    [FactIfFtpServer]
    public async Task 이름이_겹치면_고른_대로_덮어쓰거나_둘다_유지한다()
    {
        var home = await ConnectAsync();
        File.WriteAllText(ServerFile("c.txt"), "old");
        File.WriteAllText(Path.Combine(_local, "c.txt"), "new");

        await _ops.CopyAsync([Path.Combine(_local, "c.txt")], home, (_, _, _) => Task.FromResult(new ConflictDecision(ConflictChoice.KeepBoth)));
        Assert.Equal("old", File.ReadAllText(ServerFile("c.txt")));
        Assert.Equal("new", File.ReadAllText(ServerFile("c (1).txt")));

        await _ops.CopyAsync([Path.Combine(_local, "c.txt")], home, (_, _, _) => Task.FromResult(new ConflictDecision(ConflictChoice.Overwrite)));
        Assert.Equal("new", File.ReadAllText(ServerFile("c.txt")));
    }

    [FactIfFtpServer]
    public async Task 같은_서버_안에서_복사와_이동()
    {
        var home = await ConnectAsync();
        File.WriteAllText(ServerFile("s.txt"), "same server");
        Directory.CreateDirectory(ServerFile("dest"));

        var copy = await _ops.CopyAsync([_server.Url("s.txt")], _server.Url("dest"), NeverAsk);
        Assert.Empty(copy.Failures);
        Assert.Equal("same server", File.ReadAllText(ServerFile("dest/s.txt")));
        Assert.True(File.Exists(ServerFile("s.txt")));

        var move = await _ops.MoveAsync([_server.Url("s.txt")], _server.Url("dest/../dest"), (_, _, _) => Task.FromResult(new ConflictDecision(ConflictChoice.Overwrite)));
        Assert.Empty(move.Failures);
        Assert.False(File.Exists(ServerFile("s.txt")));
        Assert.Equal("same server", File.ReadAllText(ServerFile("dest/s.txt")));
        _ = home;
    }

    [FactIfFtpServer]
    public async Task 새_폴더_새_파일_이름_바꾸기_삭제()
    {
        var home = await ConnectAsync();
        var made = await _ops.CreateDirectoryAsync(home, "made");
        Assert.True(Directory.Exists(ServerFile("made")));
        var file = await _ops.CreateFileAsync(made, "f.txt");
        Assert.True(File.Exists(ServerFile("made/f.txt")));

        var renamed = await _ops.RenameAsync(file, "g.txt");
        Assert.Equal(_server.Url("made/g.txt"), renamed);
        Assert.True(File.Exists(ServerFile("made/g.txt")));

        var del = await _ops.DeleteAsync([_server.Url("made")], toTrash: false);
        Assert.Equal(1, del.Processed);
        Assert.False(Directory.Exists(ServerFile("made")));
    }

    [FactIfFtpServer]
    public async Task 없는_폴더는_찾을_수_없다는_오류다()
    {
        var home = await ConnectAsync();
        var e = await Assert.ThrowsAsync<FileOperationException>(() => _sessions.FileSystemFor(home).ListAsync(_server.Url("nope")));
        Assert.IsType<FileError.NotFound>(e.Error);
    }

    [FactIfFtpServer]
    public async Task 연결을_끊은_뒤의_접근은_연결_안_됨이다()
    {
        var home = await ConnectAsync();
        _sessions.Disconnect(home);
        var e = Assert.Throws<FileOperationException>(() => _sessions.FileSystemFor(home));
        Assert.IsType<FileError.RemoteNotConnected>(e.Error);
    }

    [FactIfFtpServer]
    public async Task 원격_편집_연결은_덮어쓰고_서버의_변경을_알아챈다()
    {
        var home = await ConnectAsync();
        File.WriteAllText(ServerFile("edit.txt"), "original");
        var fs = _sessions.FileSystemFor(home);
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
