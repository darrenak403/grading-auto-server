using System.Text.RegularExpressions;

namespace GradingSystem.Application.Common;

public static partial class GradingRoundHelper
{
    [GeneratedRegex(@"^Lần (\d+)$")]
    private static partial Regex RoundLabelRegex();

    public static int? ParseRoundNumber(string label)
    {
        var m = RoundLabelRegex().Match(label.Trim());
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    public static string NextRoundLabel(IEnumerable<string> existingRounds)
    {
        var maxNumber = existingRounds
            .Select(ParseRoundNumber)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .DefaultIfEmpty(0)
            .Max();
        return $"Lần {maxNumber + 1}";
    }

    public static string LatestRoundLabel(IEnumerable<string> existingRounds)
    {
        var rounds = existingRounds.ToList();
        if (rounds.Count == 0)
            return "Lần 1";

        var withNumbers = rounds
            .Select(r => (Label: r, Number: ParseRoundNumber(r)))
            .Where(x => x.Number.HasValue)
            .ToList();

        return withNumbers.Count > 0
            ? withNumbers.OrderByDescending(x => x.Number!.Value).First().Label
            : rounds[0];
    }
}
