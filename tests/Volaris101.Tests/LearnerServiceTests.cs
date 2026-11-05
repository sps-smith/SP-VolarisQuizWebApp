using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Application.Services;
using Volaris101.Domain.Entities;

namespace Volaris101.Tests;

public class LearnerServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_GrandfatheredUser_UsesPublishedQuizzesOnly()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "Learner");
        var publishedQuizzes = CreatePublishedQuizzes(10);
        var draftQuiz = CreateQuiz(11, isPublished: false);
        var quizzes = publishedQuizzes.Concat(new[] { draftQuiz }).ToList();
        var attempts = CreatePassedAttempts(userId, publishedQuizzes.Take(9).ToList(), new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero))
            .Concat(new[]
            {
                CreatePassedAttempt(userId, draftQuiz, new DateTimeOffset(2025, 12, 20, 9, 0, 0, TimeSpan.Zero))
            })
            .ToList();

        var service = CreateService(
            user,
            quizzes,
            attempts,
            completionRecord: null);

        var dashboard = await service.GetDashboardAsync(userId, "en");

        Assert.True(dashboard.IsFullyCompleted);
        Assert.Equal(10, dashboard.TotalCount);
        Assert.Equal(9, dashboard.PassedCount);
        Assert.Equal(new DateTimeOffset(2025, 12, 9, 9, 0, 0, TimeSpan.Zero), dashboard.CompletedAt);
    }

    [Fact]
    public async Task GetDashboardAsync_AllPublishedModulesComplete_UsesLastPublishedCompletionDate()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "GlobalAdmin", firstName: "Jasmine");
        var publishedQuizzes = CreatePublishedQuizzes(10);
        var draftQuiz = CreateQuiz(11, isPublished: false);
        var quizzes = publishedQuizzes.Concat(new[] { draftQuiz }).ToList();
        var attempts = CreatePassedAttempts(userId, publishedQuizzes, new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero))
            .Concat(new[]
            {
                CreatePassedAttempt(userId, draftQuiz, new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero))
            })
            .ToList();

        var service = CreateService(
            user,
            quizzes,
            attempts,
            completionRecord: null);

        var dashboard = await service.GetDashboardAsync(userId, "en");

        Assert.True(dashboard.IsFullyCompleted);
        Assert.Equal(10, dashboard.TotalCount);
        Assert.Equal(10, dashboard.PassedCount);
        Assert.Equal(new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero), dashboard.CompletedAt);
    }

    [Fact]
    public async Task StartQuizAsync_MarksDraftQuizAsTestData_ForGlobalAdmin()
    {
        var userId = Guid.NewGuid();
        var quiz = CreateQuiz(1, isPublished: false);
        var attemptRepo = new FakeAttemptRepository([]);
        var service = new LearnerService(
            new FakeQuizRepository([quiz]),
            attemptRepo,
            new FakeCompletionRepository(null),
            new FakeUserRepository(CreateUser(userId, "GlobalAdmin")),
            new FakeAuditLogRepository());

        var result = await service.StartQuizAsync(userId, quiz.QuizId);

        Assert.Equal(quiz.QuizId, result.QuizId);
        Assert.Empty(result.Questions);
        Assert.Single(attemptRepo.CreatedAttempts);
        Assert.True(attemptRepo.CreatedAttempts[0].IsTestData);
    }

    [Fact]
    public async Task StartQuizAsync_KeepsAnswerOptionsInCanonicalOrder()
    {
        var userId = Guid.NewGuid();
        var quiz = CreateQuiz(1, isPublished: true);
        var firstOptionId = Guid.NewGuid();
        var secondOptionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        quiz.Questions =
        [
            new Question
            {
                QuestionId = questionId,
                QuizId = quiz.QuizId,
                DisplayOrder = 1,
                Translations = [new QuestionTranslation
                {
                    QuestionId = questionId,
                    LanguageCode = "en",
                    QuestionText = "Which option is first?"
                }],
                Options =
                [
                    new AnswerOption
                    {
                        OptionId = secondOptionId,
                        QuestionId = questionId,
                        DisplayOrder = 2,
                        Translations = [new AnswerOptionTranslation
                        {
                            OptionId = secondOptionId,
                            LanguageCode = "en",
                            OptionText = "Second"
                        }]
                    },
                    new AnswerOption
                    {
                        OptionId = firstOptionId,
                        QuestionId = questionId,
                        DisplayOrder = 1,
                        Translations = [new AnswerOptionTranslation
                        {
                            OptionId = firstOptionId,
                            LanguageCode = "en",
                            OptionText = "First"
                        }]
                    }
                ]
            }
        ];

        var service = CreateService(
            CreateUser(userId, "Learner"),
            [quiz],
            [],
            completionRecord: null);

        var result = await service.StartQuizAsync(userId, quiz.QuizId);

        var question = Assert.Single(result.Questions);
        Assert.Equal([firstOptionId, secondOptionId], question.Options.Select(o => o.OptionId));
        Assert.Equal(['A', 'B'], question.Options.Select(o => o.OptionLetter));
        Assert.Equal([1, 2], question.Options.Select(o => o.DisplayOrder));
    }

    private static LearnerService CreateService(
        User user,
        IReadOnlyList<Quiz> quizzes,
        IReadOnlyList<QuizAttempt> attempts,
        CompletionRecord? completionRecord)
    {
        return new LearnerService(
            new FakeQuizRepository(quizzes),
            new FakeAttemptRepository(attempts),
            new FakeCompletionRepository(completionRecord),
            new FakeUserRepository(user),
            new FakeAuditLogRepository());
    }

    private static User CreateUser(Guid userId, string roleCode, string firstName = "Jasmine")
    {
        return new User
        {
            UserId = userId,
            Login = $"{firstName.ToLowerInvariant()}@example.com",
            IdpSubjectIdentifier = userId.ToString(),
            FirstName = firstName,
            LastName = "Test",
            Email = $"{firstName.ToLowerInvariant()}@example.com",
            EmployeeId = "123",
            Company = "VBU",
            Portfolio = "Portfolio",
            RoleCode = roleCode,
            IsActive = true,
            AccountCreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static List<Quiz> CreatePublishedQuizzes(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateQuiz(i, isPublished: true))
            .ToList();
    }

    private static Quiz CreateQuiz(int displayOrder, bool isPublished)
    {
        return new Quiz
        {
            QuizId = Guid.NewGuid(),
            DisplayOrder = displayOrder,
            IsActive = true,
            IsPublished = isPublished,
            PassingScore = 50m,
            Translations =
            [
                new QuizTranslation
                {
                    QuizId = Guid.NewGuid(),
                    LanguageCode = "en",
                    Title = $"Module {displayOrder}"
                }
            ]
        };
    }

    private static IReadOnlyList<QuizAttempt> CreatePassedAttempts(Guid userId, IReadOnlyList<Quiz> quizzes, DateTimeOffset startDate)
    {
        return quizzes.Select((quiz, index) => CreatePassedAttempt(userId, quiz, startDate.AddDays(index))).ToList();
    }

    private static QuizAttempt CreatePassedAttempt(Guid userId, Quiz quiz, DateTimeOffset completedAt)
    {
        return new QuizAttempt
        {
            AttemptId = Guid.NewGuid(),
            UserId = userId,
            QuizId = quiz.QuizId,
            AttemptNumber = 1,
            Status = "Passed",
            IsTestData = false,
            ScorePercent = 100m,
            TotalCount = 1,
            StartedAt = completedAt.AddMinutes(-15),
            CompletedAt = completedAt
        };
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly User _user;

        public FakeUserRepository(User user) => _user = user;

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<User?>(userId == _user.UserId ? _user : null);

        public Task<User?> GetByLoginAsync(string login, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmployeeIdAsync(string employeeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetAllActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> SearchActiveByPrefixAsync(string? searchTerm, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountActiveAsync(string? searchTerm = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetActiveAsync(string? searchTerm = null, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetFilteredReportingUsersAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, int? take = null, CancellationToken ct = default, Guid? includeUserId = null) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetFilteredReportingUserIdsAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountFilteredReportingUsersAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetDistinctReportingDomainsAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompanyGroupDto>> GetAvailableReportingCompaniesAsync(IReadOnlyCollection<int>? scopeOrganizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User> CreateAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User> UpdateAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeQuizRepository : IQuizRepository
    {
        private readonly IReadOnlyList<Quiz> _quizzes;

        public FakeQuizRepository(IReadOnlyList<Quiz> quizzes) => _quizzes = quizzes;

        public Task<IReadOnlyList<Quiz>> GetAllActiveAsync(string lang = "en", CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Quiz>>(_quizzes);

        public Task<IReadOnlyList<Quiz>> GetAllPublishedAsync(string lang = "en", CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Quiz>>(_quizzes.Where(q => q.IsPublished).ToList());

        public Task<Quiz?> GetByIdAsync(Guid quizId, string lang = "en", CancellationToken ct = default) =>
            Task.FromResult(_quizzes.FirstOrDefault(q => q.QuizId == quizId));
        public Task<Quiz?> GetByIdForLearnerAsync(Guid quizId, bool includeDrafts, string lang = "en", CancellationToken ct = default) =>
            Task.FromResult(_quizzes.FirstOrDefault(q => q.QuizId == quizId && (includeDrafts || q.IsPublished)));
        public Task<Quiz> CreateAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Quiz> UpdateAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PublishAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Question?> GetQuestionByIdAsync(Guid questionId, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Question> CreateQuestionAsync(Question question, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Question> UpdateQuestionAsync(Question question, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteQuestionAsync(Guid questionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Question>> SearchQuestionsAsync(string? searchTerm, Guid? quizId, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeAttemptRepository : IAttemptRepository
    {
        private readonly IReadOnlyList<QuizAttempt> _attempts;
        public List<QuizAttempt> CreatedAttempts { get; } = new();

        public FakeAttemptRepository(IReadOnlyList<QuizAttempt> attempts) => _attempts = attempts;

        public Task<QuizAttempt?> GetAttemptByIdAsync(Guid attemptId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> GetAttemptCountAsync(Guid userId, Guid quizId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<QuizAttempt>> GetUserAttemptsForQuizAsync(Guid userId, Guid quizId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QuizAttempt>> GetAllAttemptsForUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<QuizAttempt>>(_attempts.Where(a => a.UserId == userId).ToList());
        public Task<QuizAttempt> CreateAttemptAsync(QuizAttempt attempt, CancellationToken ct = default)
        {
            CreatedAttempts.Add(attempt);
            return Task.FromResult(attempt);
        }
        public Task<QuizAttempt> UpdateAttemptAsync(QuizAttempt attempt, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QuizAttempt>> GetAttemptsForReportingAsync(bool includeTestData = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<(Guid UserId, Guid QuizId, bool HasPassed, int TotalAttempts, decimal? BestScore, DateTimeOffset? LastAttemptAt)>> GetPassStatusSummaryAsync(bool includeTestData = false, IReadOnlyCollection<Guid>? userIds = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeCompletionRepository : ICompletionRepository
    {
        private readonly CompletionRecord? _completionRecord;

        public FakeCompletionRepository(CompletionRecord? completionRecord) => _completionRecord = completionRecord;

        public Task<CompletionRecord?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(_completionRecord);
        public Task<CompletionRecord> CreateAsync(CompletionRecord record, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CompletionRecord> UpdateAsync(CompletionRecord record, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompletionRecord>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CompletionRecord>>([]);
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public Task WriteAsync(string actionType, string outcome, Guid? actorUserId, string actorType, Guid? targetUserId, string? beforeValue, string? afterValue, string? sourceComponent, string? denialReason = null, string? ipAddress = null, string? additionalDataJson = null, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
