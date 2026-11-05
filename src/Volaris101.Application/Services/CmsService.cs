using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;

namespace Volaris101.Application.Services;

public class CmsService : ICmsService
{
    private static readonly (string Code, string Label)[] SupportedLanguages =
    {
        ("en", "EN"),
        ("de", "DE"),
        ("es", "ES"),
        ("fr", "FR"),
        ("pt", "PT")
    };

    private readonly IQuizRepository _quizRepo;
    private readonly IAuditLogRepository _auditLog;

    public CmsService(IQuizRepository quizRepo, IAuditLogRepository auditLog)
    {
        _quizRepo = quizRepo;
        _auditLog = auditLog;
    }

    public async Task<IReadOnlyList<CmsQuizSummaryDto>> GetQuizListAsync(string lang = "en", CancellationToken ct = default)
    {
        lang = NormalizeLanguage(lang);
        var quizzes = await _quizRepo.GetAllActiveAsync(lang, ct);
        return quizzes.Select(q => new CmsQuizSummaryDto(
            q.QuizId,
            q.DisplayOrder,
            q.GetTitle(lang),
            q.Questions.Count(qu => qu.IsActive),
            q.IsActive,
            q.IsPublished
        )).ToList();
    }

    public async Task<CmsQuizDetailDto> GetQuizDetailAsync(Guid quizId, string lang = "en", CancellationToken ct = default)
    {
        lang = NormalizeLanguage(lang);
        var quiz = await _quizRepo.GetByIdAsync(quizId, lang, ct)
            ?? throw new KeyNotFoundException($"Quiz {quizId} not found.");

        return MapQuiz(quiz, lang);
    }

    public async Task<CmsQuestionDto> GetQuestionAsync(Guid questionId, string lang = "en", CancellationToken ct = default)
    {
        lang = NormalizeLanguage(lang);
        var question = await _quizRepo.GetQuestionByIdAsync(questionId, lang, ct)
            ?? throw new KeyNotFoundException($"Question {questionId} not found.");

        return MapQuestion(question, lang);
    }

    public async Task<IReadOnlyList<CmsQuestionDto>> GetAllQuestionsAsync(string? searchTerm, Guid? quizId, string lang = "en", CancellationToken ct = default)
    {
        lang = NormalizeLanguage(lang);
        var questions = await _quizRepo.SearchQuestionsAsync(searchTerm, quizId, lang, ct);
        var quizzes = await _quizRepo.GetAllActiveAsync(lang, ct);
        var quizzesById = quizzes.ToDictionary(q => q.QuizId);

        return questions.Select(q =>
        {
            quizzesById.TryGetValue(q.QuizId, out var quiz);
            return MapQuestion(q, lang, quiz);
        }).ToList();
    }

    public async Task<CmsQuestionDto> AddQuestionAsync(Guid quizId, CmsQuestionDto dto, CancellationToken ct = default)
    {
        var quiz = await _quizRepo.GetByIdAsync(quizId, "en", ct)
            ?? throw new KeyNotFoundException($"Quiz {quizId} not found.");

        int nextOrder = quiz.Questions.Any() ? quiz.Questions.Max(q => q.DisplayOrder) + 1 : 1;
        var questionId = Guid.NewGuid();

        var question = new Question
        {
            QuestionId = questionId,
            QuizId = quizId,
            DisplayOrder = nextOrder,
            IsMultiSelect = dto.IsMultiSelect,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Translations = new List<QuestionTranslation>
            {
                new() { QuestionId = Guid.NewGuid(), LanguageCode = "en", QuestionText = dto.QuestionText, IsComplete = true }
            },
            Options = dto.Options.Select((o, i) =>
            {
                var displayOrder = o.DisplayOrder > 0 ? o.DisplayOrder : i + 1;
                return new AnswerOption
                {
                    OptionId = Guid.NewGuid(),
                    QuestionId = questionId,
                    OptionKey = OptionKeyForOrder(displayOrder),
                    DisplayOrder = displayOrder,
                    IsCorrect = o.IsCorrect,
                    Translations = new List<AnswerOptionTranslation>
                    {
                        new() { LanguageCode = "en", OptionText = o.OptionText }
                    }
                };
            }).ToList()
        };

        // Fix up translation OptionId references
        foreach (var opt in question.Options)
            foreach (var t in opt.Translations)
                t.OptionId = opt.OptionId;

        foreach (var t in question.Translations)
            t.QuestionId = question.QuestionId;

        await _quizRepo.CreateQuestionAsync(question, ct);
        await _auditLog.WriteAsync("cms.question_added", "success", null, "system", null,
            null, question.QuestionId.ToString(), "cms", ct: ct);

        return MapQuestion(question, "en");
    }

