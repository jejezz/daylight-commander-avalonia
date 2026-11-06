using System.Diagnostics;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 로컬 파일 작업: 복사·이동·삭제·이름변경·새 폴더/파일 (원본 <c>LocalFileOperations</c> 계열).
/// <list type="bullet">
/// <item>복사·이동·삭제는 취소를 존중한다. 복사 중 취소되면 쓰던 파일은 지운다.</item>
/// <item>항목 하나가 실패해도 나머지는 계속하고 <see cref="OperationResult.Failures"/>에 모아 돌려준다.
/// 폴더 안의 파일이 실패하면 그 최상위 항목은 거기서 멈춘다.</item>
/// <item>이름 충돌은 <see cref="ConflictResolver"/>에 묻는다. 폴더끼리는 합치고(merge), 파일↔폴더처럼 형식이 다르면
/// 덮어쓰기를 거부한다. 같은 위치로의 복사는 묻지 않고 "이름 (1)"로 만든다.</item>
/// <item>이동은 먼저 이름 바꾸기(rename)를 시도하고, 안 되면(다른 볼륨 등) 복사 후 원본 삭제로 넘어간다.
/// 일부라도 건너뛰었으면 원본은 지우지 않는다.</item>
/// <item>삭제는 기본이 휴지통이고, 휴지통을 쓸 수 없으면 호출한 쪽이 영구 삭제로 부른다 (<see cref="CanTrash"/>).</item>
/// </list>
/// 경로는 문자열이다 (도메인이 파일 시스템 형식에 의존하지 않게).
/// </summary>
public sealed class FileOperations(ITrash? trash = null, RemoteSessions? remote = null)
{
    readonly ITrash _trash = trash ?? Services.Trash.Default;
    readonly RemoteOperations? _remote = remote is null ? null : new RemoteOperations(remote);

    /// <summary>원격 서버가 낀 작업이면 원격 작업 처리기, 아니면 null (로컬 작업은 아래 코드가 한다).</summary>
    RemoteOperations? RemoteFor(IEnumerable<string> paths) => _remote is not null && RemoteOperations.Involves(paths) ? _remote : null;

    RemoteOperations RemoteOrThrow() => _remote ?? throw new FileOperationException(new FileError.RemoteUnsupported());

    public bool CanTrash => _trash.IsSupported;

    public Task<OperationResult> CopyAsync(
        IReadOnlyList<string> sources, string targetDir, ConflictResolver resolver,
        Action<OperationProgress>? onProgress = null, CancellationToken ct = default) =>
        RemoteFor([.. sources, targetDir]) is { } r ? r.CopyAsync(sources, targetDir, resolver, onProgress, ct)
            : Task.Run(() => new Transfer(resolver, onProgress, ct).RunAsync(sources, targetDir, move: false), ct);

    public Task<OperationResult> MoveAsync(
        IReadOnlyList<string> sources, string targetDir, ConflictResolver resolver,
        Action<OperationProgress>? onProgress = null, CancellationToken ct = default) =>
        RemoteFor([.. sources, targetDir]) is { } r ? r.MoveAsync(sources, targetDir, resolver, onProgress, ct)
            : Task.Run(() => new Transfer(resolver, onProgress, ct).RunAsync(sources, targetDir, move: true), ct);

    public Task<OperationResult> DeleteAsync(
        IReadOnlyList<string> paths, bool toTrash,
        Action<OperationProgress>? onProgress = null, CancellationToken ct = default) =>
        RemoteFor(paths) is { } r ? r.DeleteAsync(paths, onProgress, ct) : Task.Run(() =>
        {
            var failures = new List<OperationFailure>();
            var processed = 0;
            long total = paths.Count;
            onProgress?.Invoke(new OperationProgress(0, total, ""));
            for (var i = 0; i < paths.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var path = paths[i];
                onProgress?.Invoke(new OperationProgress(i, total, NameOf(path)));
                try
                {
                    if (toTrash) _trash.MoveToTrash(path); else DeleteTree(path);
                    processed++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    failures.Add(new OperationFailure(path, FileErrors.From(e, path)));
                }
            }
            onProgress?.Invoke(new OperationProgress(total, total, ""));
            return new OperationResult(processed, failures);
        }, ct);

