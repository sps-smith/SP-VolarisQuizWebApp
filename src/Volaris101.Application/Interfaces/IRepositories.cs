using Volaris101.Domain.Entities;
using Volaris101.Application.Dtos;

namespace Volaris101.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<User?> GetByLoginAsync(string login, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByEmployeeIdAsync(string employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<User>> SearchActiveByPrefixAsync(string? searchTerm, int? take = null, CancellationToken ct = default);
    Task<int> CountActiveAsync(string? searchTerm = null, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetActiveAsync(string? searchTerm = null, int? take = null, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetFilteredReportingUsersAsync(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        ReportingFilterDto filter,
        int? take = null,
        CancellationToken ct = default,
        Guid? includeUserId = null);
    Task<IReadOnlyList<Guid>> GetFilteredReportingUserIdsAsync(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        ReportingFilterDto filter,
        int? take = null,
        CancellationToken ct = default);
    Task<int> CountFilteredReportingUsersAsync(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        ReportingFilterDto filter,
        CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctReportingDomainsAsync(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        CancellationToken ct = default);
    Task<IReadOnlyList<CompanyGroupDto>> GetAvailableReportingCompaniesAsync(
        IReadOnlyCollection<int>? scopeOrganizationIds,
        CancellationToken ct = default);
    Task<User> CreateAsync(User user, CancellationToken ct = default);
    Task<User> UpdateAsync(User user, CancellationToken ct = default);
}

public interface IReportingAccessRepository
{
    Task<IReadOnlyList<OrgAdminAssignment>> GetOrgAssignmentsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PortfolioAdminAssignment>> GetPortfolioAssignmentsAsync(Guid userId, CancellationToken ct = default);
    Task SetOrgAssignmentsAsync(Guid userId, IReadOnlyList<int> organizationIds, Guid actorUserId, CancellationToken ct = default);
    Task SetPortfolioAssignmentsAsync(Guid userId, IReadOnlyList<int> portfolioIds, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Organization>> SearchOrganizationsAsync(string? searchTerm, int take = 25, CancellationToken ct = default);
    Task<IReadOnlyList<Portfolio>> SearchPortfoliosAsync(string? searchTerm, int take = 25, CancellationToken ct = default);
    Task<Portfolio?> GetPortfolioByNameAsync(string portfolioName, CancellationToken ct = default);
    Task<IReadOnlyList<Organization>> GetOrganizationsByIdsAsync(IReadOnlyCollection<int> organizationIds, CancellationToken ct = default);
    Task<IReadOnlyList<Portfolio>> GetPortfoliosByIdsAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetOrganizationIdsForPortfoliosAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default);
    Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default);
}

public interface IAccessGroupRepository
{
    Task<IReadOnlyList<AccessGroup>> GetAccessGroupsAsync(CancellationToken ct = default);
    Task<AccessGroup?> GetAccessGroupByIdAsync(Guid accessGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessGroup>> GetAccessGroupsByIdsAsync(IReadOnlyCollection<Guid> accessGroupIds, CancellationToken ct = default);
    Task UpdateAccessGroupAsync(AccessGroup group, CancellationToken ct = default);
    Task<IReadOnlyList<UserAccessGroupAssignment>> GetUserGroupAssignmentsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserAccessGroupAssignment>> GetGroupUserAssignmentsAsync(Guid accessGroupId, CancellationToken ct = default);
    Task SetUserGroupAssignmentsAsync(Guid userId, IReadOnlyList<Guid> accessGroupIds, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> CountUserAssignmentsByGroupAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Portfolio>> GetAllPortfoliosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default);
    Task AddAccessGroupsAsync(IReadOnlyList<AccessGroup> groups, CancellationToken ct = default);
}

public interface IQuizRepository
{
    Task<IReadOnlyList<Quiz>> GetAllActiveAsync(string lang = "en", CancellationToken ct = default);
    Task<IReadOnlyList<Quiz>> GetAllPublishedAsync(string lang = "en", CancellationToken ct = default);
    Task<Quiz?> GetByIdAsync(Guid quizId, string lang = "en", CancellationToken ct = default);
    Task<Quiz?> GetByIdForLearnerAsync(Guid quizId, bool includeDrafts, string lang = "en", CancellationToken ct = default);
    Task<Quiz> CreateAsync(Quiz quiz, CancellationToken ct = default);
    Task<Quiz> UpdateAsync(Quiz quiz, CancellationToken ct = default);
    Task<int> PublishAsync(Quiz quiz, CancellationToken ct = default);
    Task<Question?> GetQuestionByIdAsync(Guid questionId, string lang = "en", CancellationToken ct = default);
    Task<Question> CreateQuestionAsync(Question question, CancellationToken ct = default);
    Task<Question> UpdateQuestionAsync(Question question, CancellationToken ct = default);
    Task DeleteQuestionAsync(Guid questionId, CancellationToken ct = default);
    Task<IReadOnlyList<Question>> SearchQuestionsAsync(string? searchTerm, Guid? quizId, string lang = "en", CancellationToken ct = default);
}

public interface IAttemptRepository
{
    Task<QuizAttempt?> GetAttemptByIdAsync(Guid attemptId, CancellationToken ct = default);
    Task<int> GetAttemptCountAsync(Guid userId, Guid quizId, CancellationToken ct = default);
    Task<IReadOnlyList<QuizAttempt>> GetUserAttemptsForQuizAsync(Guid userId, Guid quizId, CancellationToken ct = default);
    Task<IReadOnlyList<QuizAttempt>> GetAllAttemptsForUserAsync(Guid userId, CancellationToken ct = default);
    Task<QuizAttempt> CreateAttemptAsync(QuizAttempt attempt, CancellationToken ct = default);
    Task<QuizAttempt> UpdateAttemptAsync(QuizAttempt attempt, CancellationToken ct = default);

    // Reporting queries
    Task<IReadOnlyList<QuizAttempt>> GetAttemptsForReportingAsync(bool includeTestData = false, CancellationToken ct = default);
    Task<IReadOnlyList<(Guid UserId, Guid QuizId, bool HasPassed, int TotalAttempts, decimal? BestScore, DateTimeOffset? LastAttemptAt)>>
        GetPassStatusSummaryAsync(
            bool includeTestData = false,
            IReadOnlyCollection<Guid>? userIds = null,
            CancellationToken ct = default);
}

public interface ICompletionRepository
{
    Task<CompletionRecord?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<CompletionRecord> CreateAsync(CompletionRecord record, CancellationToken ct = default);
    Task<CompletionRecord> UpdateAsync(CompletionRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<CompletionRecord>> GetAllAsync(CancellationToken ct = default);
}

public interface IAuditLogRepository
{
    Task WriteAsync(string actionType, string outcome, Guid? actorUserId, string actorType,
        Guid? targetUserId, string? beforeValue, string? afterValue, string? sourceComponent,
        string? denialReason = null, string? ipAddress = null, string? additionalDataJson = null,
        CancellationToken ct = default);
}

public interface IUserMergeRepository
{
    Task<MergeUsersStatisticsDto> GetMergeStatisticsAsync(
        IReadOnlyCollection<Guid> sourceUserIds,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<MergeUsersResultDto> ExecuteMergeAsync(
        IReadOnlyCollection<Guid> sourceUserIds,
        Guid targetUserId,
        CancellationToken ct = default);
}
