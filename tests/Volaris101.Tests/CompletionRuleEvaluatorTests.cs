using Volaris101.Application.Services;

namespace Volaris101.Tests;

public class CompletionRuleEvaluatorTests
{
    [Fact]
    public void HasMetCompletionRule_ReturnsTrue_ForGrandfatheredUser()
    {
        var dates = Enumerable.Range(1, 9)
            .Select(day => new DateTimeOffset(2025, 12, day, 9, 0, 0, TimeSpan.Zero))
            .Cast<DateTimeOffset?>()
            .ToList();

        var result = CompletionRuleEvaluator.HasMetCompletionRule(10, 9, dates);

        Assert.True(result);
    }

    [Fact]
    public void HasMetCompletionRule_ReturnsFalse_WhenUserHasNotMetPublishedThreshold()
    {
        var dates = Enumerable.Range(1, 8)
            .Select(day => new DateTimeOffset(2025, 12, day, 9, 0, 0, TimeSpan.Zero))
            .Cast<DateTimeOffset?>()
            .ToList();

        var result = CompletionRuleEvaluator.HasMetCompletionRule(10, 8, dates);

        Assert.False(result);
    }
}
