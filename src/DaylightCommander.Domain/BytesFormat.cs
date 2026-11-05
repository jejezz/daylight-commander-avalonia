using System.Globalization;

namespace DaylightCommander.Domain;

public static class BytesFormat
{
    static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>원본 <c>formatBytes</c>: 1024 단위, B는 소수점 없이 나머지는 한 자리. null이면 <c>--</c>.</summary>
    public static string Format(long? bytes)
    {
        if (bytes is null) return "--";
        double value = bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var text = value.ToString(unit == 0 ? "F0" : "F1", CultureInfo.InvariantCulture);
        return $"{text} {Units[unit]}";
    }
}
