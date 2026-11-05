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

public static class Trash
{
    /// <summary>이 OS의 휴지통. 지원하지 않으면 <see cref="NoTrash"/>.</summary>
    public static ITrash Default { get; } = OperatingSystem.IsWindows() ? new WindowsTrash() : new NoTrash();
}
