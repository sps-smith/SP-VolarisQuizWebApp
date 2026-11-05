using Volaris101.Application.Dtos;
using Volaris101.Domain.Entities;

namespace Volaris101.Application.Interfaces;

public interface ILearnerService
{
    Task<LearnerDashboardDto> GetDashboardAsync(Guid userId, string lang = "en", CancellationToken ct = default);
    Task<StartQuizResponseDto> StartQuizAsync(Guid userId, Guid quizId, string lang = "en", CancellationToken ct = default);
    Task<QuizResultDto> SubmitQuizAsync(Guid userId, SubmitQuizRequestDto request, string lang = "en", CancellationToken ct = default);
}

public interface IReportingService
{
    Task<ReportingOverviewDto> GetOverviewAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default);
    Task<ReportingDashboardDto> GetLanguageDashboardAsync(CancellationToken ct = default);
    Task<int> GetUserActivityCountAsync(ReportingFilterDto filter, bool includeDrafts = false, CancellationToken ct = default);
    Task<IReadOnlyList<UserActivityRowDto>> GetUserActivityAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default);
    Task<int> GetExportDataCountAsync(ReportingFilterDto filter, bool includeDrafts = false, CancellationToken ct = default);
    Task<IReadOnlyList<ReportingExportRowDto>> GetExportDataAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyCompletionReportRowDto>> GetMonthlyCompletionReportAsync(ReportingFilterDto filter, CancellationToken ct = default);
    Task<PortfolioReportDto> GetPortfolioReportAsync(ReportingFilterDto filter, CancellationToken ct = default);
    Task<PortfolioReportDiagnosticDto> GetPortfolioReportDiagnosticAsync(ReportingFilterDto filter, CancellationToken ct = default);
    Task<NotStartedUsersResultDto> GetNotStartedUsersAsync(ReportingFilterDto filter, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctDomainsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CompanyGroupDto>> GetAvailableCompaniesAsync(CancellationToken ct = default);
}

public interface IPeopleDirectoryService
{
    Task<IReadOnlyList<PeopleDirectoryEmployeeDto>> GetEmployeesByCompaniesAsync(IReadOnlyList<string> companyNames, CancellationToken ct = default);
}

public interface ICmsService
{
    Task<IReadOnlyList<CmsQuizSummaryDto>> GetQuizListAsync(string lang = "en", CancellationToken ct = default);
    Task<CmsQuizDetailDto> GetQuizDetailAsync(Guid quizId, string lang = "en", CancellationToken ct = default);
    Task<CmsQuestionDto> GetQuestionAsync(Guid questionId, string lang = "en", CancellationToken ct = default);
    Task<IReadOnlyList<CmsQuestionDto>> GetAllQuestionsAsync(string? searchTerm, Guid? quizId, string lang = "en", CancellationToken ct = default);
    Task<CmsQuestionDto> AddQuestionAsync(Guid quizId, CmsQuestionDto dto, CancellationToken ct = default);
    Task<CmsQuestionDto> UpdateQuestionAsync(Guid questionId, CmsQuestionDto dto, CancellationToken ct = default);
    Task DeleteQuestionAsync(Guid questionId, CancellationToken ct = default);
    Task<CmsQuizSummaryDto> AddQuizAsync(string title, CancellationToken ct = default);
    Task<CmsQuizSummaryDto> UpdateQuizAsync(Guid quizId, string title, CancellationToken ct = default);
    Task<CmsQuizSummaryDto> PublishQuizAsync(Guid quizId, Guid actorUserId, CancellationToken ct = default);
}

