using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 파일 시스템 하나(로컬, 또는 연결된 원격 서버)에 대한 최소한의 작업. 원격 전송(<see cref="RemoteOperations"/>)이 로컬·SFTP를
/// 같은 방식으로 다루려고 쓴다. 경로는 항상 전체 경로 문자열 (원격은 <c>sftp://user@host/dir/file</c>).
/// 실패는 <see cref="FileOperationException"/>(또는 IOException)으로 올라온다.
/// </summary>
public interface IFileSystem
{
    Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct = default);

    /// <summary>항목 하나의 정보. 없으면 null.</summary>
    Task<FileEntry?> StatAsync(string path, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);

    /// <summary>쓰기용 스트림. <paramref name="overwrite"/>가 아니면 이미 있을 때 실패한다. 스트림을 닫아야 파일이 완성된다.</summary>
    Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct = default);

    Task CreateDirectoryAsync(string path, CancellationToken ct = default);

    /// <summary>파일이면 지우고, 폴더면 안의 것까지 모두 지운다.</summary>
    Task DeleteAsync(string path, CancellationToken ct = default);

    /// <summary>같은 파일 시스템 안에서 이름·위치를 바꾼다 (파일·폴더).</summary>
    Task RenameAsync(string from, string to, CancellationToken ct = default);

    /// <summary>수정 시각을 맞춘다 (복사한 파일이 원본과 같은 시각을 갖도록). 못 해도 오류는 아니다.</summary>
    Task SetModifiedAsync(string path, DateTimeOffset modified, CancellationToken ct = default);
}
