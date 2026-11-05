using Volaris101.Domain.Entities;

namespace Volaris101.Application.Services;

internal static class QuizAttemptEvaluator
{
    public static bool IsInProgress(QuizAttempt attempt) =>
        string.Equals(attempt.Status, "InProgress", StringComparison.OrdinalIgnoreCase);

    public static bool IsPassedAttempt(QuizAttempt attempt, AttemptScoreSnapshot? inferredScore, decimal passingScore)
    {
        if (HasStatus(attempt, "Passed"))
            return true;

        var score = attempt.ScorePercent ?? inferredScore?.ScorePercent;
        return score.HasValue &&
               score.Value >= passingScore &&
               HasCompletionSignal(attempt, inferredScore);
    }

    public static bool IsFailedAttempt(QuizAttempt attempt, AttemptScoreSnapshot? inferredScore, decimal passingScore)
    {
        if (HasStatus(attempt, "Failed"))
            return true;

        var score = attempt.ScorePercent ?? inferredScore?.ScorePercent;
        return score.HasValue &&
               score.Value < passingScore &&
               HasCompletionSignal(attempt, inferredScore);
    }

    public static bool HasCompletionSignal(QuizAttempt attempt, AttemptScoreSnapshot? inferredScore) =>
        attempt.CompletedAt.HasValue ||
        attempt.TotalCount.HasValue ||
        attempt.ScorePercent.HasValue ||
        inferredScore != null ||
        HasStatus(attempt, "Passed") ||
        HasStatus(attempt, "Failed");

    public static AttemptScoreSnapshot? TryInferScoreFromAnswers(QuizAttempt attempt, Quiz quiz)
    {
        if (attempt.Answers.Count == 0)
            return null;

        var questions = quiz.Questions.Where(q => q.IsActive).ToList();
        if (questions.Count == 0)
            return null;

        var answersByQuestionId = attempt.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.OptionId).ToHashSet());

        var correct = 0;
        foreach (var question in questions)
        {
            var correctOptionIds = question.Options
                .Where(o => o.IsCorrect)
                .Select(o => o.OptionId)
                .ToHashSet();

            answersByQuestionId.TryGetValue(question.QuestionId, out var selectedOptionIds);
            selectedOptionIds ??= new HashSet<Guid>();

            if (correctOptionIds.Count > 0 && correctOptionIds.SetEquals(selectedOptionIds))
                correct++;
        }

        var scorePercent = (decimal)correct / questions.Count * 100;
        return new AttemptScoreSnapshot(correct, questions.Count, Math.Round(scorePercent, 2));
    }

    private static bool HasStatus(QuizAttempt attempt, string status) =>
        string.Equals(attempt.Status, status, StringComparison.OrdinalIgnoreCase);
}

internal sealed record AttemptScoreSnapshot(int Correct, int Total, decimal ScorePercent);
