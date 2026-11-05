using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;

namespace Volaris101.Application.Services;

public class LearnerService : ILearnerService
{
    private readonly IQuizRepository _quizRepo;
    private readonly IAttemptRepository _attemptRepo;
    private readonly ICompletionRepository _completionRepo;
    private readonly IUserRepository _userRepo;
    private readonly IAuditLogRepository _auditLog;

    public LearnerService(
        IQuizRepository quizRepo,
        IAttemptRepository attemptRepo,
        ICompletionRepository completionRepo,
        IUserRepository userRepo,
        IAuditLogRepository auditLog)
    {
        _quizRepo = quizRepo;
        _attemptRepo = attemptRepo;
        _completionRepo = completionRepo;
        _userRepo = userRepo;
        _auditLog = auditLog;
    }

    public async Task<LearnerDashboardDto> GetDashboardAsync(Guid userId, string lang = "en", CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var includeDrafts = user.RoleCode == "GlobalAdmin";
        var quizzes = includeDrafts
            ? await _quizRepo.GetAllActiveAsync(lang, ct)
            : await _quizRepo.GetAllPublishedAsync(lang, ct);
        var publishedQuizzes = quizzes.Where(q => q.IsPublished).ToList();
        var attempts = await _attemptRepo.GetAllAttemptsForUserAsync(userId, ct);
        if (!includeDrafts)
            attempts = attempts.Where(a => !a.IsTestData).ToList();

        var attemptsByQuiz = attempts
            .GroupBy(a => a.QuizId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var modules = quizzes.OrderBy(q => q.DisplayOrder).Select(quiz =>
        {
            var quizAttempts = attemptsByQuiz.GetValueOrDefault(quiz.QuizId, new());
            var scoredAttempts = quizAttempts
                .Select(a => new
                {
                    Attempt = a,
                    InferredScore = QuizAttemptEvaluator.TryInferScoreFromAnswers(a, quiz)
                })
                .ToList();
            var hasPassed = scoredAttempts.Any(a =>
                QuizAttemptEvaluator.IsPassedAttempt(a.Attempt, a.InferredScore, quiz.PassingScore));
            var hasFailed = !hasPassed && scoredAttempts.Any(a =>
                QuizAttemptEvaluator.IsFailedAttempt(a.Attempt, a.InferredScore, quiz.PassingScore));
            var hasInProgress = !hasPassed && !hasFailed && quizAttempts.Any(QuizAttemptEvaluator.IsInProgress);
            var scores = scoredAttempts
                .Select(a => a.Attempt.ScorePercent ?? a.InferredScore?.ScorePercent)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToList();
            var bestScore = scores.Count > 0 ? scores.Max() : (decimal?)null;
            var attemptCount = scoredAttempts.Count(a =>
                !QuizAttemptEvaluator.IsInProgress(a.Attempt) ||
                QuizAttemptEvaluator.HasCompletionSignal(a.Attempt, a.InferredScore));

            var status = hasPassed ? "pass" : hasFailed ? "fail" : hasInProgress ? "ip" : "ns";
            var statusLabel = hasPassed ? "Passed" : hasFailed ? "Failed" : hasInProgress ? "In progress" : "Not started";
            var ctaLabel = status == "ns" ? "Start quiz" : status == "ip" ? "Restart quiz" : "Retake";

            return new ModuleCardDto(
                quiz.QuizId,
                quiz.DisplayOrder,
                quiz.GetTitle(lang),
                quiz.IsPublished,
                status,
                statusLabel,
                bestScore.HasValue ? (int)Math.Round(bestScore.Value) : null,
                attemptCount > 0 ? attemptCount : null,
                ctaLabel
            );
        }).ToList();

        var passedCount = modules.Count(m => m.Status == "pass" && m.IsPublished);
        var totalCount = publishedQuizzes.Count;
        var progressPercent = totalCount > 0 ? (int)Math.Round((double)passedCount / totalCount * 100) : 0;
        var dashboardQuizById = publishedQuizzes.ToDictionary(q => q.QuizId);
        var publishedAttemptsByQuiz = attemptsByQuiz
            .Where(kvp => dashboardQuizById.ContainsKey(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var passedModuleCompletionDates = publishedAttemptsByQuiz
            .Where(kvp => kvp.Value.Count > 0)
            .Select(kvp =>
            {
                if (!dashboardQuizById.TryGetValue(kvp.Key, out var quiz))
                    return (DateTimeOffset?)null;

                var passedAttempts = kvp.Value
                    .Select(a => new
                    {
                        Attempt = a,
                        InferredScore = QuizAttemptEvaluator.TryInferScoreFromAnswers(a, quiz)
                    })
                    .Where(a => QuizAttemptEvaluator.IsPassedAttempt(a.Attempt, a.InferredScore, quiz.PassingScore))
                    .Select(a => a.Attempt.CompletedAt ?? a.Attempt.StartedAt)
                    .OrderBy(date => date)
                    .ToList();

                return passedAttempts.Count > 0 ? passedAttempts[0] : null;
            })
            .ToList();
        var isFullyCompleted = CompletionRuleEvaluator.HasMetCompletionRule(totalCount, passedCount, passedModuleCompletionDates);
        DateTimeOffset? completedAt = isFullyCompleted
            ? passedModuleCompletionDates
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .DefaultIfEmpty()
                .Max()
            : null;

        return new LearnerDashboardDto(
            user.FirstName,
            user.Initials,
            passedCount,
            totalCount,
            progressPercent,
            isFullyCompleted,
            completedAt,
            modules
        );
    }

    public async Task<StartQuizResponseDto> StartQuizAsync(Guid userId, Guid quizId, string lang = "en", CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var includeDrafts = user.RoleCode == "GlobalAdmin";
        var quiz = await _quizRepo.GetByIdForLearnerAsync(quizId, includeDrafts, lang, ct)
            ?? throw new KeyNotFoundException($"Quiz {quizId} not found.");

        var attemptCount = await _attemptRepo.GetAttemptCountAsync(userId, quizId, ct);

        var attempt = new QuizAttempt
        {
            AttemptId = Guid.NewGuid(),
            UserId = userId,
            QuizId = quizId,
            AttemptNumber = attemptCount + 1,
            Status = "InProgress",
            IsTestData = !quiz.IsPublished,
            StartedAt = DateTimeOffset.UtcNow
        };

        await _attemptRepo.CreateAttemptAsync(attempt, ct);

        // Shuffle questions and options using Fisher-Yates for true randomisation
        var rng = Random.Shared;
        var questions = quiz.Questions.Where(q => q.IsActive).ToList();
        for (int i = questions.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (questions[i], questions[j]) = (questions[j], questions[i]);
        }

        var shuffledQuestions = questions.Select((q, i) =>
        {
            // Keep answer options in their canonical CMS order for every learner.
            // The question order is randomized above, but answer positions must
            // remain consistent so that users see the same A/B/C mapping.
            var options = q.Options
                .OrderBy(opt => opt.DisplayOrder)
                .ThenBy(opt => opt.OptionId)
                .ToList();

            var shuffledOptions = options.Select((opt, oi) => new QuizOptionDto(
                opt.OptionId,
                opt.GetText(lang),
                (char)('A' + oi),
                opt.DisplayOrder > 0 ? opt.DisplayOrder : oi + 1
            )).ToList();

            return new QuizQuestionDto(
                q.QuestionId,
                i + 1,
                q.GetText(lang),
                q.IsMultiSelect,
                shuffledOptions
            );
        }).ToList();

        return new StartQuizResponseDto(
            attempt.AttemptId,
            quiz.QuizId,
            quiz.DisplayOrder,
            quiz.GetTitle(lang),
            shuffledQuestions
        );
    }

    public async Task<QuizResultDto> SubmitQuizAsync(Guid userId, SubmitQuizRequestDto request, string lang = "en", CancellationToken ct = default)
    {
        var attempt = await _attemptRepo.GetAttemptByIdAsync(request.AttemptId, ct)
            ?? throw new KeyNotFoundException($"Attempt {request.AttemptId} not found.");

        if (attempt.UserId != userId)
            throw new UnauthorizedAccessException("Attempt does not belong to this user.");

        if (attempt.Status != "InProgress")
            throw new InvalidOperationException("Attempt has already been submitted.");

        var quiz = await _quizRepo.GetByIdAsync(attempt.QuizId, lang, ct)
            ?? throw new KeyNotFoundException($"Quiz {attempt.QuizId} not found.");

        // Score the attempt
        var submittedAnswers = request.Answers ?? Array.Empty<QuestionAnswerDto>();
        var activeQuestions = quiz.Questions.Where(q => q.IsActive).ToList();
        var questionsById = activeQuestions.ToDictionary(q => q.QuestionId);
        var answersByQuestionId = submittedAnswers
            .Where(a => questionsById.ContainsKey(a.QuestionId))
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Last());

        var orderedQuestions = submittedAnswers
            .Select(a => a.QuestionId)
            .Where(id => questionsById.ContainsKey(id))
            .Distinct()
            .Select(id => questionsById[id])
            .ToList();

        orderedQuestions.AddRange(
            activeQuestions
                .Where(q => !answersByQuestionId.ContainsKey(q.QuestionId))
                .OrderBy(q => q.DisplayOrder));

        var reviewItems = new List<ResultReviewItemDto>();
        int correct = 0;
        int total = activeQuestions.Count;
        int position = 1;

        foreach (var question in orderedQuestions)
        {
            answersByQuestionId.TryGetValue(question.QuestionId, out var answer);
            var options = question.Options.OrderBy(o => o.DisplayOrder).ToList();
            var validOptionIds = options.Select(o => o.OptionId).ToHashSet();
            var validDisplayOrders = options.Select(o => o.DisplayOrder).ToHashSet();

            var selectedIds = (answer?.SelectedOptionIds ?? Array.Empty<Guid>())
                .Where(id => id != Guid.Empty && validOptionIds.Contains(id))
                .ToHashSet();

            var selectedDisplayOrders = (answer?.SelectedOptionDisplayOrders ?? Array.Empty<int>())
                .Where(order => validDisplayOrders.Contains(order))
                .ToHashSet();

            if (selectedDisplayOrders.Count == 0 && selectedIds.Count > 0)
            {
                selectedDisplayOrders = options
                    .Where(o => selectedIds.Contains(o.OptionId))
                    .Select(o => o.DisplayOrder)
                    .ToHashSet();
            }

            if (selectedIds.Count == 0 && selectedDisplayOrders.Count > 0)
            {
                selectedIds = options
                    .Where(o => selectedDisplayOrders.Contains(o.DisplayOrder))
                    .Select(o => o.OptionId)
                    .ToHashSet();
            }

            var correctOptionIds = options.Where(o => o.IsCorrect).Select(o => o.OptionId).ToHashSet();
            var correctDisplayOrders = options.Where(o => o.IsCorrect).Select(o => o.DisplayOrder).ToHashSet();
            var hasAnswerKey = correctOptionIds.Count > 0 || correctDisplayOrders.Count > 0;
            var isCorrect = hasAnswerKey &&
                            (correctDisplayOrders.SetEquals(selectedDisplayOrders) ||
                             correctOptionIds.SetEquals(selectedIds));

            if (isCorrect) correct++;

            // Build review for this question (all correct + wrongly selected)
            var reviewOptions = options
                .Where(o => o.IsCorrect || selectedIds.Contains(o.OptionId) || selectedDisplayOrders.Contains(o.DisplayOrder))
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new ResultOptionDto(
                    o.OptionId,
                    OptionLetterForDisplayOrder(o.DisplayOrder),
                    o.GetText(lang),
                    o.IsCorrect,
                    selectedIds.Contains(o.OptionId) || selectedDisplayOrders.Contains(o.DisplayOrder)
                )).ToList();

            reviewItems.Add(new ResultReviewItemDto(
                position++,
                question.QuestionId,
                question.GetText(lang),
                isCorrect,
                reviewOptions
            ));

            // Persist each selected option
            foreach (var optionId in selectedIds)
            {
                attempt.Answers.Add(new AttemptAnswer
                {
                    AttemptAnswerId = Guid.NewGuid(),
                    AttemptId = attempt.AttemptId,
                    QuestionId = question.QuestionId,
                    OptionId = optionId,
                    SelectedAt = DateTimeOffset.UtcNow
                });
            }
        }

        var scorePercent = total > 0 ? (decimal)correct / total * 100 : 0;
        var passed = scorePercent >= quiz.PassingScore;

        attempt.Status       = passed ? "Passed" : "Failed";
        attempt.ScorePercent  = Math.Round(scorePercent, 2);
        attempt.CorrectCount  = correct;
        attempt.TotalCount    = total;
        attempt.CurrentQuestionPosition = total;
        attempt.CompletedAt   = DateTimeOffset.UtcNow;

        await _attemptRepo.UpdateAttemptAsync(attempt, ct);

        // Check for full platform completion
        bool allNowComplete = false;
        if (passed && !attempt.IsTestData)
        {
            var existingCompletion = await _completionRepo.GetByUserIdAsync(userId, ct);
            if (existingCompletion == null)
            {
                var allQuizzes = await _quizRepo.GetAllPublishedAsync("en", ct);
                var allAttempts = await _attemptRepo.GetAllAttemptsForUserAsync(userId, ct);
                var quizById = allQuizzes.ToDictionary(q => q.QuizId);
                var passedQuizIds = allAttempts
                    .Where(a => !a.IsTestData && quizById.ContainsKey(a.QuizId))
                    .Where(a =>
                    {
                        var completedQuiz = quizById[a.QuizId];
                        var inferredScore = QuizAttemptEvaluator.TryInferScoreFromAnswers(a, completedQuiz);
                        return QuizAttemptEvaluator.IsPassedAttempt(a, inferredScore, completedQuiz.PassingScore);
                    })
                    .Select(a => a.QuizId)
                    .ToHashSet();

                var passedModuleCompletionDates = allAttempts
                    .Where(a => !a.IsTestData && quizById.ContainsKey(a.QuizId))
                    .GroupBy(a => a.QuizId)
                    .Select(g => g
                        .Where(a =>
                        {
                            var completedQuiz = quizById[g.Key];
                            var inferredScore = QuizAttemptEvaluator.TryInferScoreFromAnswers(a, completedQuiz);
                            return QuizAttemptEvaluator.IsPassedAttempt(a, inferredScore, completedQuiz.PassingScore);
                        })
                        .Select(a => a.CompletedAt ?? a.StartedAt)
                        .OrderBy(date => date)
                        .Cast<DateTimeOffset?>()
                        .FirstOrDefault())
                    .ToList();

                var allPassed = CompletionRuleEvaluator.HasMetCompletionRule(
                    allQuizzes.Count,
                    passedQuizIds.Count,
                    passedModuleCompletionDates);
                if (allPassed)
                {
                    var user = await _userRepo.GetByIdAsync(userId, ct)!;
                    var completion = new CompletionRecord
                    {
                        CompletionId = Guid.NewGuid(),
                        UserId = userId,
                        CompletedAt = CompletionRuleEvaluator.GetDerivedCompletionDate(
                            allQuizzes.Count,
                            passedQuizIds.Count,
                            passedModuleCompletionDates) ?? DateTimeOffset.UtcNow,
                        SnapshotFirstName = user!.FirstName,
                        SnapshotLastName = user.LastName,
                        SnapshotEmail = user.Email,
                        SnapshotEmployeeId = user.EmployeeId,
                        SnapshotCompany = user.Company ?? string.Empty,
                        SnapshotPortfolio = user.Portfolio ?? string.Empty
                    };
                    await _completionRepo.CreateAsync(completion, ct);
                    allNowComplete = true;

                    await _auditLog.WriteAsync(
                        "system.completion_written", "success",
                        null, "system", userId,
                        null, null, "system.completion", ct: ct);
                }
            }
        }

        return new QuizResultDto(
            passed,
            (int)Math.Round(scorePercent),
            correct,
            total,
            allNowComplete,
            reviewItems
        );
    }

    private static char OptionLetterForDisplayOrder(int displayOrder)
    {
        var normalizedOrder = Math.Clamp(displayOrder, 1, 26);
        return (char)('A' + normalizedOrder - 1);
    }

}
