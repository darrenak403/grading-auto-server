using GradingSystem.Application.Common;

namespace GradingSystem.Tests.Application.Common;

public class GradingRoundHelperTests
{
    [Theory]
    [InlineData("Round 1", 1)]
    [InlineData("Round 12", 12)]
    [InlineData("Round 0", 0)]
    public void ParseRoundNumber_ValidLabel_ReturnsNumber(string label, int expected)
    {
        Assert.Equal(expected, GradingRoundHelper.ParseRoundNumber(label));
    }

    [Theory]
    [InlineData("Lần 1", 1)]
    [InlineData("Lần 12", 12)]
    public void ParseRoundNumber_LegacyLanLabel_StillParses(string label, int expected)
    {
        Assert.Equal(expected, GradingRoundHelper.ParseRoundNumber(label));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Custom Round Name")]
    [InlineData("Round")]
    [InlineData("Round abc")]
    [InlineData("Round1")]
    public void ParseRoundNumber_InvalidLabel_ReturnsNull(string label)
    {
        Assert.Null(GradingRoundHelper.ParseRoundNumber(label));
    }

    [Fact]
    public void ParseRoundNumber_TrimsWhitespace()
    {
        Assert.Equal(3, GradingRoundHelper.ParseRoundNumber("  Round 3  "));
    }

    [Fact]
    public void NextRoundLabel_EmptyList_ReturnsRoundOne()
    {
        Assert.Equal("Round 1", GradingRoundHelper.NextRoundLabel([]));
    }

    [Fact]
    public void NextRoundLabel_SingleRound_IncrementsByOne()
    {
        Assert.Equal("Round 2", GradingRoundHelper.NextRoundLabel(["Round 1"]));
    }

    [Fact]
    public void NextRoundLabel_MultipleRounds_UsesMaxPlusOne()
    {
        Assert.Equal("Round 4", GradingRoundHelper.NextRoundLabel(["Round 1", "Round 3", "Round 2"]));
    }

    [Fact]
    public void NextRoundLabel_IgnoresUnparseableLabels()
    {
        Assert.Equal("Round 3", GradingRoundHelper.NextRoundLabel(["Round 2", "Custom Round Name"]));
    }

    [Fact]
    public void NextRoundLabel_AllUnparseable_ReturnsRoundOne()
    {
        Assert.Equal("Round 1", GradingRoundHelper.NextRoundLabel(["Custom A", "Custom B"]));
    }

    [Fact]
    public void NextRoundLabel_DuplicateLabels_StillIncrementsCorrectly()
    {
        Assert.Equal("Round 2", GradingRoundHelper.NextRoundLabel(["Round 1", "Round 1"]));
    }

    [Fact]
    public void NextRoundLabel_MixedLegacyAndCurrentLabels_UsesMaxPlusOne()
    {
        // Rounds created before the "Lần" -> "Round" rename still parse and count toward the max.
        Assert.Equal("Round 4", GradingRoundHelper.NextRoundLabel(["Lần 1", "Lần 3", "Round 2"]));
    }

    [Fact]
    public void LatestRoundLabel_EmptyList_ReturnsRoundOne()
    {
        Assert.Equal("Round 1", GradingRoundHelper.LatestRoundLabel([]));
    }

    [Fact]
    public void LatestRoundLabel_SingleRound_ReturnsThatRound()
    {
        Assert.Equal("Round 1", GradingRoundHelper.LatestRoundLabel(["Round 1"]));
    }

    [Fact]
    public void LatestRoundLabel_MultipleRounds_ReturnsHighestNumbered()
    {
        Assert.Equal("Round 5", GradingRoundHelper.LatestRoundLabel(["Round 1", "Round 5", "Round 3"]));
    }

    [Fact]
    public void LatestRoundLabel_NoParseableRounds_ReturnsFirstInList()
    {
        Assert.Equal("Custom A", GradingRoundHelper.LatestRoundLabel(["Custom A", "Custom B"]));
    }

    [Fact]
    public void LatestRoundLabel_MixedParseableAndCustom_PrefersHighestParseable()
    {
        Assert.Equal("Round 2", GradingRoundHelper.LatestRoundLabel(["Custom A", "Round 2", "Round 1"]));
    }

    [Fact]
    public void LatestRoundLabel_MixedLegacyAndCurrentLabels_PrefersHighestParseable()
    {
        Assert.Equal("Round 2", GradingRoundHelper.LatestRoundLabel(["Lần 1", "Round 2"]));
    }
}
