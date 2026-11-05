using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Application.Services;
using Volaris101.Domain.Entities;

namespace Volaris101.Tests;

public class CmsServiceTests
{
    [Fact]
    public async Task UpdateQuestionAsync_UpdatesOnlySelectedLanguageTranslations()
    {
        var questionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var question = new Question
        {
            QuestionId = questionId,
            QuizId = Guid.NewGuid(),
            DisplayOrder = 1,
            IsActive = true,
            Translations =
            [
                new QuestionTranslation { QuestionId = questionId, LanguageCode = "en", QuestionText = "English question" },
                new QuestionTranslation { QuestionId = questionId, LanguageCode = "fr", QuestionText = "Question française" }
            ],
            Options =
            [
                new AnswerOption
                {
                    OptionId = optionId,
                    QuestionId = questionId,
                    DisplayOrder = 1,
                    OptionKey = "A",
                    IsCorrect = true,
                    Translations =
                    [
                        new AnswerOptionTranslation { OptionId = optionId, LanguageCode = "en", OptionText = "English answer" },
                        new AnswerOptionTranslation { OptionId = optionId, LanguageCode = "fr", OptionText = "Réponse française" }
                    ]
                }
            ]
        };
        var repository = new RecordingQuizRepository(question);
        var service = new CmsService(repository, new NoOpAuditLogRepository());

        await service.UpdateQuestionAsync(
            questionId,
            new CmsQuestionDto(
                questionId,
                1,
                "Question française modifiée",
                false,
                true,
                [new CmsOptionDto(optionId, 1, "Réponse française modifiée", true)],
                LanguageCode: "fr"));

        Assert.Equal("English question", question.Translations.Single(t => t.LanguageCode == "en").QuestionText);
        Assert.Equal("Question française modifiée", question.Translations.Single(t => t.LanguageCode == "fr").QuestionText);
        Assert.Equal("English answer", question.Options[0].Translations.Single(t => t.LanguageCode == "en").OptionText);
        Assert.Equal("Réponse française modifiée", question.Options[0].Translations.Single(t => t.LanguageCode == "fr").OptionText);
    }

    [Fact]
    public async Task UpdateQuestionAsync_CreatesMissingSelectedLanguageTranslations()
    {
        var questionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var question = new Question
        {
            QuestionId = questionId,
            QuizId = Guid.NewGuid(),
            Translations = [new QuestionTranslation { QuestionId = questionId, LanguageCode = "en", QuestionText = "English question" }],
            Options =
            [
                new AnswerOption
                {
                    OptionId = optionId,
                    QuestionId = questionId,
                    Translations = [new AnswerOptionTranslation { OptionId = optionId, LanguageCode = "en", OptionText = "English answer" }]
                }
            ]
        };
        var service = new CmsService(new RecordingQuizRepository(question), new NoOpAuditLogRepository());

        await service.UpdateQuestionAsync(
            questionId,
            new CmsQuestionDto(
                questionId,
                1,
                "Deutsche Frage",
                false,
                true,
                [new CmsOptionDto(optionId, 1, "Deutsche Antwort", true)],
                LanguageCode: "de"));

        Assert.Equal("Deutsche Frage", question.Translations.Single(t => t.LanguageCode == "de").QuestionText);
        Assert.Equal("Deutsche Antwort", question.Options[0].Translations.Single(t => t.LanguageCode == "de").OptionText);
        Assert.Equal("English question", question.Translations.Single(t => t.LanguageCode == "en").QuestionText);
    }

    private sealed class RecordingQuizRepository : IQuizRepository
    {
        private readonly Question _question;
        public RecordingQuizRepository(Question question) => _question = question;

        public Task<IReadOnlyList<Quiz>> GetAllActiveAsync(string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Quiz>> GetAllPublishedAsync(string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Quiz?> GetByIdAsync(Guid quizId, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Quiz?> GetByIdForLearnerAsync(Guid quizId, bool includeDrafts, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Quiz> CreateAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Quiz> UpdateAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PublishAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Question?> GetQuestionByIdAsync(Guid questionId, string lang = "en", CancellationToken ct = default) => Task.FromResult<Question?>(_question);
        public Task<Question> CreateQuestionAsync(Question question, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Question> UpdateQuestionAsync(Question question, CancellationToken ct = default) => Task.FromResult(question);
        public Task DeleteQuestionAsync(Guid questionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Question>> SearchQuestionsAsync(string? searchTerm, Guid? quizId, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoOpAuditLogRepository : IAuditLogRepository
    {
        public Task WriteAsync(string actionType, string outcome, Guid? actorUserId, string actorType,
            Guid? targetUserId, string? beforeValue, string? afterValue, string? sourceComponent,
            string? denialReason = null, string? ipAddress = null, string? additionalDataJson = null,
            CancellationToken ct = default) => Task.CompletedTask;
    }
}
