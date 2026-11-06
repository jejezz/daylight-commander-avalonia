using Avalonia.Input;

namespace DaylightCommander.App;

/// <summary>
/// 키 보조키의 OS별 차이. 단축키와 끌어다 놓기는 Windows·Linux에서 Ctrl로 정해 두었는데, macOS에서는 같은 자리를 Command(⌘)가
/// 맡는다 (Finder에서 복사는 Option). 한 곳에서 판단해 두면 화면 코드는 OS를 몰라도 된다.
/// </summary>
internal static class Mods
{
    /// <summary>Ctrl (macOS는 ⌘도): 단축키의 "기본" 보조키.</summary>
    public static bool Primary(KeyModifiers m) =>
        m.HasFlag(KeyModifiers.Control) || (OperatingSystem.IsMacOS() && m.HasFlag(KeyModifiers.Meta));

    /// <summary>끌어다 놓을 때 복사를 뜻하는 키: Ctrl (macOS는 ⌘·Option도).</summary>
    public static bool Copy(KeyModifiers m) =>
        Primary(m) || (OperatingSystem.IsMacOS() && m.HasFlag(KeyModifiers.Alt));
}
