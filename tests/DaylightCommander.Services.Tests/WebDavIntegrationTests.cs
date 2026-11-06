using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>WebDAV 통합 테스트는 wsgidav·cheroot가 있는 파이썬이 있어야 돈다 (없으면 건너뜀).</summary>
public sealed class FactIfWebDavServerAttribute : FactAttribute
{
    public FactIfWebDavServerAttribute()
    {
        if (TestPython.With("wsgidav") is null || TestPython.With("cheroot") is null)
            Skip = "wsgidav·cheroot가 있는 파이썬이 없어 건너뜀 (DC_TEST_PYTHON으로 지정할 수 있다)";
    }
}

/// <summary>실제 WebDAV 서버(wsgidav, <c>support/webdav_test_server.py</c>) 하나. 사용자 <c>user</c>/<c>pw</c>.</summary>
public sealed class WebDavServer : IDisposable
{
    public int Port { get; }
    public string Root { get; } = Directory.CreateTempSubdirectory("dc-dav-root").FullName;
    readonly Process? _process;

    public WebDavServer()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        Port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        var python = TestPython.With("wsgidav");
        if (python is null) return;
        var script = Path.Combine(AppContext.BaseDirectory, "support", "webdav_test_server.py");
        var info = new ProcessStartInfo(python) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in new[] { script, "127.0.0.1", Port.ToString(), Root, "user", "pw" }) info.ArgumentList.Add(a);
        _process = Process.Start(info)!;
        var ready = _process.StandardOutput.ReadLineAsync();
        if (!ready.Wait(TimeSpan.FromSeconds(20)) || ready.Result != "READY") throw new InvalidOperationException("WebDAV 시험 서버가 뜨지 않았다");
    }

    public string Url(string relative = "") => $"webdav://user@127.0.0.1:{Port}/" + relative;

    public void Dispose()
    {
        try { _process?.Kill(entireProcessTree: true); _process?.WaitForExit(5000); } catch (InvalidOperationException) { }
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}

public sealed class WebDavIntegrationTests : IDisposable
{
    readonly WebDavServer _server = new(); // 테스트마다 서버를 새로 띄운다 (서로의 파일이 섞이지 않게)
    readonly RemoteSessions _sessions = new();
    readonly FileOperations _ops;
    readonly string _local = Directory.CreateTempSubdirectory("dc-dav-local").FullName;

    public WebDavIntegrationTests() => _ops = new FileOperations(remote: _sessions);

    public void Dispose()
    {
        _sessions.Dispose();
        _server.Dispose();
        Directory.Delete(_local, recursive: true);
    }

    static readonly ConflictResolver NeverAsk = (_, _, _) => throw new InvalidOperationException("충돌을 물으면 안 된다");

    Task<string> ConnectAsync(string password = "pw", string path = "/") =>
        _sessions.ConnectAsync(new RemoteConnectRequest("webdav", "127.0.0.1", _server.Port, "user", password, path));

    string ServerFile(string relative) => Path.Combine(_server.Root, relative);

    [FactIfWebDavServer]
    public async Task 연결하고_목록을_읽는다()
    {
        File.WriteAllText(ServerFile("hello.txt"), "hi");
        Directory.CreateDirectory(ServerFile("sub"));
        var home = await ConnectAsync();
        Assert.Equal(_server.Url(), home);

        var entries = await _sessions.FileSystemFor(home).ListAsync(home);

        Assert.Equal(["sub", "hello.txt"], entries.Select(e => e.Name));
        Assert.True(entries[0].IsDirectory);
        Assert.Equal(2, entries[1].SizeBytes);
        Assert.Equal(_server.Url("hello.txt"), entries[1].Path);
        Assert.NotNull(entries[1].ModifiedAt);
    }

    [FactIfWebDavServer]
    public async Task 시작_폴더를_주면_그_폴더가_홈이다()
    {
        Directory.CreateDirectory(ServerFile("data/inner"));
        var home = await ConnectAsync(path: "/data/");
        Assert.Equal(_server.Url("data"), home);
        var entries = await _sessions.FileSystemFor(home).ListAsync(home);
        Assert.Equal("inner", Assert.Single(entries).Name);
    }

    [FactIfWebDavServer]
    public async Task 비밀번호가_틀리면_인증_실패다()
    {
        var e = await Assert.ThrowsAsync<FileOperationException>(() => ConnectAsync("wrong"));
        Assert.Equal(new FileError.RemoteAuthFailed(), e.Error);
    }

    [FactIfWebDavServer]
    public async Task 서버가_없으면_연결_실패다()
    {
        var e = await Assert.ThrowsAsync<FileOperationException>(() =>
            _sessions.ConnectAsync(new RemoteConnectRequest("webdav", "127.0.0.1", 1, "user", "pw")));
        Assert.IsType<FileError.RemoteConnectFailed>(e.Error);
    }

    [FactIfWebDavServer]
    public async Task 없는_시작_폴더는_찾을_수_없다는_오류다()
    {
        var e = await Assert.ThrowsAsync<FileOperationException>(() => ConnectAsync(path: "/nope"));
        Assert.IsType<FileError.NotFound>(e.Error);
    }

