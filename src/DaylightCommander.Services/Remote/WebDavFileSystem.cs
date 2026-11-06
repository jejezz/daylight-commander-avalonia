using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// WebDAV 서버 하나를 <see cref="IFileSystem"/>으로 보이게 한 것. 별도 라이브러리 없이 <see cref="HttpClient"/>로 PROPFIND(목록·정보),
/// GET(읽기), PUT(쓰기), MKCOL(새 폴더), DELETE(삭제, 폴더는 서버가 안까지 지운다), MOVE(이름 바꾸기)를 보낸다. HTTP는 요청마다 독립이라
/// 읽기는 스트림으로 바로 흘려 보내고, 쓰기는 임시 파일에 쓰다가 스트림을 닫을 때(<c>DisposeAsync</c>) 올린다.
/// 인증은 <see cref="HttpClientHandler.Credentials"/>가 서버의 요구(Basic·Digest 등)에 맞춰 처리한다.
/// 서버 안의 경로는 실제 URL 경로 그대로다 (서버가 <c>/dav/</c> 아래에 있으면 그 경로가 앞에 붙어 있다).
/// WebDAV에는 수정 시각을 정해서 올리는 표준 방법이 없어, 올린 파일은 서버가 정한 시각을 갖는다.
/// </summary>
public sealed class WebDavFileSystem : IFileSystem, IDisposable
{
    static readonly HttpMethod Propfind = new("PROPFIND");
    static readonly HttpMethod Mkcol = new("MKCOL");
    static readonly HttpMethod Move = new("MOVE");
    static readonly XNamespace D = "DAV:";

    readonly HttpClient _http;
    readonly string _authority;

    public WebDavFileSystem(HttpClient http, string authority)
    {
        _http = http;
        _authority = authority;
    }

