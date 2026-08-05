using System.Text.Json;
using GradingSystem.Worker.Services;

namespace GradingSystem.Tests.Worker.Services;

public class TestRunnerJsonComparisonTests
{
    [Fact]
    public void JsonSubsetScore_WhenNumbersHaveDifferentScale_TreatsValuesAsEqual()
    {
        using var actual = JsonDocument.Parse("""{"price":45000.00,"priceWithTax":47250.000}""");
        using var expected = JsonDocument.Parse("""{"price":45000,"priceWithTax":47250}""");

        var result = TestRunner.JsonSubsetScore(actual.RootElement, expected.RootElement);

        Assert.Equal((2, 2), result);
    }

    [Fact]
    public void JsonSubsetScore_WhenEquivalentNumbersExceedDecimalRange_TreatsValuesAsEqual()
    {
        using var actual = JsonDocument.Parse("""{"value":1e29}""");
        using var expected = JsonDocument.Parse("""{"value":100000000000000000000000000000}""");

        var result = TestRunner.JsonSubsetScore(actual.RootElement, expected.RootElement);

        Assert.Equal((1, 1), result);
    }

    [Fact]
    public void JsonSubsetScore_WhenNumbersDiffer_DoesNotMatchValues()
    {
        using var actual = JsonDocument.Parse("""{"price":45000.01}""");
        using var expected = JsonDocument.Parse("""{"price":45000}""");

        var result = TestRunner.JsonSubsetScore(actual.RootElement, expected.RootElement);

        Assert.Equal((0, 1), result);
    }
}
