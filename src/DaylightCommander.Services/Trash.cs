using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>휴지통으로 보내기. OS마다 방법이 달라서 인터페이스로 둔다 (테스트에서 바꿀 수 있다).</summary>
public interface ITrash
{
    bool IsSupported { get; }

    /// <summary>실패하면 <see cref="FileOperationException"/>(<see cref="FileError.TrashFailed"/>).</summary>
    void MoveToTrash(string path);
}

/// <summary>휴지통을 쓸 수 없는 환경. 호출한 쪽이 영구 삭제로 안내한다.</summary>
public sealed class NoTrash : ITrash
{
    public bool IsSupported => false;

    public void MoveToTrash(string path) =>
        throw new FileOperationException(new FileError.TrashFailed(Path.GetFileName(path), null));
}

/// <summary>Windows 휴지통 (<c>SHFileOperationW</c> + <c>FOF_ALLOWUNDO</c>). 다른 OS는 아직 지원하지 않는다.</summary>
public sealed partial class WindowsTrash : ITrash
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public void MoveToTrash(string path)
    {
        var name = Path.GetFileName(path.TrimEnd('\\', '/'));
        if (!OperatingSystem.IsWindows()) throw new FileOperationException(new FileError.TrashFailed(name, null));

        // pFrom은 널 문자 두 개로 끝나는 목록이다 (StringToHGlobalUni가 하나를 더해 준다).
        var from = Marshal.StringToHGlobalUni(Path.GetFullPath(path) + "\0");
        try
        {
            var op = new ShFileOpStruct
            {
                wFunc = FoDelete,
                pFrom = from,
                fFlags = (ushort)(FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi),
            };
            var code = SHFileOperationW(ref op);
            if (code != 0 || op.fAnyOperationsAborted != 0)
                throw new FileOperationException(new FileError.TrashFailed(name, $"0x{code:X}"));
        }
        finally
        {
            Marshal.FreeHGlobal(from);
        }
    }

    const uint FoDelete = 0x0003;
    const int FofSilent = 0x0004;
    const int FofNoConfirmation = 0x0010;
    const int FofAllowUndo = 0x0040;
    const int FofNoErrorUi = 0x0400;

    [StructLayout(LayoutKind.Sequential)]
    struct ShFileOpStruct
    {
        public nint hwnd;
        public uint wFunc;
        public nint pFrom;
        public nint pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public nint hNameMappings;
        public nint lpszProgressTitle;
    }

    [LibraryImport("shell32.dll", EntryPoint = "SHFileOperationW")]
    private static partial int SHFileOperationW(ref ShFileOpStruct op);
}

/// <summary>
/// 외부 프로그램에 휴지통 보내기를 맡기는 구현 (macOS·Linux). 프로그램을 셸 없이 인자로 부르고, 종료 코드가 0이 아니면
/// <see cref="FileError.TrashFailed"/>로 알린다. 어떤 명령을 부를지는 <paramref name="commands"/>가 정하고 앞에서부터 하나씩 시도한다.
/// </summary>
public sealed class CommandTrash(Func<string, IReadOnlyList<IReadOnlyList<string>>> commands, Func<bool> isSupported, Func<IReadOnlyList<string>, (int ExitCode, string Error)>? run = null) : ITrash
{
    public bool IsSupported => isSupported();

    public void MoveToTrash(string path)
    {
        var name = Path.GetFileName(path.TrimEnd((char)92, '/'));
        var full = Path.GetFullPath(path);
        string? lastError = null;
        foreach (var command in commands(full))
        {
            try
            {
                var (code, error) = (run ?? Run)(command);
                if (code == 0) return;
                lastError = string.IsNullOrWhiteSpace(error) ? $"exit {code}" : error.Trim();
            }
            catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
            {
                lastError = e.Message; // 이 프로그램이 없다 — 다음 후보로
            }
        }
        throw new FileOperationException(new FileError.TrashFailed(name, lastError));
    }

    static (int, string) Run(IReadOnlyList<string> command)
    {
        var info = new ProcessStartInfo(command[0]) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (var arg in command.Skip(1)) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException(command[0]);
        var error = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(30_000))
        {
            try { process.Kill(); } catch (InvalidOperationException) { }
            return (-1, "timeout");
        }
        return (process.ExitCode, error.GetAwaiter().GetResult());
    }
}

public static class Trash
{
    /// <summary>
    /// macOS는 Finder에 시키는 <c>osascript</c> (처음 한 번 Finder 제어를 허락해야 한다). Linux는 GLib의 <c>gio trash</c>, 없으면
    /// <c>trash-put</c> (trash-cli). 둘 다 없으면 지원하지 않는 것으로 보고 호출한 쪽이 영구 삭제로 안내한다.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> MacCommands(string fullPath) =>
        [["osascript", "-e", $"tell application \"Finder\" to delete POSIX file \"{EscapeAppleScript(fullPath)}\""]];

    public static IReadOnlyList<IReadOnlyList<string>> LinuxCommands(string fullPath) =>
        [["gio", "trash", "--", fullPath], ["trash-put", "--", fullPath]];

    /// <summary>AppleScript 문자열 안에서 따옴표와 역슬래시는 앞에 역슬래시를 붙인다.</summary>
    internal static string EscapeAppleScript(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static bool OnPath(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, tool)));

    /// <summary>이 OS의 휴지통. 지원하지 않으면 <see cref="NoTrash"/>.</summary>
    public static ITrash Default { get; } = Create();

    static ITrash Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsTrash();
        if (OperatingSystem.IsMacOS()) return new CommandTrash(MacCommands, () => true);
        if (OperatingSystem.IsLinux()) return new CommandTrash(LinuxCommands, () => OnPath("gio") || OnPath("trash-put"));
        return new NoTrash();
    }
}
