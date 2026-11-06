using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 원격 파일을 에디터로 고칠 때 임시 사본과 서버의 원본을 이어 주는 연결. 에디터는 로컬 임시 파일을 고치고, 저장할 때마다
/// <see cref="UploadAsync"/>로 서버에 올린다 (원본 PLAN의 "원격 업로드백"). 올리기에 실패하면 임시 파일에 고친 내용이 남는다.
/// </summary>
public interface IRemoteEditLink
{
    /// <summary>서버 쪽 파일의 전체 경로 (<c>sftp://user@host/dir/file</c>).</summary>
    string Url { get; }

    /// <summary>열었을 때(또는 마지막으로 올렸을 때) 이후 서버의 파일이 바뀌었는가. 파일이 없어졌어도 true.</summary>
    Task<bool> ChangedOnServerAsync(CancellationToken ct = default);

    /// <summary>로컬 파일 내용을 서버의 파일에 덮어쓴다. 성공하면 "바뀌었는가"의 기준도 새로 잡는다.</summary>
    Task UploadAsync(string localPath, CancellationToken ct = default);
}

/// <summary><see cref="IFileSystem"/> 위의 <see cref="IRemoteEditLink"/>. <paramref name="opened"/>는 내려받을 때 본 서버 파일의 정보.</summary>
public sealed class RemoteEditLink(IFileSystem fs, string url, FileEntry? opened) : IRemoteEditLink
{
    FileEntry? _stamp = opened;

    public string Url => url;

    static bool Same(FileEntry? a, FileEntry? b) =>
        a is not null && b is not null && a.SizeBytes == b.SizeBytes && a.ModifiedAt?.ToUnixTimeSeconds() == b.ModifiedAt?.ToUnixTimeSeconds();

    public async Task<bool> ChangedOnServerAsync(CancellationToken ct = default)
    {
        var now = await fs.StatAsync(url, ct);
        return _stamp is not null && !Same(_stamp, now); // 처음에 정보를 못 얻었으면 비교할 수 없으니 바뀌지 않은 것으로 본다
    }

    public async Task UploadAsync(string localPath, CancellationToken ct = default)
    {
        await using (var input = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256 * 1024, useAsync: true))
        await using (var output = await fs.OpenWriteAsync(url, overwrite: true, ct))
        {
            await input.CopyToAsync(output, 256 * 1024, ct);
        }
        _stamp = await fs.StatAsync(url, ct) ?? _stamp;
    }
}
