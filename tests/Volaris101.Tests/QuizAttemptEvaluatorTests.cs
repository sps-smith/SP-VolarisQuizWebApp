using Volaris101.Application.Services;
using Volaris101.Domain.Entities;

namespace Volaris101.Tests;

public class QuizAttemptEvaluatorTests
{
    [Fact]
    public void IsPassedAttempt_ReturnsTrue_WhenStatusIsPassed()
    {
        var attempt = new QuizAttempt { Status = "Passed" };

        var result = QuizAttemptEvaluator.IsPassedAttempt(attempt, null, 50m);

        Assert.True(result);
    }

    [Fact]
    public void IsFailedAttempt_ReturnsTrue_WhenStatusIsFailed()
    {
        var attempt = new QuizAttempt { Status = "Failed" };

        var result = QuizAttemptEvaluator.IsFailedAttempt(attempt, null, 50m);

        Assert.True(result);
    }

    [Fact]
    public void TryInferScoreFromAnswers_ReturnsCorrectScore()
    {
        var questionId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();
        var wrongOptionId = Guid.NewGuid();
        var quiz = new Quiz
        {
            QuizId = Guid.NewGuid(),
            DisplayOrder = 1,
            IsActive = true,
            IsPublished = true,
            Questions =
            [
                new Question
                {
                    QuestionId = questionId,
                    QuizId = Guid.NewGuid(),
                    DisplayOrder = 1,
                    IsActive = true,
                    Options =
                    [
                        new AnswerOption { OptionId = correctOptionId, QuestionId = questionId, IsCorrect = true },
                        new AnswerOption { OptionId = wrongOptionId, QuestionId = questionId, IsCorrect = false }
                    ]
                }
            ]
        };

        var attempt = new QuizAttempt
        {
            AttemptId = Guid.NewGuid(),
            QuizId = quiz.QuizId,
            UserId = Guid.NewGuid(),
            Status = "Passed",
            Answers =
            [
                new AttemptAnswer
                {
                    AttemptAnswerId = Guid.NewGuid(),
                    AttemptId = Guid.NewGuid(),
                    QuestionId = questionId,
                    OptionId = correctOptionId,
                    SelectedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        var result = QuizAttemptEvaluator.TryInferScoreFromAnswers(attempt, quiz);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Correct);
        Assert.Equal(1, result.Total);
        Assert.Equal(100m, result.ScorePercent);
    }
}