    [FactIfWebDavServer]
    public async Task 올리고_내려받는다_바이너리도_그대로다()
    {
        var home = await ConnectAsync();
        var bytes = new byte[400_000];
        new Random(3).NextBytes(bytes);
        File.WriteAllBytes(Path.Combine(_local, "blob.bin"), bytes);
        File.WriteAllText(Path.Combine(_local, "up.txt"), "uploaded content");

        var up = await _ops.CopyAsync([Path.Combine(_local, "blob.bin"), Path.Combine(_local, "up.txt")], home, NeverAsk);
        Assert.Empty(up.Failures);
        Assert.Equal(bytes, File.ReadAllBytes(ServerFile("blob.bin")));
        Assert.Equal("uploaded content", File.ReadAllText(ServerFile("up.txt")));

        Directory.CreateDirectory(Path.Combine(_local, "back"));
        var down = await _ops.CopyAsync([_server.Url("blob.bin"), _server.Url("up.txt")], Path.Combine(_local, "back"), NeverAsk);
        Assert.Empty(down.Failures);
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(_local, "back", "blob.bin")));
        Assert.Equal("uploaded content", File.ReadAllText(Path.Combine(_local, "back", "up.txt")));
    }

    [FactIfWebDavServer]
    public async Task 공백_한글_특수문자_이름도_다룬다()
    {
        var home = await ConnectAsync();
        var names = new[] { "my file.txt", "한글 이름.txt", "a#b%c&d.txt", "plus+sign.txt" };
        foreach (var n in names) File.WriteAllText(Path.Combine(_local, n), "content of " + n);

        var up = await _ops.CopyAsync(names.Select(n => Path.Combine(_local, n)).ToList(), home, NeverAsk);
        Assert.Empty(up.Failures);
        foreach (var n in names) Assert.Equal("content of " + n, File.ReadAllText(ServerFile(n)));

        var listed = (await _sessions.FileSystemFor(home).ListAsync(home)).Select(e => e.Name).Order().ToArray();
        Assert.Equal(names.Order().ToArray(), listed);

        Directory.CreateDirectory(Path.Combine(_local, "d"));
        var down = await _ops.CopyAsync(names.Select(n => _server.Url(n)).ToList(), Path.Combine(_local, "d"), NeverAsk);
        Assert.Empty(down.Failures);
        foreach (var n in names) Assert.Equal("content of " + n, File.ReadAllText(Path.Combine(_local, "d", n)));
    }

    [FactIfWebDavServer]
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

    [FactIfWebDavServer]
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

    [FactIfWebDavServer]
    public async Task 같은_서버_안에서_복사와_이동()
    {
        var home = await ConnectAsync();
        File.WriteAllText(ServerFile("s.txt"), "same server");
        Directory.CreateDirectory(ServerFile("dest"));

        var copy = await _ops.CopyAsync([_server.Url("s.txt")], _server.Url("dest"), NeverAsk);
        Assert.Empty(copy.Failures);
        Assert.Equal("same server", File.ReadAllText(ServerFile("dest/s.txt")));
        Assert.True(File.Exists(ServerFile("s.txt")));

        File.WriteAllText(ServerFile("m.txt"), "move me");
        var move = await _ops.MoveAsync([_server.Url("m.txt")], _server.Url("dest"), NeverAsk);
        Assert.Empty(move.Failures);
        Assert.False(File.Exists(ServerFile("m.txt")));
        Assert.Equal("move me", File.ReadAllText(ServerFile("dest/m.txt")));
        _ = home;
    }

    [FactIfWebDavServer]
    public async Task 새_폴더_새_파일_이름_바꾸기_삭제()
    {
        var home = await ConnectAsync();
        var made = await _ops.CreateDirectoryAsync(home, "made");
        Assert.True(Directory.Exists(ServerFile("made")));
        var file = await _ops.CreateFileAsync(made, "f.txt");
        Assert.True(File.Exists(ServerFile("made/f.txt")));

        var renamed = await _ops.RenameAsync(file, "g h.txt");
        Assert.Equal(_server.Url("made/g h.txt"), renamed);
        Assert.True(File.Exists(ServerFile("made/g h.txt")));

        File.WriteAllText(ServerFile("made/taken.txt"), "t");
        var e = await Assert.ThrowsAsync<FileOperationException>(() => _ops.RenameAsync(renamed, "taken.txt"));
        Assert.Equal(new FileError.AlreadyExists("taken.txt"), e.Error);

        var del = await _ops.DeleteAsync([_server.Url("made")], toTrash: false);
        Assert.Equal(1, del.Processed);
        Assert.False(Directory.Exists(ServerFile("made")));
    }

    [FactIfWebDavServer]
    public async Task 없는_폴더는_찾을_수_없다는_오류다()
    {
        var home = await ConnectAsync();
        var e = await Assert.ThrowsAsync<FileOperationException>(() => _sessions.FileSystemFor(home).ListAsync(_server.Url("nope")));
        Assert.IsType<FileError.NotFound>(e.Error);
    }

    [FactIfWebDavServer]
    public async Task 이미_있는_폴더를_만들면_이미_있다는_오류다()
    {
        var home = await ConnectAsync();
        Directory.CreateDirectory(ServerFile("dup"));
        var e = await Assert.ThrowsAsync<FileOperationException>(() => _ops.CreateDirectoryAsync(home, "dup"));
        Assert.IsType<FileError.AlreadyExists>(e.Error);
    }
}
