namespace GradingSystem.Application.Common;

public static class CsvImportHelper
{
    public static string[] SplitColumns(string line) =>
        line.Split(',').Select(c => c.Trim()).ToArray();

    public static bool IsHeaderRow(string line) =>
        line.TrimStart().StartsWith("username", StringComparison.OrdinalIgnoreCase);
}