    /// <summary>스킴(<c>webdav</c>/<c>webdavs</c>)에 맞는 클라이언트를 만든다. 계정이 비어 있으면 인증 없이 보낸다.</summary>
    public static HttpClient CreateClient(RemoteConnectRequest request)
    {
        var handler = new HttpClientHandler { PreAuthenticate = true, AllowAutoRedirect = true };
        if (request.User.Length > 0) handler.Credentials = new NetworkCredential(request.User, request.Password);
        var http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri($"{(request.Scheme == "webdavs" ? "https" : "http")}://{request.Host}:{request.Port}"),
            Timeout = TimeSpan.FromSeconds(60),
        };
        return http;
    }

    /// <summary>연결을 확인한다: 시작 폴더에 PROPFIND. 인증 실패·연결 실패는 앱의 오류로 올린다.</summary>
    public async Task VerifyAsync(string serverPath, CancellationToken ct = default)
    {
        using var response = await SendAsync(Propfind, serverPath, depth: "0", ct: ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new FileOperationException(new FileError.RemoteAuthFailed());
        if (response.StatusCode == HttpStatusCode.NotFound) throw new FileOperationException(new FileError.NotFound(Locations.NameOf(serverPath)));
        if ((int)response.StatusCode != 207 && !response.IsSuccessStatusCode)
            throw new FileOperationException(new FileError.RemoteConnectFailed($"HTTP {(int)response.StatusCode}"));
    }

    string ServerPath(string path) => Locations.TryParse(path)?.Path ?? throw new FileOperationException(new FileError.Other(path));

    string UrlFor(string serverPath) =>
        Locations.TryParse(_authority + "/") is { } loc ? Locations.Format(loc with { Path = Locations.NormalizeServerPath(serverPath) }) : serverPath;

    /// <summary>서버 경로를 요청 URL 경로로: 구간마다 퍼센트 인코딩.</summary>
    static string Encode(string serverPath) =>
        "/" + string.Join('/', serverPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    async Task<HttpResponseMessage> SendAsync(HttpMethod method, string serverPath, string? depth = null, HttpContent? content = null,
        Action<HttpRequestMessage>? configure = null, HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(method, Encode(serverPath)) { Content = content };
        if (depth is not null) request.Headers.Add("Depth", depth);
        configure?.Invoke(request);
        try
        {
            return await _http.SendAsync(request, completion, ct);
        }
        catch (HttpRequestException e)
        {
            throw new FileOperationException(new FileError.RemoteConnectFailed(e.Message));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new FileOperationException(new FileError.RemoteConnectFailed("timeout"));
        }
    }

    /// <summary>실패한 응답을 앱의 오류로 바꾼다 (성공이면 아무 일도 없다).</summary>
    void Check(HttpResponseMessage response, string name)
    {
        if (response.IsSuccessStatusCode) return;
        throw new FileOperationException(response.StatusCode switch
        {
            HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.Gone => new FileError.NotFound(name),
            HttpStatusCode.Forbidden => new FileError.AccessDenied(name),
            HttpStatusCode.Unauthorized => new FileError.RemoteAuthFailed(),
            HttpStatusCode.MethodNotAllowed or HttpStatusCode.PreconditionFailed => new FileError.AlreadyExists(name),
            _ => new FileError.Other($"HTTP {(int)response.StatusCode}"),
        });
    }

    // --- 읽기 ---

    sealed record Item(string ServerPath, bool IsDirectory, long? Size, DateTimeOffset? Modified);

    static async Task<List<Item>> ParseMultistatusAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        var items = new List<Item>();
        foreach (var r in doc.Descendants(D + "response"))
        {
            var href = r.Element(D + "href")?.Value;
            if (string.IsNullOrEmpty(href)) continue;
            var path = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new Uri(href).AbsolutePath : href;
            path = Locations.NormalizeServerPath(Uri.UnescapeDataString(path));
            // 성공(200)인 속성만 읽는다.
            var props = r.Elements(D + "propstat").Where(p => p.Element(D + "status")?.Value.Contains(" 200", StringComparison.Ordinal) ?? true).SelectMany(p => p.Elements(D + "prop")).ToList();
            var isDir = props.Any(p => p.Element(D + "resourcetype")?.Element(D + "collection") is not null);
            long? size = props.Select(p => p.Element(D + "getcontentlength")?.Value).FirstOrDefault(v => v is not null) is { } len && long.TryParse(len, out var n) ? n : null;
            DateTimeOffset? modified = props.Select(p => p.Element(D + "getlastmodified")?.Value).FirstOrDefault(v => v is not null) is { } lm
                && DateTimeOffset.TryParse(lm, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t.ToUniversalTime() : null;
            items.Add(new Item(path, isDir, isDir ? null : size, modified));
        }
        return items;
    }

    FileEntry ToEntry(Item item)
    {
        var name = item.ServerPath == "/" ? _authority : item.ServerPath[(item.ServerPath.LastIndexOf('/') + 1)..];
        return new FileEntry(UrlFor(item.ServerPath), name, item.IsDirectory, item.Size, item.Modified, name.StartsWith('.'));
    }

    public async Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct = default)
    {
        var server = ServerPath(path);
        using var response = await SendAsync(Propfind, server, depth: "1", ct: ct);
        Check(response, Locations.NameOf(path));
        var items = await ParseMultistatusAsync(response, ct);
        var entries = items.Where(i => i.ServerPath != server).Select(ToEntry).ToList(); // 첫 항목은 폴더 자신
        return EntrySorting.Sort(entries, SortField.Name, ascending: true);
    }

    public async Task<FileEntry?> StatAsync(string path, CancellationToken ct = default)
    {
        var server = ServerPath(path);
        using var response = await SendAsync(Propfind, server, depth: "0", ct: ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        Check(response, Locations.NameOf(path));
        return (await ParseMultistatusAsync(response, ct)).FirstOrDefault() is { } item ? ToEntry(item with { ServerPath = server }) : null;
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Get, ServerPath(path), completion: HttpCompletionOption.ResponseHeadersRead, ct: ct);
        try
        {
            Check(response, Locations.NameOf(path));
            return new ResponseStream(response, await response.Content.ReadAsStreamAsync(ct));
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>스트림을 닫을 때 응답도 같이 치운다.</summary>
    sealed class ResponseStream(HttpResponseMessage response, Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); response.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // --- 쓰기 ---

    public async Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct = default)
    {
        if (!overwrite && await StatAsync(path, ct) is not null) throw new FileOperationException(new FileError.AlreadyExists(Locations.NameOf(path)));
        return new UploadOnCloseStream(this, path, ct);
    }

    /// <summary>쓰는 동안은 임시 파일이고, 닫을 때 PUT으로 올린 뒤 지운다 (<see cref="FtpFileSystem"/>의 것과 같은 방식).</summary>
    sealed class UploadOnCloseStream : FileStream
    {
        readonly WebDavFileSystem _owner;
        readonly string _remote;
        readonly string _temp;
        readonly CancellationToken _ct;
        bool _closingAsync;

        public UploadOnCloseStream(WebDavFileSystem owner, string remote, CancellationToken ct)
            : this(owner, remote, ct, Path.Combine(Path.GetTempPath(), "dc-dav-" + Guid.NewGuid().ToString("N"))) { }

        UploadOnCloseStream(WebDavFileSystem owner, string remote, CancellationToken ct, string temp)
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
                await using var source = new FileStream(_temp, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024, FileOptions.Asynchronous);
                using var response = await _owner.SendAsync(HttpMethod.Put, _owner.ServerPath(_remote), content: new StreamContent(source), ct: _ct);
                _owner.Check(response, Locations.NameOf(_remote));
            }
            finally
            {
                try { File.Delete(_temp); } catch (IOException) { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && !_closingAsync) try { File.Delete(_temp); } catch (IOException) { }
        }
    }

    public async Task CreateDirectoryAsync(string path, CancellationToken ct = default)
    {
        using var response = await SendAsync(Mkcol, ServerPath(path), ct: ct);
        Check(response, Locations.NameOf(path));
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, ServerPath(path), ct: ct);
        Check(response, Locations.NameOf(path));
    }

    public async Task RenameAsync(string from, string to, CancellationToken ct = default)
    {
        var destination = new Uri(_http.BaseAddress!, Encode(ServerPath(to))).AbsoluteUri;
        using var response = await SendAsync(Move, ServerPath(from), configure: r =>
        {
            r.Headers.Add("Destination", destination);
            r.Headers.Add("Overwrite", "F"); // 같은 이름이 있으면 덮어쓰지 않고 실패 (412)
        }, ct: ct);
        Check(response, Locations.NameOf(from));
    }

    /// <summary>WebDAV에는 수정 시각을 정하는 표준 방법이 없다 — 아무것도 하지 않는다.</summary>
    public Task SetModifiedAsync(string path, DateTimeOffset modified, CancellationToken ct = default) => Task.CompletedTask;

    public void Dispose() => _http.Dispose();
}
