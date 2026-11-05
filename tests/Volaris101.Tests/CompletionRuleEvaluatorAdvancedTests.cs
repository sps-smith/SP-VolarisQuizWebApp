using Volaris101.Application.Services;

namespace Volaris101.Tests;

public class CompletionRuleEvaluatorAdvancedTests
{
    [Fact]
    public void GetDerivedCompletionDate_ReturnsGrandfatherCompletionDate_WhenNinePublishedModulesArePassedBeforeCutoff()
    {
        var dates = Enumerable.Range(1, 9)
            .Select(day => new DateTimeOffset(2025, 12, day, 9, 0, 0, TimeSpan.Zero))
            .Cast<DateTimeOffset?>()
            .ToList();

        var result = CompletionRuleEvaluator.GetDerivedCompletionDate(10, 9, dates);

        Assert.Equal(new DateTimeOffset(2025, 12, 9, 9, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetDerivedCompletionDate_ReturnsLastPublishedCompletionDate_WhenAllPublishedModulesArePassed()
    {
        var dates = Enumerable.Range(1, 10)
            .Select(day => new DateTimeOffset(2026, 7, day, 9, 0, 0, TimeSpan.Zero))
            .Cast<DateTimeOffset?>()
            .ToList();

        var result = CompletionRuleEvaluator.GetDerivedCompletionDate(10, 10, dates);

        Assert.Equal(new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetDerivedCompletionDate_ReturnsNull_WhenInsufficientPublishedModulesArePassed()
    {
        var dates = Enumerable.Range(1, 8)
            .Select(day => new DateTimeOffset(2025, 12, day, 9, 0, 0, TimeSpan.Zero))
            .Cast<DateTimeOffset?>()
            .ToList();

        var result = CompletionRuleEvaluator.GetDerivedCompletionDate(10, 8, dates);

        Assert.Null(result);
    }
}
