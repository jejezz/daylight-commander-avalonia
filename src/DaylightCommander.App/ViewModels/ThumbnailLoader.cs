using Avalonia.Media.Imaging;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.ViewModels;

/// <summary>
/// 이미지 썸네일 만들기: 메모리(LRU) → 디스크 캐시 → 원본을 줄여 디코딩 순서로 찾는다.
/// 동시에 몇 개만 디코딩하고(UI가 굳지 않게), 대기 중에 취소할 수 있다. 실패하면 null(형식 아이콘 그대로).
/// </summary>
public static class ThumbnailLoader
{
    static readonly LruCache<string, Bitmap> Memory = new(600);
    static readonly SemaphoreSlim Gate = new(3);

    /// <summary>디스크 캐시 폴더. 설치본이 아닌 곳에서도 쓸 수 있게 LocalAppData 아래에 둔다 (WebView2 데이터 폴더와 같은 이유).</summary>
    static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaylightCommander", "thumbs");

    /// <summary>
    /// 전부 백그라운드에서 한다: 키 계산(해시)·메모리 캐시 확인·디스크 읽기·디코딩·캐시 쓰기 어느 것도 UI 스레드를 쓰지 않는다.
    /// 호출한 쪽은 await만 하고, 결과(Bitmap)만 UI 스레드에서 받는다.
    /// </summary>
    public static Task<Bitmap?> LoadAsync(FileEntry entry, int tileSize, CancellationToken ct) =>
        Task.Run(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var width = ThumbnailPolicy.DecodeWidth(tileSize);
            var key = ThumbnailPolicy.CacheKey(entry, width);
            if (Memory.TryGet(key, out var hit)) return hit;

            await Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested(); // 기다리는 동안 타일이 사라졌으면 디코딩하지 않는다
                var bitmap = Decode(entry, key, width);
                if (bitmap is not null) Memory.Set(key, bitmap);
                return bitmap;
            }
            finally { Gate.Release(); }
        }, ct);

    static Bitmap? Decode(FileEntry entry, string key, int width)
    {
        var cached = Path.Combine(CacheDir, key + ".png");
        try
        {
            if (File.Exists(cached)) return new Bitmap(cached);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { /* 깨진 캐시는 새로 만든다 */ }

        try
        {
            using var stream = File.OpenRead(entry.Path);
            var bitmap = Bitmap.DecodeToWidth(stream, width);
            try
            {
                Directory.CreateDirectory(CacheDir);
                bitmap.Save(cached, new PngBitmapEncoderOptions());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* 캐시는 덤이다 */ }
            return bitmap;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null; // 못 읽는 그림: 형식 아이콘을 그대로 둔다
        }
    }
}