    public async Task<CmsQuestionDto> UpdateQuestionAsync(Guid questionId, CmsQuestionDto dto, CancellationToken ct = default)
    {
        var language = NormalizeLanguage(dto.LanguageCode);
        var question = await _quizRepo.GetQuestionByIdAsync(questionId, "en", ct)
            ?? throw new KeyNotFoundException($"Question {questionId} not found.");

        // Update English translation
        var questionTranslation = question.Translations.FirstOrDefault(t =>
            t.LanguageCode.Equals(language, StringComparison.OrdinalIgnoreCase));
        if (questionTranslation == null)
        {
            questionTranslation = new QuestionTranslation
            {
                QuestionId = questionId,
                LanguageCode = language
            };
            question.Translations.Add(questionTranslation);
        }
        questionTranslation.QuestionText = dto.QuestionText;
        questionTranslation.IsComplete = true;

        question.IsMultiSelect = dto.IsMultiSelect;
        question.UpdatedAt = DateTimeOffset.UtcNow;

        // Replace options (simple approach — clear and re-add)
        var optionIds = dto.Options
            .Where(o => o.OptionId != Guid.Empty)
            .Select(o => o.OptionId)
            .ToHashSet();
        foreach (var option in question.Options.Where(o => !optionIds.Contains(o.OptionId)).ToList())
            question.Options.Remove(option);

        foreach (var (o, i) in dto.Options.Select((option, index) => (option, index)))
        {
            var displayOrder = o.DisplayOrder > 0 ? o.DisplayOrder : i + 1;
            var option = question.Options.FirstOrDefault(existing => existing.OptionId == o.OptionId);
            if (option == null)
            {
                option = new AnswerOption
                {
                    OptionId = o.OptionId != Guid.Empty ? o.OptionId : Guid.NewGuid(),
                    QuestionId = questionId,
                    Translations = new List<AnswerOptionTranslation>()
                };
                question.Options.Add(option);
            }

            option.OptionKey = OptionKeyForOrder(displayOrder);
            option.DisplayOrder = displayOrder;
            option.IsCorrect = o.IsCorrect;
            var optionTranslation = option.Translations.FirstOrDefault(t =>
                t.LanguageCode.Equals(language, StringComparison.OrdinalIgnoreCase));
            if (optionTranslation == null)
            {
                optionTranslation = new AnswerOptionTranslation
                {
                    OptionId = option.OptionId,
                    LanguageCode = language
                };
                option.Translations.Add(optionTranslation);
            }
            optionTranslation.OptionText = o.OptionText;
        }

        await _quizRepo.UpdateQuestionAsync(question, ct);
        await _auditLog.WriteAsync("cms.question_edited", "success", null, "system", null,
            questionId.ToString(), questionId.ToString(), "cms", ct: ct);

        return MapQuestion(question, language);
    }

    public async Task DeleteQuestionAsync(Guid questionId, CancellationToken ct = default)
    {
        await _quizRepo.DeleteQuestionAsync(questionId, ct);
        await _auditLog.WriteAsync("cms.question_deleted", "success", null, "system", null,
            questionId.ToString(), null, "cms", ct: ct);
    }

    public async Task<CmsQuizSummaryDto> AddQuizAsync(string title, CancellationToken ct = default)
    {
        var existing = await _quizRepo.GetAllActiveAsync("en", ct);
        int nextOrder = existing.Any() ? existing.Max(q => q.DisplayOrder) + 1 : 1;

        var quiz = new Quiz
        {
            QuizId = Guid.NewGuid(),
            DisplayOrder = nextOrder,
            PassingScore = 50m,
            IsActive = true,
            IsPublished = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Translations = new List<QuizTranslation>
            {
                new() { LanguageCode = "en", Title = title.Trim(), IsComplete = true }
            },
            Questions = new()
        };

        quiz.Translations[0].QuizId = quiz.QuizId;
        await _quizRepo.CreateAsync(quiz, ct);
        await _auditLog.WriteAsync("cms.quiz_added", "success", null, "system", null,
            null, title, "cms", ct: ct);

        return new CmsQuizSummaryDto(quiz.QuizId, quiz.DisplayOrder, title, 0, true, false);
    }