    /// <summary>이름을 바꾼다. 같은 이름이 이미 있으면 <see cref="FileError.AlreadyExists"/> (대소문자만 바꾸는 것은 허용).</summary>
    public Task<string> RenameAsync(string path, string newName) => Locations.IsRemote(path) ? RemoteOrThrow().RenameAsync(path, newName) : Task.Run(() =>
    {
        ValidateName(newName);
        var target = Path.Combine(Path.GetDirectoryName(path) ?? "", newName);
        // 경로 비교는 Windows에서 대소문자를 무시하므로 이름 문자열로 직접 비교한다 (a.txt → A.txt는 바꿔야 한다).
        if (newName == Path.GetFileName(path)) return path;
        if (ExistsNoFollow(target))
        {
            if (!SamePath(path, target)) throw new FileOperationException(new FileError.AlreadyExists(newName));
            // 대소문자만 다른 이름(대소문자를 가리지 않는 파일 시스템): Move는 같은 파일이면 아무것도 안 하므로
            // 임시 이름을 거쳐 옮긴다.
            var temp = Path.Combine(Path.GetDirectoryName(path) ?? "", $".dc-rename-{Stopwatch.GetTimestamp()}");
            MoveEntry(path, temp);
            try
            {
                MoveEntry(temp, target);
            }
            catch (IOException)
            {
                MoveEntry(temp, path);
                throw;
            }
            return target;
        }
        MoveEntry(path, target);
        return target;
    });

    public Task<string> CreateDirectoryAsync(string parent, string name) => Locations.IsRemote(parent) ? RemoteOrThrow().CreateDirectoryAsync(parent, name) : Task.Run(() =>
    {
        ValidateName(name);
        var path = Path.Combine(parent, name);
        if (ExistsNoFollow(path)) throw new FileOperationException(new FileError.AlreadyExists(name));
        Directory.CreateDirectory(path);
        return path;
    });

    public Task<string> CreateFileAsync(string parent, string name) => Locations.IsRemote(parent) ? RemoteOrThrow().CreateFileAsync(parent, name) : Task.Run(() =>
    {
        ValidateName(name);
        var path = Path.Combine(parent, name);
        if (ExistsNoFollow(path)) throw new FileOperationException(new FileError.AlreadyExists(name));
        using (File.Open(path, FileMode.CreateNew, FileAccess.Write)) { }
        return path;
    });

    /// <summary>한 번의 복사/이동 실행 상태.</summary>
    sealed class Transfer(ConflictResolver resolver, Action<OperationProgress>? onProgress, CancellationToken ct)
    {
        const int BufferSize = 256 * 1024;
        const long ReportIntervalTicks = 50 * TimeSpan.TicksPerMillisecond;

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
            _total = sources.Sum(SizeOf);
            Report(force: true);
            var failures = new List<OperationFailure>();
            var processed = 0;
            foreach (var src in sources)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var name = Path.GetFileName(src.TrimEnd('\\', '/'));
                    if (string.IsNullOrEmpty(name)) throw new FileOperationException(new FileError.RootNotAllowed());
                    var dst = Path.Combine(targetDir, name);
                    var completed = move ? await MoveEntryAsync(src, dst) : await CopyEntryAsync(src, dst);
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

