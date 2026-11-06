using System.Diagnostics;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>파일 관리자(탐색기·Finder 등)에서 항목을 보여 준다 (파일을 선택한 채로 폴더를 연다).</summary>
public interface IFileRevealer
{
    void Reveal(string path);
}

/// <summary>
/// Windows는 <c>explorer.exe /select,"경로"</c>, macOS는 <c>open -R 경로</c>, Linux는 <c>xdg-open 상위폴더</c>
/// (Linux에는 파일을 고른 채 여는 공통 방법이 없다). 셸을 거치지 않고 인자를 하나씩 넘긴다.
/// </summary>
public sealed class FileReveal(bool? isWindows = null, bool? isMac = null, Action<IReadOnlyList<string>>? start = null) : IFileRevealer
{
    readonly bool _isWindows = isWindows ?? OperatingSystem.IsWindows();
    readonly bool _isMac = isMac ?? OperatingSystem.IsMacOS();
    readonly Action<IReadOnlyList<string>> _start = start ?? StartProcess;

    /// <summary>실행할 명령줄 (테스트용으로 공개).</summary>
    public IReadOnlyList<string> CommandFor(string path)
    {
        if (_isWindows) return ["explorer.exe", $"/select,{path}"];
        if (_isMac) return ["open", "-R", path];
        if (Directory.Exists(path)) return ["xdg-open", path];
        var trimmed = path.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        return ["xdg-open", slash <= 0 ? "/" : trimmed[..slash]]; // 문자열로 자른다 (Linux 경로 규칙, 어느 OS에서든 같은 결과)
    }

    public void Reveal(string path)
    {
        try
        {
            _start(CommandFor(path));
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new FileOperationException(new FileError.Other(e.Message));
        }
    }

    static void StartProcess(IReadOnlyList<string> command)
    {
        var info = new ProcessStartInfo(command[0]) { UseShellExecute = false };
        foreach (var arg in command.Skip(1)) info.ArgumentList.Add(arg);
        using var _ = Process.Start(info);
    }
}
