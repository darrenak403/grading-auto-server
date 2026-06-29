using System.Text.RegularExpressions;

namespace GradingSystem.Application.Common;

public static partial class GradingRoundHelper
{
    // Accepts both the current "Round N" label and the legacy "Lần N" label so
    // rounds created before the rename still parse/sort correctly.
    [GeneratedRegex(@"^(?:Round|Lần) (\d+)$")]
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
        return $"Round {maxNumber + 1}";
    }

    public static string LatestRoundLabel(IEnumerable<string> existingRounds)
    {
        var rounds = existingRounds.ToList();
        if (rounds.Count == 0)
            return "Round 1";

        var withNumbers = rounds
            .Select(r => (Label: r, Number: ParseRoundNumber(r)))
            .Where(x => x.Number.HasValue)
            .ToList();

        return withNumbers.Count > 0
            ? withNumbers.OrderByDescending(x => x.Number!.Value).First().Label
            : rounds[0];
    }
}
