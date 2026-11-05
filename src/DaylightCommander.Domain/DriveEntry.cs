namespace DaylightCommander.Domain;

/// <summary>드라이브 종류. Windows는 <c>GetDriveType</c>으로 구분하고, macOS/Linux는 아직 구분할 방법이 없어 전부 <see cref="Unknown"/>이다.</summary>
public enum DriveKind { Unknown, Fixed, Removable, Network }

/// <summary>
/// 드라이브/볼륨 전환 메뉴에 표시되는 항목 (로컬 디스크, 외장 디스크, 마운트된 네트워크 드라이브 등).
/// SMB는 OS 마운트 방식이라 <c>/Volumes</c> 등에 그대로 잡힌다 (원본 ARCHITECTURE.md 4장).
/// </summary>
public sealed record DriveEntry(string Name, string Path, DriveKind Kind = DriveKind.Unknown);
