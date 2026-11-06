using System.ComponentModel;
using System.Diagnostics;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>활성 패널의 폴더에서 터미널을 연다 (원본 <c>OpenTerminal</c>).</summary>
public interface ITerminalLauncher
{
    /// <summary>실패하면 <see cref="FileOperationException"/>(<see cref="FileError.TerminalNotFound"/>).</summary>
    void Open(string directory);
}

/// <summary>
/// Windows는 Windows Terminal(<c>wt.exe -d</c>), 없으면 <c>cmd.exe</c>. macOS는 <c>open -a Terminal</c>. Linux는 gnome-terminal ·
/// konsole · xfce4-terminal을 차례로, 마지막으로 xterm. 터미널 창은 사용자가 닫을 때까지 떠 있으므로 종료를 기다리지 않는다.
/// 셸을 거치지 않고 인자를 하나씩 넘긴다 (경로의 따옴표·공백이 명령으로 해석되지 않게).
/// </summary>
public sealed class TerminalLauncher(bool? isWindows = null, bool? isMac = null, Action<IReadOnlyList<string>>? start = null) : ITerminalLauncher
{
    readonly bool _isWindows = isWindows ?? OperatingSystem.IsWindows();
    readonly bool _isMac = isMac ?? OperatingSystem.IsMacOS();

    /// <summary>시도할 명령줄들 (앞에서부터 하나씩 시도해 처음 되는 것을 쓴다). 테스트용으로 공개.</summary>
    public IReadOnlyList<IReadOnlyList<string>> CandidatesFor(string directory)
    {
        if (_isWindows)
            return [["wt.exe", "-d", directory], ["cmd.exe", "/c", "start", "cmd.exe", "/K"]]; // 작업 폴더를 폴더로 주고 시작하므로 cd가 필요 없다 (따옴표 문제도 없다)
        if (_isMac)
            return [["open", "-a", "Terminal", directory]];
        return
        [
            ["gnome-terminal", $"--working-directory={directory}"],
            ["konsole", "--workdir", directory],
            ["xfce4-terminal", $"--working-directory={directory}"],
            ["xterm", "-e", $"cd \"{directory}\" && exec $SHELL"],
        ];
    }

    public void Open(string directory)
    {
        foreach (var command in CandidatesFor(directory))
        {
            try
            {
                if (start is not null) start(command); else StartProcess(command, directory);
                return;
            }
            catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
            {
                // 이 터미널은 없다 — 다음 후보로.
            }
        }
        throw new FileOperationException(new FileError.TerminalNotFound());
    }

    static void StartProcess(IReadOnlyList<string> command, string directory)
    {
        var info = new ProcessStartInfo(command[0]) { UseShellExecute = false, WorkingDirectory = Directory.Exists(directory) ? directory : "" };
        foreach (var arg in command.Skip(1)) info.ArgumentList.Add(arg);
        using var _ = Process.Start(info);
    }
}
