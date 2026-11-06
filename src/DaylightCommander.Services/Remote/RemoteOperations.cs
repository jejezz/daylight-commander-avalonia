using System.Diagnostics;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 원격이 낀 파일 작업(복사·이동·삭제·이름변경·새 폴더/파일). 로컬 작업(<see cref="FileOperations"/>)과 같은 규칙을 따른다:
/// 이름이 겹치면 <see cref="ConflictResolver"/>에 묻고, 폴더끼리는 합치며, 같은 곳으로의 복사는 묻지 않고 "이름 (1)"로 만들고,
/// 항목 하나가 실패해도 나머지는 계속한다. 로컬↔원격, 원격↔원격 모두 <see cref="IFileSystem"/> 둘 사이를 스트림으로 흘려 보내므로
/// 임시 폴더를 거치지 않는다. 이동은 같은 서버 안이면 이름 바꾸기로, 아니면 복사 후 원본 삭제로 한다.
/// </summary>
public sealed class RemoteOperations(RemoteSessions sessions)
{
    const int BufferSize = 256 * 1024;
    const long ReportIntervalTicks = 50 * TimeSpan.TicksPerMillisecond;

    IFileSystem Fs(string path) => Locations.IsRemote(path) ? sessions.FileSystemFor(path) : LocalFileSystem.Instance;

    /// <summary>복사·이동·삭제·이름변경 중 하나라도 원격이 끼어 있는가.</summary>
    public static bool Involves(IEnumerable<string> paths) => paths.Any(Locations.IsRemote);

    public Task<OperationResult> CopyAsync(IReadOnlyList<string> sources, string targetDir, ConflictResolver resolver,
        Action<OperationProgress>? onProgress = null, CancellationToken ct = default) =>
        new Transfer(this, resolver, onProgress, ct).RunAsync(sources, targetDir, move: false);

    public Task<OperationResult> MoveAsync(IReadOnlyList<string> sources, string targetDir, ConflictResolver resolver,
        Action<OperationProgress>? onProgress = null, CancellationToken ct = default) =>
        new Transfer(this, resolver, onProgress, ct).RunAsync(sources, targetDir, move: true);

    public async Task<OperationResult> DeleteAsync(IReadOnlyList<string> paths, Action<OperationProgress>? onProgress = null, CancellationToken ct = default)
    {
        var failures = new List<OperationFailure>();
        var processed = 0;
        long total = paths.Count;
        onProgress?.Invoke(new OperationProgress(0, total, ""));
        for (var i = 0; i < paths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            onProgress?.Invoke(new OperationProgress(i, total, Locations.NameOf(paths[i])));
            try
            {
                await Fs(paths[i]).DeleteAsync(paths[i], ct);
                processed++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failures.Add(new OperationFailure(paths[i], FileErrors.From(e, paths[i])));
            }
        }
        onProgress?.Invoke(new OperationProgress(total, total, ""));
        return new OperationResult(processed, failures);
    }

    public async Task<string> RenameAsync(string path, string newName, CancellationToken ct = default)
    {
        FileOperations.ValidateName(newName);
        if (newName == Locations.NameOf(path)) return path;
        var parent = Locations.Parent(path) ?? throw new FileOperationException(new FileError.RootNotAllowed());
        var target = Locations.Combine(parent, newName);
        var fs = Fs(path);
        if (await fs.StatAsync(target, ct) is not null) throw new FileOperationException(new FileError.AlreadyExists(newName));
        await fs.RenameAsync(path, target, ct);
        return target;
    }

    public async Task<string> CreateDirectoryAsync(string parent, string name, CancellationToken ct = default)
    {
        FileOperations.ValidateName(name);
        var path = Locations.Combine(parent, name);
        var fs = Fs(path);
        if (await fs.StatAsync(path, ct) is not null) throw new FileOperationException(new FileError.AlreadyExists(name));
        await fs.CreateDirectoryAsync(path, ct);
        return path;
    }

    public async Task<string> CreateFileAsync(string parent, string name, CancellationToken ct = default)
    {
        FileOperations.ValidateName(name);
        var path = Locations.Combine(parent, name);
        var fs = Fs(path);
        if (await fs.StatAsync(path, ct) is not null) throw new FileOperationException(new FileError.AlreadyExists(name));
        await using var stream = await fs.OpenWriteAsync(path, overwrite: false, ct);
        return path;
    }

