namespace Volaris101.Application.Services;

public static class CompletionRuleEvaluator
{
    public const int GrandfatherRequiredPassedModules = 9;
    public static readonly DateTimeOffset GrandfatherCutoffExclusiveUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static bool HasMetCompletionRule(
        int totalPublishedModuleCount,
        int passedModuleCount,
        IEnumerable<DateTimeOffset?> passedModuleCompletionDates)
    {
        if (totalPublishedModuleCount <= 0)
            return false;

        if (passedModuleCount >= totalPublishedModuleCount)
            return true;

        return GetGrandfatherCompletionDate(totalPublishedModuleCount, passedModuleCompletionDates).HasValue;
    }

    public static DateTimeOffset? GetDerivedCompletionDate(
        int totalPublishedModuleCount,
        int passedModuleCount,
        IEnumerable<DateTimeOffset?> passedModuleCompletionDates)
    {
        if (totalPublishedModuleCount <= 0)
            return null;

        var normalizedDates = passedModuleCompletionDates
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .OrderBy(date => date)
            .ToList();

        var grandfatherCompletionDate = GetGrandfatherCompletionDate(totalPublishedModuleCount, normalizedDates);
        if (grandfatherCompletionDate.HasValue)
            return grandfatherCompletionDate;

        if (passedModuleCount >= totalPublishedModuleCount && normalizedDates.Count >= totalPublishedModuleCount)
            return normalizedDates[totalPublishedModuleCount - 1];

        return null;
    }

    private static DateTimeOffset? GetGrandfatherCompletionDate(
        int totalPublishedModuleCount,
        IEnumerable<DateTimeOffset?> passedModuleCompletionDates)
    {
        if (totalPublishedModuleCount < GrandfatherRequiredPassedModules)
            return null;

        var grandfatherDates = passedModuleCompletionDates
            .Where(date => date.HasValue && date.Value < GrandfatherCutoffExclusiveUtc)
            .Select(date => date!.Value)
            .OrderBy(date => date)
            .ToList();

        return grandfatherDates.Count >= GrandfatherRequiredPassedModules
            ? grandfatherDates[GrandfatherRequiredPassedModules - 1]
            : null;
    }

    private static DateTimeOffset? GetGrandfatherCompletionDate(
        int totalPublishedModuleCount,
        IEnumerable<DateTimeOffset> passedModuleCompletionDates)
    {
        if (totalPublishedModuleCount < GrandfatherRequiredPassedModules)
            return null;

        var grandfatherDates = passedModuleCompletionDates
            .Where(date => date < GrandfatherCutoffExclusiveUtc)
            .OrderBy(date => date)
            .ToList();

        return grandfatherDates.Count >= GrandfatherRequiredPassedModules
            ? grandfatherDates[GrandfatherRequiredPassedModules - 1]
            : null;
    }
}