        /// <summary>끝까지 복사했으면 true, 일부라도 건너뛰었으면 false.</summary>
        async Task<bool> CopyEntryAsync(string src, string requestedDst)
        {
            ct.ThrowIfCancellationRequested();
            CheckNotIntoItself(src, requestedDst);
            var srcIsDir = IsDirectoryNoFollow(src);
            var dst = requestedDst;

            if (ExistsNoFollow(dst))
            {
                var dstIsDir = IsDirectoryNoFollow(dst);
                if (SamePath(src, dst))
                {
                    dst = UniqueName(dst);
                }
                else if (srcIsDir && dstIsDir)
                {
                    return await CopyChildrenAsync(src, dst);
                }
                else
                {
                    var choice = _sticky ?? await AskAsync(src, dst);
                    switch (choice)
                    {
                        case ConflictChoice.Skip:
                            _done += SizeOf(src);
                            Report();
                            return false;
                        case ConflictChoice.KeepBoth:
                            dst = UniqueName(dst);
                            break;
                        case ConflictChoice.Overwrite:
                            if (srcIsDir != dstIsDir)
                                throw new FileOperationException(new FileError.DifferentTypes(Path.GetFileName(dst)));
                            DeleteTree(dst);
                            break;
                        default:
                            throw new OperationCanceledException("cancelled by the user");
                    }
                }
            }

            if (IsLink(src))
            {
                CopyLink(src, dst);
            }
            else if (srcIsDir)
            {
                Directory.CreateDirectory(dst);
                var all = await CopyChildrenAsync(src, dst);
                try { Directory.SetLastWriteTimeUtc(dst, Directory.GetLastWriteTimeUtc(src)); } catch (IOException) { }
                return all;
            }
            else
            {
                CopyFile(src, dst);
            }
            return true;
        }

        async Task<bool> CopyChildrenAsync(string src, string dst)
        {
            var all = true;
            foreach (var child in Directory.EnumerateFileSystemEntries(src).ToList())
                if (!await CopyEntryAsync(child, Path.Combine(dst, Path.GetFileName(child)))) all = false;
            return all;
        }