    /// <summary>한 번의 복사/이동 실행 상태.</summary>
    sealed class Transfer(RemoteOperations owner, ConflictResolver resolver, Action<OperationProgress>? onProgress, CancellationToken ct)
    {
        long _total, _done, _lastReport;
        string _current = "";
        ConflictChoice? _sticky;

        void Report(bool force = false)
        {
            var now = Stopwatch.GetTimestamp();
            if (!force && (now - _lastReport) * TimeSpan.TicksPerSecond / Stopwatch.Frequency <= ReportIntervalTicks) return;
            _lastReport = now;
            onProgress?.Invoke(new OperationProgress(_done, _total, _current));
        }

        public async Task<OperationResult> RunAsync(IReadOnlyList<string> sources, string targetDir, bool move)
        {
            var failures = new List<OperationFailure>();
            var processed = 0;
            // 진행률의 전체 크기: 읽을 수 없으면 0으로 두고 계속한다.
            foreach (var src in sources)
            {
                try { _total += await SizeOfAsync(src); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            Report(force: true);

            foreach (var src in sources)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var name = Locations.NameOf(src);
                    if (Locations.Parent(src) is null) throw new FileOperationException(new FileError.RootNotAllowed());
                    var dst = Locations.Combine(targetDir, name);
                    var entry = await owner.Fs(src).StatAsync(src, ct) ?? throw new FileOperationException(new FileError.NotFound(name));
                    var completed = move ? await MoveEntryAsync(entry, dst) : await CopyEntryAsync(entry, dst);
                    if (completed) processed++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    failures.Add(new OperationFailure(src, FileErrors.From(e, src)));
                }
            }
            Report(force: true);
            return new OperationResult(processed, failures);
        }

        async Task<long> SizeOfAsync(string path)
        {
            var fs = owner.Fs(path);
            var entry = await fs.StatAsync(path, ct);
            if (entry is null) return 0;
            if (!entry.IsDirectory) return entry.SizeBytes ?? 0;
            long sum = 0;
            foreach (var child in await fs.ListAsync(path, ct)) sum += child.IsDirectory ? await SizeOfAsync(child.Path) : child.SizeBytes ?? 0;
            return sum;
        }

