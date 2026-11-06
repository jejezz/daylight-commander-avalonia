using System.Text;
using System.Text.RegularExpressions;

namespace DaylightCommander.Services;

/// <summary>
/// 패턴 선택의 와일드카드 (원본 <c>_globToRegExp</c>): <c>*</c>은 아무 글자 여럿(0개 포함), <c>?</c>은 글자 하나, 나머지는 그대로.
/// 이름 전체가 맞아야 하고 대소문자는 가리지 않는다.
/// </summary>
public static class GlobPattern
{
    public static Regex ToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        foreach (var ch in pattern)
        {
            sb.Append(ch switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(ch.ToString()),
            });
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}
