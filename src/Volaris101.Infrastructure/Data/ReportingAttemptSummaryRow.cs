namespace Volaris101.Infrastructure.Data;

public class ReportingAttemptSummaryRow
{
    public Guid UserId { get; set; }
    public Guid QuizId { get; set; }
    public bool IsTestData { get; set; }
    public bool HasPassed { get; set; }
    public int TotalAttempts { get; set; }
    public decimal? BestScore { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}