        static bool IsSameLocation(string a, string b) =>
            string.Equals(Locations.IsRemote(a) ? Locations.NormalizeRemote(a) : System.IO.Path.GetFullPath(a),
                          Locations.IsRemote(b) ? Locations.NormalizeRemote(b) : System.IO.Path.GetFullPath(b),
                          Locations.IsRemote(a) || OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

        static bool IsInside(string outer, string inner)
        {
            var o = Locations.IsRemote(outer) ? Locations.NormalizeRemote(outer) : System.IO.Path.GetFullPath(outer);
            var i = Locations.IsRemote(inner) ? Locations.NormalizeRemote(inner) : System.IO.Path.GetFullPath(inner);
            var sep = Locations.IsRemote(outer) ? "/" : System.IO.Path.DirectorySeparatorChar.ToString();
            return i.StartsWith(o.TrimEnd('/', '\\') + sep, Locations.IsRemote(outer) || OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>같은 파일 시스템(같은 서버)의 같은 위치로 복사하면 묻지 않고 새 이름을 쓴다.</summary>
        async Task<string> UniqueNameAsync(IFileSystem fs, string path)
        {
            var dir = Locations.Parent(path) ?? "";
            var name = Locations.NameOf(path);
            var dot = name.LastIndexOf('.');
            var stem = dot <= 0 ? name : name[..dot];
            var ext = dot <= 0 ? "" : name[dot..];
            for (var i = 1; ; i++)
            {
                var candidate = Locations.Combine(dir, $"{stem} ({i}){ext}");
                if (await fs.StatAsync(candidate, ct) is null) return candidate;
            }
        }

        /// <summary>끝까지 복사했으면 true, 일부라도 건너뛰었으면 false.</summary>
        async Task<bool> CopyEntryAsync(FileEntry src, string requestedDst)
        {
            ct.ThrowIfCancellationRequested();
            var srcFs = owner.Fs(src.Path);
            var dstFs = owner.Fs(requestedDst);
            if (src.IsDirectory && (IsSameLocation(src.Path, requestedDst) || IsInside(src.Path, requestedDst)) && ReferenceEquals(srcFs, dstFs))
                throw new FileOperationException(new FileError.IntoItself(src.Name));

            var dst = requestedDst;
            var existing = await dstFs.StatAsync(dst, ct);
            if (existing is not null)
            {
                if (ReferenceEquals(srcFs, dstFs) && IsSameLocation(src.Path, dst))
                {
                    dst = await UniqueNameAsync(dstFs, dst);
                }
                else if (src.IsDirectory && existing.IsDirectory)
                {
                    return await CopyChildrenAsync(src, dst, srcFs);
                }
                else
                {
                    var choice = _sticky ?? await AskAsync(src.Path, dst);
                    switch (choice)
                    {
                        case ConflictChoice.Skip:
                            _done += src.IsDirectory ? 0 : src.SizeBytes ?? 0;
                            Report();
                            return false;
                        case ConflictChoice.KeepBoth:
                            dst = await UniqueNameAsync(dstFs, dst);
                            break;
                        case ConflictChoice.Overwrite:
                            if (src.IsDirectory != existing.IsDirectory)
                                throw new FileOperationException(new FileError.DifferentTypes(Locations.NameOf(dst)));
                            await dstFs.DeleteAsync(dst, ct);
                            break;
                        default:
                            throw new OperationCanceledException("cancelled by the user");
                    }
                }
            }

            if (src.IsDirectory)
            {
                await dstFs.CreateDirectoryAsync(dst, ct);
                var all = await CopyChildrenAsync(src, dst, srcFs);
                if (src.ModifiedAt is { } m) await dstFs.SetModifiedAsync(dst, m, ct);
                return all;
            }
            await CopyFileAsync(src, srcFs, dst, dstFs);
            return true;
        }

        async Task<bool> CopyChildrenAsync(FileEntry src, string dst, IFileSystem srcFs)
        {
            var all = true;
            foreach (var child in await srcFs.ListAsync(src.Path, ct))
                if (!await CopyEntryAsync(child, Locations.Combine(dst, child.Name))) all = false;
            return all;
        }

        async Task CopyFileAsync(FileEntry src, IFileSystem srcFs, string dst, IFileSystem dstFs)
        {
            _current = src.Name;
            Report(force: true);
            try
            {
                await using (var input = await srcFs.OpenReadAsync(src.Path, ct))
                await using (var output = await dstFs.OpenWriteAsync(dst, overwrite: false, ct))
                {
                    var buffer = new byte[BufferSize];
                    int n;
                    while ((n = await input.ReadAsync(buffer, ct)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, n), ct);
                        _done += n;
                        Report();
                    }
                }
            }
            catch
            {
                // 취소나 오류로 끊긴 불완전한 파일은 남기지 않는다.
                try { await dstFs.DeleteAsync(dst); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                throw;
            }
            if (src.ModifiedAt is { } m) await dstFs.SetModifiedAsync(dst, m, ct);
        }

        async Task<bool> MoveEntryAsync(FileEntry src, string dst)
        {
            ct.ThrowIfCancellationRequested();
            var srcFs = owner.Fs(src.Path);
            var dstFs = owner.Fs(dst);
            if (ReferenceEquals(srcFs, dstFs))
            {
                if (src.IsDirectory && (IsSameLocation(src.Path, dst) || IsInside(src.Path, dst)) && !IsSameLocation(src.Path, dst))
                    throw new FileOperationException(new FileError.IntoItself(src.Name));
                if (IsSameLocation(src.Path, dst)) return false; // 같은 위치로의 이동: 할 일 없음
                if (await dstFs.StatAsync(dst, ct) is null)
                {
                    try
                    {
                        await srcFs.RenameAsync(src.Path, dst, ct);
                        _current = src.Name;
                        _done += src.IsDirectory ? 0 : src.SizeBytes ?? 0;
                        Report(force: true);
                        return true;
                    }
                    catch (IOException)
                    {
                        // 이름 바꾸기가 안 되는 곳: 복사 후 삭제로 넘어간다.
                    }
                }
            }
            var completed = await CopyEntryAsync(src, dst);
            if (completed) await srcFs.DeleteAsync(src.Path, ct);
            return completed;
        }

        async Task<ConflictChoice> AskAsync(string src, string dst)
        {
            var decision = await resolver(src, dst, ct);
            if (decision.ApplyToAll) _sticky = decision.Choice;
            return decision.Choice;
        }
    }
}
