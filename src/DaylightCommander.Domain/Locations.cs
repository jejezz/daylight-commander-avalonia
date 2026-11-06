namespace DaylightCommander.Domain;

/// <summary>원격 위치 하나: <c>sftp://user@host:port/경로</c>를 나눈 것. 비밀번호는 위치에 넣지 않는다.</summary>
/// <param name="Path">서버 안의 경로 (슬래시로 시작, 루트는 <c>/</c>).</param>
public sealed record RemoteLocation(string Scheme, string? User, string Host, int Port, string Path)
{
    /// <summary>같은 서버·같은 계정인지 가르는 열쇠 (스킴·계정·호스트·포트). 경로는 뺀다.</summary>
    public string Authority => Locations.FormatAuthority(Scheme, User, Host, Port);
}

/// <summary>
/// 위치(경로) 문자열의 규칙. 로컬은 그냥 운영체제 경로이고, 원격은 <c>sftp://</c>·<c>ftp://</c>·<c>webdav://</c>(HTTP)·<c>webdavs://</c>(HTTPS) <c>user@host[:port]/경로</c> 형태의 문자열이다 — 도메인과
/// 서비스가 두 종류를 같은 문자열로 다루고, 위·이름·합치기만 이 도우미를 거친다. 서버 안의 경로는 항상 슬래시 구분이다.
/// 원격 경로는 퍼센트 인코딩하지 않는다 (공백·한글이 그대로 들어 있다).
/// </summary>
public static class Locations
{
    /// <summary>지원하는 원격 스킴과 기본 포트.</summary>
    static readonly (string Scheme, int DefaultPort)[] Schemes = [("sftp", 22), ("ftp", 21), ("webdav", 80), ("webdavs", 443)];

    public static bool IsRemote(string path) => SchemeOf(path) is not null;

    static string? SchemeOf(string path)
    {
        foreach (var (scheme, _) in Schemes)
            if (path.StartsWith(scheme + "://", StringComparison.OrdinalIgnoreCase)) return scheme;
        return null;
    }

    public static int DefaultPort(string scheme) => Schemes.First(s => s.Scheme == scheme).DefaultPort;

    /// <summary>원격 경로를 나눈다. 원격이 아니거나 호스트가 비었으면 null. 경로가 비었으면 <c>/</c>.</summary>
    public static RemoteLocation? TryParse(string path)
    {
        if (SchemeOf(path) is not { } scheme) return null;
        var rest = path[(scheme.Length + 3)..];
        var slash = rest.IndexOf('/');
        var authority = slash < 0 ? rest : rest[..slash];
        var serverPath = slash < 0 ? "/" : rest[slash..];

        string? user = null;
        var at = authority.LastIndexOf('@');
        if (at >= 0)
        {
            user = authority[..at];
            authority = authority[(at + 1)..];
        }
        var port = DefaultPort(scheme);
        var colon = authority.LastIndexOf(':');
        if (colon >= 0 && int.TryParse(authority[(colon + 1)..], out var p) && p is > 0 and <= 65535)
        {
            port = p;
            authority = authority[..colon];
        }
        if (authority.Length == 0) return null;
        return new RemoteLocation(scheme, string.IsNullOrEmpty(user) ? null : user, authority, port, NormalizeServerPath(serverPath));
    }

    public static string FormatAuthority(string scheme, string? user, string host, int port)
    {
        var portPart = port == DefaultPort(scheme) ? "" : $":{port}";
        return $"{scheme}://{(string.IsNullOrEmpty(user) ? "" : user + "@")}{host}{portPart}";
    }

    public static string Format(RemoteLocation location) => location.Authority + (location.Path == "/" ? "/" : location.Path);

    /// <summary>서버 안의 경로를 다듬는다: 슬래시로 시작, 중복 슬래시·<c>.</c>·<c>..</c> 정리, 끝 슬래시 제거(루트 제외).</summary>
    public static string NormalizeServerPath(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; }
            parts.Add(part);
        }
        return "/" + string.Join('/', parts);
    }

    /// <summary>원격 경로를 다듬은 문자열로 (기본 포트는 생략). 원격이 아니면 그대로.</summary>
    public static string NormalizeRemote(string path) => TryParse(path) is { } loc ? Format(loc) : path;

    /// <summary>같은 서버(같은 계정)에 있는가. 둘 다 원격이어야 하고 스킴·계정·호스트·포트가 같아야 한다.</summary>
    public static bool SameServer(string a, string b) =>
        TryParse(a) is { } x && TryParse(b) is { } y && x.Scheme == y.Scheme && x.Port == y.Port &&
        string.Equals(x.User, y.User, StringComparison.Ordinal) && string.Equals(x.Host, y.Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 한 단계 위. 로컬은 <see cref="System.IO.Path.GetDirectoryName(string?)"/>(루트면 null), 원격은 서버 안의 위쪽 폴더
    /// (서버 루트 <c>/</c>면 null).
    /// </summary>
    public static string? Parent(string path)
    {
        if (TryParse(path) is not { } loc) return System.IO.Path.GetDirectoryName(path);
        if (loc.Path == "/") return null;
        var cut = loc.Path.LastIndexOf('/');
        return Format(loc with { Path = cut <= 0 ? "/" : loc.Path[..cut] });
    }

    /// <summary>폴더 경로와 항목 이름을 합친다.</summary>
    public static string Combine(string dir, string name)
    {
        if (TryParse(dir) is not { } loc) return System.IO.Path.Combine(dir, name);
        return Format(loc with { Path = loc.Path == "/" ? "/" + name : loc.Path + "/" + name });
    }

    /// <summary>경로의 마지막 이름. 서버 루트나 로컬 루트는 빈 문자열이 아니라 경로 자신.</summary>
    public static string NameOf(string path)
    {
        if (TryParse(path) is { } loc)
        {
            if (loc.Path == "/") return loc.Authority;
            return loc.Path[(loc.Path.LastIndexOf('/') + 1)..];
        }
        var trimmed = path.TrimEnd('\\', '/');
        var name = System.IO.Path.GetFileName(trimmed);
        return name.Length > 0 ? name : path;
    }
}
