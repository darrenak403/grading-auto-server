using GradingSystem.Application.Common;

namespace GradingSystem.Tests.Application.Common;

public class GradingRoundHelperTests
{
    [Theory]
    [InlineData("Lần 1", 1)]
    [InlineData("Lần 12", 12)]
    [InlineData("Lần 0", 0)]
    public void ParseRoundNumber_ValidLabel_ReturnsNumber(string label, int expected)
    {
        Assert.Equal(expected, GradingRoundHelper.ParseRoundNumber(label));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Round 1")]
    [InlineData("Lần")]
    [InlineData("Lần abc")]
    [InlineData("Lần1")]
    public void ParseRoundNumber_InvalidLabel_ReturnsNull(string label)
    {
        Assert.Null(GradingRoundHelper.ParseRoundNumber(label));
    }

    [Fact]
    public void ParseRoundNumber_TrimsWhitespace()
    {
        Assert.Equal(3, GradingRoundHelper.ParseRoundNumber("  Lần 3  "));
    }

    [Fact]
    public void NextRoundLabel_EmptyList_ReturnsLanOne()
    {
        Assert.Equal("Lần 1", GradingRoundHelper.NextRoundLabel([]));
    }

    [Fact]
    public void NextRoundLabel_SingleRound_IncrementsByOne()
    {
        Assert.Equal("Lần 2", GradingRoundHelper.NextRoundLabel(["Lần 1"]));
    }

    [Fact]
    public void NextRoundLabel_MultipleRounds_UsesMaxPlusOne()
    {
        Assert.Equal("Lần 4", GradingRoundHelper.NextRoundLabel(["Lần 1", "Lần 3", "Lần 2"]));
    }

    [Fact]
    public void NextRoundLabel_IgnoresUnparseableLabels()
    {
        Assert.Equal("Lần 3", GradingRoundHelper.NextRoundLabel(["Lần 2", "Custom Round Name"]));
    }

    [Fact]
    public void NextRoundLabel_AllUnparseable_ReturnsLanOne()
    {
        Assert.Equal("Lần 1", GradingRoundHelper.NextRoundLabel(["Custom A", "Custom B"]));
    }

    [Fact]
    public void NextRoundLabel_DuplicateLabels_StillIncrementsCorrectly()
    {
        Assert.Equal("Lần 2", GradingRoundHelper.NextRoundLabel(["Lần 1", "Lần 1"]));
    }

    [Fact]
    public void LatestRoundLabel_EmptyList_ReturnsLanOne()
    {
        Assert.Equal("Lần 1", GradingRoundHelper.LatestRoundLabel([]));
    }

    [Fact]
    public void LatestRoundLabel_SingleRound_ReturnsThatRound()
    {
        Assert.Equal("Lần 1", GradingRoundHelper.LatestRoundLabel(["Lần 1"]));
    }

    [Fact]
    public void LatestRoundLabel_MultipleRounds_ReturnsHighestNumbered()
    {
        Assert.Equal("Lần 5", GradingRoundHelper.LatestRoundLabel(["Lần 1", "Lần 5", "Lần 3"]));
    }

    [Fact]
    public void LatestRoundLabel_NoParseableRounds_ReturnsFirstInList()
    {
        Assert.Equal("Custom A", GradingRoundHelper.LatestRoundLabel(["Custom A", "Custom B"]));
    }

    [Fact]
    public void LatestRoundLabel_MixedParseableAndCustom_PrefersHighestParseable()
    {
        Assert.Equal("Lần 2", GradingRoundHelper.LatestRoundLabel(["Custom A", "Lần 2", "Lần 1"]));
    }
}
