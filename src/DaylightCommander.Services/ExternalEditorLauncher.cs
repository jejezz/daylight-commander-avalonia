using System.Diagnostics;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>
/// 사용자가 설정에서 고른 외부 텍스트 편집기로 파일을 연다 (F4).
/// 셸을 거치지 않고 실행 파일에 파일 경로를 <b>인자 하나로</b> 넘긴다 — 공백이나 특수 문자가 든 경로도 그대로 간다.
/// macOS에서 <c>.app</c> 꾸러미를 고르면 <c>open -a &lt;앱&gt; &lt;파일&gt;</c>로 연다.
/// 실패는 <see cref="FileOperationException"/>(<see cref="FileError.EditorNotFound"/> / <see cref="FileError.EditorLaunchFailed"/>)로 올라온다.
/// </summary>
/// <param name="isMac">테스트에서 바꿀 수 있다.</param>
/// <param name="start">프로세스를 시작하는 부분. 테스트에서 바꿀 수 있다.</param>
public sealed class ExternalEditorLauncher(bool? isMac = null, Action<IReadOnlyList<string>>? start = null)
{
    readonly bool _isMac = isMac ?? OperatingSystem.IsMacOS();
    readonly Action<IReadOnlyList<string>> _start = start ?? StartProcess;

    /// <summary>실행할 명령줄. 실행 파일이 없으면 <see cref="FileError.EditorNotFound"/>.</summary>
    public IReadOnlyList<string> CommandFor(string executable, string file)
    {
        if (!File.Exists(executable) && !Directory.Exists(executable))
            throw new FileOperationException(new FileError.EditorNotFound(executable));
        return _isMac && executable.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? ["open", "-a", executable, file]
            : [executable, file];
    }

    public void Open(string executable, string file)
    {
        var command = CommandFor(executable, file);
        try
        {
            _start(command);
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new FileOperationException(new FileError.EditorLaunchFailed(e.Message));
        }
    }

    static void StartProcess(IReadOnlyList<string> command)
    {
        var info = new ProcessStartInfo(command[0]) { UseShellExecute = false };
        foreach (var arg in command.Skip(1)) info.ArgumentList.Add(arg);
        using var _ = Process.Start(info);
    }
}
