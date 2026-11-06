namespace DaylightCommander.Services;

/// <summary>파일·폴더 하나의 속성 (속성 창의 윗부분). 폴더 크기는 오래 걸릴 수 있어 따로 잰다 (<see cref="FileProperties.MeasureAsync"/>).</summary>
public sealed record PropertiesInfo(
    string Path, string Name, string Parent, bool IsDirectory, long? FileSize,
    DateTime? Created, DateTime? Modified, DateTime? Accessed, FileAttributes Attributes, string? LinkTarget);

/// <summary>선택한 항목들의 합계. <see cref="Complete"/>가 false면 읽지 못한 곳이 있어 합계가 모자랄 수 있다.</summary>
public sealed record PropertiesTotals(long Bytes, int Files, int Folders, bool Complete);

/// <summary>속성 창(F-바의 속성)이 읽는 정보.</summary>
public static class FileProperties
{
    public static PropertiesInfo Read(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var isDirectory = Directory.Exists(path);
        FileSystemInfo info = isDirectory ? new DirectoryInfo(path) : new FileInfo(path);
        if (!info.Exists && info.LinkTarget is null) throw new FileNotFoundException(null, path);
        var name = System.IO.Path.GetFileName(trimmed);
        return new PropertiesInfo(
            Path: path,
            Name: name.Length > 0 ? name : path, // 드라이브 루트는 이름이 비어 있다
            Parent: System.IO.Path.GetDirectoryName(trimmed) ?? "",
            IsDirectory: isDirectory,
            FileSize: info is FileInfo f ? f.Length : null,
            Created: info.CreationTime,
            Modified: info.LastWriteTime,
            Accessed: info.LastAccessTime,
            Attributes: info.Attributes,
            LinkTarget: info.LinkTarget);
    }

    /// <summary>
    /// 항목들의 총 크기와 파일·폴더 개수를 센다. 폴더는 안쪽까지 훑고(심볼릭 링크는 따라가지 않는다), 읽지 못하는 곳은 건너뛰며
    /// <see cref="PropertiesTotals.Complete"/>를 끈다. <paramref name="progress"/>로 중간 값을 알린다. 취소하면 <see cref="OperationCanceledException"/>.
    /// 전달한 폴더 자신은 폴더 개수에 세지 않고, 안에 든 것만 센다.
    /// </summary>
    public static Task<PropertiesTotals> MeasureAsync(IReadOnlyList<string> paths, IProgress<PropertiesTotals>? progress = null, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            long bytes = 0;
            var files = 0;
            var folders = 0;
            var complete = true;
            var lastReport = Environment.TickCount64;

            void Report(bool force = false)
            {
                var now = Environment.TickCount64;
                if (!force && now - lastReport < 100) return;
                lastReport = now;
                progress?.Report(new PropertiesTotals(bytes, files, folders, complete));
            }

            void Walk(string path, bool top)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(path);
                    if (info.LinkTarget is not null) { files++; return; } // 링크는 따라가지 않고 파일 하나로 센다
                    if (Directory.Exists(path))
                    {
                        if (!top) folders++;
                        foreach (var child in Directory.EnumerateFileSystemEntries(path)) Walk(child, top: false);
                    }
                    else
                    {
                        var length = info.Length; // 읽지 못하면 여기서 예외 — 개수에도 넣지 않는다
                        files++;
                        bytes += length;
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    complete = false;
                }
                Report();
            }

            foreach (var path in paths) Walk(path, top: true);
            var result = new PropertiesTotals(bytes, files, folders, complete);
            progress?.Report(result);
            return result;
        }, ct);
}
