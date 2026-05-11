using System.Text.RegularExpressions;

namespace GradingSystem.Application.Common;

public static partial class StudentCode
{
    public static string ParseId(string code)
    {
        var m = IdPattern().Match(code);
        return m.Success ? m.Value : code;
    }

    [GeneratedRegex(@"[a-zA-Z]{2}\d{6}")]
    private static partial Regex IdPattern();
}