    public async Task<CmsQuizSummaryDto> UpdateQuizAsync(Guid quizId, string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.");

        var quiz = await _quizRepo.GetByIdAsync(quizId, "en", ct)
            ?? throw new KeyNotFoundException($"Quiz {quizId} not found.");

        var before = quiz.GetTitle("en");
        var enTrans = quiz.Translations.FirstOrDefault(t => t.LanguageCode == "en");
        if (enTrans == null)
        {
            quiz.Translations.Add(new QuizTranslation
            {
                QuizId = quiz.QuizId,
                LanguageCode = "en",
                Title = title.Trim(),
                IsComplete = true
            });
        }
        else
        {
            enTrans.Title = title.Trim();
        }

        await _quizRepo.UpdateAsync(quiz, ct);
        await _auditLog.WriteAsync("cms.quiz_renamed", "success", null, "system", null,
            before, title.Trim(), "cms", ct: ct);

        return new CmsQuizSummaryDto(quiz.QuizId, quiz.DisplayOrder, title.Trim(), quiz.Questions.Count(q => q.IsActive), quiz.IsActive, quiz.IsPublished);
    }

    public async Task<CmsQuizSummaryDto> PublishQuizAsync(Guid quizId, Guid actorUserId, CancellationToken ct = default)
    {
        var quiz = await _quizRepo.GetByIdAsync(quizId, "en", ct)
            ?? throw new KeyNotFoundException($"Quiz {quizId} not found.");

        if (quiz.IsPublished)
            return new CmsQuizSummaryDto(quiz.QuizId, quiz.DisplayOrder, quiz.GetTitle("en"), quiz.Questions.Count(q => q.IsActive), quiz.IsActive, quiz.IsPublished);

        ValidatePublishable(quiz);

        var deletedTestAttempts = await _quizRepo.PublishAsync(quiz, ct);
        await _auditLog.WriteAsync("cms.quiz_published", "success", actorUserId, "user", null,
            "Draft", "Published", "cms", additionalDataJson: $"{{\"quizId\":\"{quizId}\",\"deletedTestAttempts\":{deletedTestAttempts}}}", ct: ct);

        return new CmsQuizSummaryDto(quiz.QuizId, quiz.DisplayOrder, quiz.GetTitle("en"), quiz.Questions.Count(q => q.IsActive), quiz.IsActive, quiz.IsPublished);
    }

    private static CmsQuizDetailDto MapQuiz(Quiz q, string lang) => new(
        q.QuizId, q.DisplayOrder, q.GetTitle(lang), q.IsActive, q.IsPublished,
        lang,
        q.Questions.Where(qu => qu.IsActive).OrderBy(qu => qu.DisplayOrder).Select(qu => MapQuestion(qu, lang)).ToList(),
        MapLanguageStatuses(q, lang)
    );

    private static void ValidatePublishable(Quiz quiz)
    {
        var activeQuestions = quiz.Questions.Where(q => q.IsActive).ToList();
        if (activeQuestions.Count == 0)
            throw new InvalidOperationException("Add at least one question before publishing this quiz.");

        foreach (var question in activeQuestions)
        {
            var activeOptions = question.Options.OrderBy(o => o.DisplayOrder).ToList();
            if (activeOptions.Count < 2)
                throw new InvalidOperationException("Each question must have at least two answer options before publishing.");

            if (!activeOptions.Any(o => o.IsCorrect))
                throw new InvalidOperationException("Each question must have at least one correct answer before publishing.");
        }
    }

    private static CmsQuestionDto MapQuestion(Question q, string lang, Quiz? quiz = null) => new(
        q.QuestionId,
        q.DisplayOrder,
        q.GetText(lang),
        q.IsMultiSelect,
        q.IsActive,
        q.Options.OrderBy(o => o.DisplayOrder).Select((o, i) => new CmsOptionDto(
            o.OptionId,
            o.DisplayOrder > 0 ? o.DisplayOrder : i + 1,
            o.GetText(lang),
            o.IsCorrect
        )).ToList(),
        q.QuizId,
        quiz?.DisplayOrder,
        quiz?.GetTitle(lang),
        lang
    );

    private static string OptionKeyForOrder(int displayOrder)
    {
        var normalizedOrder = Math.Clamp(displayOrder, 1, 26);
        return ((char)('A' + normalizedOrder - 1)).ToString();
    }

    private static IReadOnlyList<CmsLanguageStatusDto> MapLanguageStatuses(Quiz quiz, string selectedLanguage)
    {
        var activeQuestions = quiz.Questions.Where(q => q.IsActive).ToList();
        return SupportedLanguages.Select(lang =>
        {
            var translatedCount = activeQuestions.Count(q =>
                q.Translations.Any(t =>
                    t.LanguageCode.Equals(lang.Code, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(t.QuestionText)));

            return new CmsLanguageStatusDto(
                lang.Code,
                lang.Label,
                translatedCount,
                lang.Code == "en",
                lang.Code == selectedLanguage);
        }).ToList();
    }

    private static string NormalizeLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
            return "en";

        var normalized = lang.Trim().ToLowerInvariant();
        return SupportedLanguages.Any(l => l.Code == normalized) ? normalized : "en";
    }
}