        void CopyFile(string src, string dst)
        {
            _current = Path.GetFileName(src);
            Report(force: true);
            try
            {
                using var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize);
                using var output = new FileStream(dst, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize);
                var buffer = new byte[BufferSize];
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, n);
                    _done += n;
                    Report();
                }
            }
            catch
            {
                // 취소나 오류로 끊긴 불완전한 파일은 남기지 않는다.
                try { File.Delete(dst); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
            try { File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src)); } catch (IOException) { }
        }

        static void CopyLink(string src, string dst)
        {
            var target = new FileInfo(src).LinkTarget!;
            if (IsDirectoryNoFollow(src, linkAsDirectory: true)) Directory.CreateSymbolicLink(dst, target);
            else File.CreateSymbolicLink(dst, target);
        }

        async Task<bool> MoveEntryAsync(string src, string dst)
        {
            ct.ThrowIfCancellationRequested();
            CheckNotIntoItself(src, dst);
            if (SamePathNormalized(src, dst)) return false; // 같은 위치로의 이동: 할 일 없음

            if (!ExistsNoFollow(dst))
            {
                var size = SizeOf(src);
                try
                {
                    MoveEntry(src, dst);
                    _current = Path.GetFileName(dst);
                    _done += size;
                    Report(force: true);
                    return true;
                }
                catch (IOException)
                {
                    // 다른 볼륨 등: 복사 후 삭제로 넘어간다.
                }
            }
            var completed = await CopyEntryAsync(src, dst);
            if (completed) DeleteTree(src);
            return completed;
        }

        async Task<ConflictChoice> AskAsync(string src, string dst)
        {
            var decision = await resolver(src, dst, ct);
            if (decision.ApplyToAll) _sticky = decision.Choice;
            return decision.Choice;
        }
    }

    // --- 파일 시스템 도우미 ---

    static string NameOf(string path) =>
        Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : path;

    static bool IsLink(string path) => new FileInfo(path).LinkTarget is not null;

    /// <summary>링크를 따라가지 않고 폴더인지 본다. 폴더를 가리키는 링크는 폴더가 아니라 링크로 취급한다.</summary>
    static bool IsDirectoryNoFollow(string path, bool linkAsDirectory = false)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null)
            return linkAsDirectory && info.Attributes.HasFlag(FileAttributes.Directory);
        return Directory.Exists(path);
    }

    /// <summary>깨진 링크도 있는 것으로 본다.</summary>
    static bool ExistsNoFollow(string path) => File.Exists(path) || Directory.Exists(path) || IsLink(path);

    static StringComparison PathComparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    static string Full(string path) => Path.GetFullPath(path).TrimEnd('\\', '/');

    static bool SamePath(string a, string b) => string.Equals(Full(a), Full(b), PathComparison);

    static bool SamePathNormalized(string a, string b) => SamePath(a, b);

    /// <summary>파일이든 폴더든 옮긴다 (같은 볼륨이 아니면 폴더는 <see cref="IOException"/>).</summary>
    static void MoveEntry(string src, string dst)
    {
        if (Directory.Exists(src) && !IsLink(src)) Directory.Move(src, dst);
        else File.Move(src, dst);
    }

    /// <summary>폴더를 자기 자신 안으로 복사/이동하는 것을 막는다. 같은 위치는 통과 (호출한 쪽이 처리).</summary>
    static void CheckNotIntoItself(string src, string dst)
    {
        var s = Full(src);
        var d = Full(dst);
        if (!string.Equals(d, s, PathComparison)
            && d.StartsWith(s + Path.DirectorySeparatorChar, PathComparison)
            && IsDirectoryNoFollow(src))
            throw new FileOperationException(new FileError.IntoItself(NameOf(src)));
    }

    /// <summary>파일 이름으로 쓸 수 없는 값이면 <see cref="FileOperationException"/>. 플랫폼 공통으로 가장 엄격한 쪽에 맞춘다.</summary>
    internal static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new FileOperationException(new FileError.NameEmpty());
        if (name is "." or "..") throw new FileOperationException(new FileError.NameReserved(name));
        foreach (var ch in name)
            if ("\\/:*?\"<>|".Contains(ch) || ch < 32)
                throw new FileOperationException(new FileError.NameInvalidChar(ch));
    }

    /// <summary><c>a.txt</c> → <c>a (1).txt</c>, <c>dir</c> → <c>dir (1)</c>. 점이 맨 앞인 이름은 확장자 없음으로 본다.</summary>
    internal static string UniqueName(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileName(path);
        var dot = name.LastIndexOf('.');
        var stem = dot <= 0 ? name : name[..dot];
        var ext = dot <= 0 ? "" : name[dot..];
        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!ExistsNoFollow(candidate)) return candidate;
        }
    }

    /// <summary>폴더를 포함한 모든 것을 지운다. 심볼릭 링크는 따라가지 않고 링크만 지운다.</summary>
    internal static void DeleteTree(string path)
    {
        if (IsLink(path) || File.Exists(path))
        {
            DeleteOne(path, isDirectory: IsDirectoryNoFollow(path, linkAsDirectory: true));
            return;
        }
        foreach (var child in Directory.EnumerateFileSystemEntries(path).ToList()) DeleteTree(child);
        DeleteOne(path, isDirectory: true);
    }

    static void DeleteOne(string path, bool isDirectory)
    {
        try
        {
            if (isDirectory) Directory.Delete(path); else File.Delete(path);
        }
        catch (UnauthorizedAccessException)
        {
            // Windows의 읽기 전용 파일
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            if (isDirectory) Directory.Delete(path); else File.Delete(path);
        }
    }

    /// <summary>일반 파일 크기의 합. 읽을 수 없는 곳은 건너뛴다. 진행률 표시용일 뿐이다.</summary>
    internal static long SizeOf(string path)
    {
        try
        {
            if (IsLink(path)) return 0;
            if (File.Exists(path)) return new FileInfo(path).Length;
            if (!Directory.Exists(path)) return 0;
            long sum = 0;
            foreach (var child in Directory.EnumerateFileSystemEntries(path)) sum += SizeOf(child);
            return sum;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
