namespace DaylightCommander.Domain;

/// <summary>
/// 패널에 표시되는 파일/폴더 한 항목 (원본 ARCHITECTURE.md 3장).
/// 원본은 <c>Uri location</c>과 <c>FileSourceType</c>(local/smb/ftp/sftp/webdav)을 갖지만, 지금은 로컬만 다루므로
/// 절대 경로 문자열 하나로 둔다. 원격 프로토콜을 붙이는 8단계에서 스킴을 도입한다.
/// </summary>
public sealed record FileEntry(
    string Path,
    string Name,
    bool IsDirectory,
    long? SizeBytes = null,
    DateTimeOffset? ModifiedAt = null,
    bool IsHidden = false);
