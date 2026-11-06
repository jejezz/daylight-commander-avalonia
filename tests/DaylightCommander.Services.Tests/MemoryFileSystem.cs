using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

/// <summary>테스트용 메모리 파일 시스템 (원격 서버 흉내). 경로는 <c>sftp://user@host/dir/file</c> 전체 경로.</summary>
public sealed class MemoryFileSystem(string authority) : IFileSystem
{
    sealed class Node(bool isDir, byte[] data, DateTimeOffset modified)
    {
        public bool IsDir = isDir;
        public byte[] Data = data;
        public DateTimeOffset Modified = modified;
    }

    readonly object _gate = new();
    readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);

    public MemoryFileSystem Dir(string serverPath)
    {
        lock (_gate) _nodes[Norm(serverPath)] = new Node(true, [], DateTimeOffset.UnixEpoch);
        return this;
    }

    public MemoryFileSystem File(string serverPath, string content, DateTimeOffset? modified = null)
    {
        var norm = Norm(serverPath);
        lock (_gate)
        {
            for (var parent = Locations.NormalizeServerPath(norm + "/.."); parent != "/" && !_nodes.ContainsKey(parent); parent = Locations.NormalizeServerPath(parent + "/.."))
                _nodes[parent] = new Node(true, [], DateTimeOffset.UnixEpoch);
            _nodes[norm] = new Node(false, System.Text.Encoding.UTF8.GetBytes(content), modified ?? DateTimeOffset.UnixEpoch.AddDays(1));
        }
        return this;
    }

    public string? Read(string serverPath)
    {
        lock (_gate) return _nodes.TryGetValue(Norm(serverPath), out var n) && !n.IsDir ? System.Text.Encoding.UTF8.GetString(n.Data) : null;
    }

    public bool Exists(string serverPath) { lock (_gate) return _nodes.ContainsKey(Norm(serverPath)); }

    public DateTimeOffset? ModifiedOf(string serverPath) { lock (_gate) return _nodes.TryGetValue(Norm(serverPath), out var n) ? n.Modified : null; }

    static string Norm(string serverPath) => Locations.NormalizeServerPath(serverPath);

    string Server(string url) => Locations.TryParse(url)!.Path;

    string Url(string serverPath) => authority + (serverPath == "/" ? "/" : serverPath);

    FileEntry Entry(string serverPath, Node n) => new(Url(serverPath), serverPath == "/" ? authority : serverPath[(serverPath.LastIndexOf('/') + 1)..], n.IsDir,
        n.IsDir ? null : n.Data.Length, n.Modified, IsHidden: serverPath[(serverPath.LastIndexOf('/') + 1)..].StartsWith('.'));

    public Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct = default)
    {
        var dir = Server(path);
        lock (_gate)
        {
            if (dir != "/" && !(_nodes.TryGetValue(dir, out var d) && d.IsDir)) throw new FileOperationException(new FileError.NotFound(Locations.NameOf(path)));
            var list = _nodes.Where(kv => kv.Key != "/" && Locations.NormalizeServerPath(kv.Key + "/..") == dir).Select(kv => Entry(kv.Key, kv.Value)).ToList();
            return Task.FromResult<IReadOnlyList<FileEntry>>(EntrySorting.Sort(list, SortField.Name, true));
        }
    }

    public Task<FileEntry?> StatAsync(string path, CancellationToken ct = default)
    {
        var p = Server(path);
        lock (_gate)
        {
            if (p == "/") return Task.FromResult<FileEntry?>(new FileEntry(Url("/"), authority, true));
            return Task.FromResult(_nodes.TryGetValue(p, out var n) ? Entry(p, n) : null);
        }
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_nodes.TryGetValue(Server(path), out var n) || n.IsDir) throw new FileOperationException(new FileError.NotFound(Locations.NameOf(path)));
            return Task.FromResult<Stream>(new MemoryStream(n.Data.ToArray(), writable: false));
        }
    }

    sealed class WriteStream(MemoryFileSystem fs, string server) : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing) lock (fs._gate) { if (fs._nodes.TryGetValue(server, out var n)) { n.Data = ToArray(); } }
            base.Dispose(disposing);
        }
    }

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct = default)
    {
        var p = Server(path);
        lock (_gate)
        {
            if (!overwrite && _nodes.ContainsKey(p)) throw new FileOperationException(new FileError.AlreadyExists(Locations.NameOf(path)));
            var parent = Locations.NormalizeServerPath(p + "/..");
            if (parent != "/" && !(_nodes.TryGetValue(parent, out var d) && d.IsDir)) throw new FileOperationException(new FileError.NotFound(Locations.NameOf(parent)));
            _nodes[p] = new Node(false, [], DateTimeOffset.UtcNow);
        }
        return Task.FromResult<Stream>(new WriteStream(this, p));
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct = default)
    {
        var p = Server(path);
        lock (_gate)
        {
            if (_nodes.ContainsKey(p)) throw new FileOperationException(new FileError.AlreadyExists(Locations.NameOf(path)));
            _nodes[p] = new Node(true, [], DateTimeOffset.UtcNow);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var p = Server(path);
        lock (_gate)
        {
            if (!_nodes.ContainsKey(p)) throw new FileOperationException(new FileError.NotFound(Locations.NameOf(path)));
            foreach (var key in _nodes.Keys.Where(k => k == p || k.StartsWith(p + "/")).ToList()) _nodes.Remove(key);
        }
        return Task.CompletedTask;
    }

    public Task RenameAsync(string from, string to, CancellationToken ct = default)
    {
        var f = Server(from);
        var t = Server(to);
        lock (_gate)
        {
            if (!_nodes.ContainsKey(f)) throw new FileOperationException(new FileError.NotFound(Locations.NameOf(from)));
            if (_nodes.ContainsKey(t)) throw new FileOperationException(new FileError.AlreadyExists(Locations.NameOf(to)));
            foreach (var key in _nodes.Keys.Where(k => k == f || k.StartsWith(f + "/")).ToList())
            {
                var node = _nodes[key];
                _nodes.Remove(key);
                _nodes[t + key[f.Length..]] = node;
            }
        }
        return Task.CompletedTask;
    }

    public Task SetModifiedAsync(string path, DateTimeOffset modified, CancellationToken ct = default)
    {
        lock (_gate) { if (_nodes.TryGetValue(Server(path), out var n)) n.Modified = modified; }
        return Task.CompletedTask;
    }
}