public interface IUserAdminService
{
    Task<int> GetUsersCountAsync(string? searchTerm, CancellationToken ct = default);
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(string? searchTerm, int? take = null, CancellationToken ct = default);
    Task<IReadOnlyList<MergeUserCandidateDto>> SearchMergeCandidatesAsync(string? searchTerm, int take = 25, CancellationToken ct = default);
    Task<MergeUsersPreviewDto> PreviewMergeUsersAsync(MergeUsersRequestDto request, Guid actorUserId, CancellationToken ct = default);
    Task<MergeUsersResultDto> ExecuteMergeUsersAsync(MergeUsersRequestDto request, Guid actorUserId, CancellationToken ct = default);
    Task<AdminUserDto> AddUserAsync(AddUserRequestDto request, Guid actorUserId, CancellationToken ct = default);
    Task<AdminUserDto> UpdateRoleAsync(Guid targetUserId, UpdateRoleRequestDto request, Guid actorUserId, CancellationToken ct = default);
    Task<UserAccessScopeDto> GetAccessAsync(Guid targetUserId, CancellationToken ct = default);
    Task<UserAccessScopeDto> UpdateAccessAsync(Guid targetUserId, UpdateUserAccessRequestDto request, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessOptionDto>> SearchOrganizationsAsync(string? searchTerm, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationCompanyOptionDto>> SearchOrganizationCompaniesAsync(string? searchTerm, CancellationToken ct = default);
    Task<IReadOnlyList<AccessOptionDto>> SearchPortfoliosAsync(string? searchTerm, CancellationToken ct = default);
    Task<AdminUserDto> SetActiveStatusAsync(Guid targetUserId, bool isActive, Guid actorUserId, CancellationToken ct = default);
}

public interface IAccessGroupService
{
    Task<AccessGroupSeedResultDto> SeedSystemManagedGroupsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccessGroup>> GetAccessGroupsAsync(CancellationToken ct = default);
    Task<AccessGroup?> GetAccessGroupByIdAsync(Guid accessGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessGroupListItemDto>> GetGroupListAsync(string? searchTerm = null, string? groupTypeCode = null, bool includeInactive = true, string? organizationSearchTerm = null, CancellationToken ct = default);
    Task<AccessGroupDetailDto> GetGroupDetailAsync(Guid accessGroupId, CancellationToken ct = default);
    Task<AccessGroupDetailDto> SetGroupActiveStatusAsync(Guid accessGroupId, bool isActive, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessGroupAssignmentDto>> GetUserGroupAssignmentsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessGroupAssignmentDto>> SetUserGroupAssignmentsAsync(Guid userId, IReadOnlyList<Guid>? accessGroupIds, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessGroupUserDto>> GetGroupUsersAsync(Guid accessGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessGroupUserDto>> AddUserToGroupAsync(Guid accessGroupId, Guid userId, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessGroupUserDto>> RemoveUserFromGroupAsync(Guid accessGroupId, Guid userId, Guid actorUserId, CancellationToken ct = default);
    Task<HashSet<int>> ResolveEffectiveOrganizationIdsAsync(Guid accessGroupId, CancellationToken ct = default);
}

public interface IReportingScopeService
{
    Task<HashSet<int>?> GetEffectiveOrganizationScopeAsync(Guid userId, string roleCode, CancellationToken ct = default);
}

public interface IReportingDashboardRepository
{
    Task<ReportingDashboardDto> GetLanguageDashboardAsync(CancellationToken ct = default);
}

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string Login { get; }
    string Email { get; }
    string FullName { get; }
    string RoleCode { get; }
    string PreferredLanguage { get; }
    bool IsAuthenticated { get; }
    bool IsImpersonating { get; }
    Guid ImpersonatorUserId { get; }
    string ImpersonatorLogin { get; }
    string ImpersonatorEmail { get; }
    string ImpersonatorFullName { get; }
    string ImpersonatorRoleCode { get; }
    string ImpersonatorPreferredLanguage { get; }

    bool IsLearner => RoleCode == "Learner";
    bool IsOrgAdmin => RoleCode is "ReportingAdmin" or "OrgAdmin" or "PortfolioAdmin" or "GlobalAdmin";
    bool IsPortfolioAdmin => RoleCode is "ReportingAdmin" or "PortfolioAdmin" or "GlobalAdmin";
    bool IsGlobalAdmin => RoleCode == "GlobalAdmin";
}
