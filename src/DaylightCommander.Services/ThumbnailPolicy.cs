using System.Security.Cryptography;
using System.Text;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 미리보기(썸네일)를 만들지 정하는 규칙 — UI와 무관. 이미지 파일만, 너무 크지 않은 것만, 원격(sftp/ftp/webdav) 경로는 건너뛴다
/// (격자 보기가 폴더를 열 때마다 네트워크로 파일을 내려받지 않도록). 건너뛴 항목은 기존 파일 형식 아이콘을 그대로 보여 준다.
/// </summary>
public static class ThumbnailPolicy
{
    /// <summary>Avalonia가 기본으로 디코딩하는 형식. 동영상·폴더 미리보기는 셸 연동(2차)에서 다룬다.</summary>
    static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jpe", ".gif", ".bmp", ".webp",
    };

    /// <summary>이보다 큰 이미지는 디코딩 비용이 커서 형식 아이콘으로 둔다.</summary>
    public const long MaxImageBytes = 64L * 1024 * 1024;

    public static bool IsImageName(string name) => ImageExtensions.Contains(Path.GetExtension(name));

    /// <summary>이 항목의 내용 썸네일을 만들어 볼 만한가 (폴더·원격·비이미지·너무 큰 파일은 아니오).</summary>
    public static bool ShouldGenerate(FileEntry entry) =>
        !entry.IsDirectory
        && IsImageName(entry.Name)
        && !Locations.IsRemote(entry.Path)
        && (entry.SizeBytes is null || entry.SizeBytes <= MaxImageBytes);

    /// <summary>
    /// 캐시 키: 경로·수정 시각·크기·요청 크기가 모두 같을 때만 같은 썸네일이다 (파일이 바뀌면 키가 달라진다).
    /// 파일 이름으로 쓰기 좋게 해시(소문자 16진 32자)로 돌려준다.
    /// </summary>
    public static string CacheKey(FileEntry entry, int size)
    {
        var text = $"{entry.Path}\n{entry.ModifiedAt?.UtcTicks}\n{entry.SizeBytes}\n{size}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 16));
    }

    /// <summary>썸네일을 디코딩할 픽셀 너비. 고해상도 화면을 위해 타일의 2배, 128 단위로 올려 캐시를 공유한다.</summary>
    public static int DecodeWidth(int tileSize) => Math.Max(128, (tileSize * 2 + 127) / 128 * 128);
}
