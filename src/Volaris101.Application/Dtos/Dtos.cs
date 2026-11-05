namespace Volaris101.Application.Dtos;

// ─── Learner Dashboard ───────────────────────────────────────────────────────

public record LearnerDashboardDto(
    string FirstName,
    string Initials,
    int PassedCount,
    int TotalCount,
    int ProgressPercent,
    bool IsFullyCompleted,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ModuleCardDto> Modules
);

public record ModuleCardDto(
    Guid QuizId,
    int DisplayOrder,
    string Title,
    bool IsPublished,
    string Status,       // "ns" | "ip" | "pass" | "fail"
    string StatusLabel,  // "Not started" | "In progress" | "Passed" | "Failed"
    int? BestScore,
    int? AttemptCount,
    string CtaLabel      // "Start quiz" | "Restart quiz" | "Retake"
);

// ─── Quiz Session ─────────────────────────────────────────────────────────────

public record StartQuizResponseDto(
    Guid AttemptId,
    Guid QuizId,
    int DisplayOrder,
    string ModuleTitle,
    IReadOnlyList<QuizQuestionDto> Questions   // shuffled
);

public record QuizQuestionDto(
    Guid QuestionId,
    int Position,           // 1-based within shuffled order
    string QuestionText,
    bool IsMultiSelect,
    IReadOnlyList<QuizOptionDto> Options       // shuffled
);

public record QuizOptionDto(
    Guid OptionId,
    string OptionText,
    char OptionLetter,      // A, B, C …
    int DisplayOrder        // stable original option order
);

// ─── Submit Quiz ──────────────────────────────────────────────────────────────

public record SubmitQuizRequestDto(
    Guid AttemptId,
    IReadOnlyList<QuestionAnswerDto> Answers
);

public record QuestionAnswerDto(
    Guid QuestionId,
    IReadOnlyList<Guid> SelectedOptionIds,
    IReadOnlyList<int>? SelectedOptionDisplayOrders = null
);

public record QuizResultDto(
    bool Passed,
    int ScorePercent,
    int CorrectCount,
    int TotalCount,
    bool AllModulesNowComplete,
    IReadOnlyList<ResultReviewItemDto> Review
);

public record ResultReviewItemDto(
    int Position,
    Guid QuestionId,
    string QuestionText,
    bool IsCorrect,
    IReadOnlyList<ResultOptionDto> Options     // only correct + wrongly-selected
);

public record ResultOptionDto(
    Guid OptionId,
    char Letter,
    string OptionText,
    bool IsCorrect,
    bool WasSelected
);

// ─── Reporting / Admin ────────────────────────────────────────────────────────

public record ReportingOverviewDto(
    int TotalLearners,
    int FullyCompleted,
    int AvgCompletionPercent,
    int NotStarted,
    IReadOnlyList<ModuleCompletionBarDto> CompletionBars,
    IReadOnlyList<ModuleScoreBarDto> ScoreBars,
    int InProgress = 0,
    int CompletedPercent = 0,
    int InProgressPercent = 0,
    int NotStartedPercent = 0
);

public record ReportingDashboardDto(
    DateTimeOffset? LastRefreshedAt,
    IReadOnlyList<ReportingDashboardRowDto> Rows
);

public record ReportingDashboardRowDto(
    string LanguageCode,
    string LanguageName,
    int InProgressCount,
    int CompletedCount,
    int TotalUsers,
    DateTimeOffset? LastRefreshedAt,
    bool IsTotalsRow,
    int SortOrder
);

public record ModuleCompletionBarDto(
    int DisplayOrder,
    string Title,
    int PassedCount,
    int TotalLearners,
    int Percent
);

public record ModuleScoreBarDto(
    int DisplayOrder,
    string Title,
    int AvgScorePercent
);

public record UserActivityModuleDto(
    int DisplayOrder,
    string ModuleName,
    string ScoreLabel,
    DateTimeOffset? LastCompletedAt
);

public record UserActivityRowDto(
    Guid UserId,
    string FullName,
    string Email,
    string Company,
    string Portfolio,
    int ModulesPassed,
    bool IsFullyCompleted,
    DateTimeOffset? LastActiveAt,
    bool IsActive,
    string ActivityStatus,
    IReadOnlyList<int> PassedModuleOrders,
    IReadOnlyList<UserActivityModuleDto> ModuleDetails
);

public record ReportingExportRowDto(
    string FullName,
    string Email,
    string EmployeeId,
    string Company,
    string Portfolio,
    int ModulesPassed,
    bool IsFullyCompleted,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LastActiveAt
);

public record NotStartedUserRowDto(
    string EmployeeName,
    string Email,
    string ManagerName,
    string ManagerEmail,
    string Company,
    string Portfolio
);

public record NotStartedUsersResultDto(
    bool RequiresCompanySelection,
    IReadOnlyList<NotStartedUserRowDto> Rows
);

public record MonthlyCompletionReportRowDto(
    string Name,
    string EmployeeId,
    string Portfolio
);
public record PortfolioReportRowDto(
    string Employee,
    string Vbu,
    string Online,
    DateTimeOffset? LastParticipated
);

public record PortfolioReportOrganizationDto(
    int OrganizationId,
    string OrganizationName,
    int TotalPeople,
    int OnlineCompleteCount,
    int OnlineInProgressCount,
    IReadOnlyList<PortfolioReportRowDto> Rows
);

public record PortfolioReportDto(
    string PortfolioName,
    IReadOnlyList<PortfolioReportOrganizationDto> Organizations
);

public record PortfolioReportDiagnosticSelectedCompanyDto(
    int OrganizationId,
    string CompanyName,
    string PortfolioName
);

public record PortfolioReportDiagnosticEmployeeDto(
    string EmployeeName,
    string Email,
    string Company,
    string Vbu,
    int EmployeeUserId,
    int? OrganizationId,
    string Status,
    string? Reason
);

public record PortfolioReportDiagnosticAppUserDto(
    Guid UserId,
    string FullName,
    string Email,
    string EmployeeId,
    int? OrganizationId,
    string Company,
    string Status,
    string? Reason
);

public record PortfolioReportDiagnosticDto(
    string PortfolioName,
    IReadOnlyList<PortfolioReportDiagnosticSelectedCompanyDto> SelectedCompanies,
    int ApiReturnedCount,
    int ApiDedupedCount,
    int ApiMappedCount,
    int ApiUnmappedCount,
    int SelectedAppUserCount,
    int ActiveAppUserCount,
    int NotStartedCount,
    IReadOnlyList<PortfolioReportDiagnosticEmployeeDto> UnmappedApiEmployees,
    IReadOnlyList<PortfolioReportDiagnosticAppUserDto> DroppedAppUsers,
    IReadOnlyList<PortfolioReportDiagnosticEmployeeDto> NotStartedEmployees
);

public record PeopleDirectoryEmployeeDto(
    string EmployeeName,
    string Email,
    string Company,
    string Vbu,
    int EmployeeUserId,
    int? OrganizationId,
    string? ManagerUserPrincipalName = null
);

// ─── CMS / Course Admin ───────────────────────────────────────────────────────

public record CmsQuizSummaryDto(
    Guid QuizId,
    int DisplayOrder,
    string Title,
    int QuestionCount,
    bool IsActive,
    bool IsPublished
);

public record CmsQuizDetailDto(
    Guid QuizId,
    int DisplayOrder,
    string Title,
    bool IsActive,
    bool IsPublished,
    string SelectedLanguage,
    IReadOnlyList<CmsQuestionDto> Questions,
    IReadOnlyList<CmsLanguageStatusDto> Languages
);

public record CmsLanguageStatusDto(
    string Code,
    string Label,
    int QuestionCount,
    bool IsPrimary,
    bool IsSelected
);

public record CmsQuestionDto(
    Guid QuestionId,
    int DisplayOrder,
    string QuestionText,
    bool IsMultiSelect,
    bool IsActive,
    IReadOnlyList<CmsOptionDto> Options,
    Guid? QuizId = null,
    int? QuizDisplayOrder = null,
    string? QuizTitle = null,
    string? LanguageCode = null
);

public record CmsOptionDto(
    Guid OptionId,
    int DisplayOrder,
    string OptionText,
    bool IsCorrect
);

// ─── Admin User Management ────────────────────────────────────────────────────

public record AdminUserDto(
    Guid UserId,
    string FullName,
    string Email,
    string EmployeeId,
    string Company,
    string Portfolio,
    string ScopedCompaniesAndPortfolios,
    int? OrganizationId,
    int? PortfolioId,
    string RoleCode,
    string RoleName,
    DateTimeOffset? LastLoginAt,
    bool IsActive
);

public record AddUserRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string EmployeeId,
    string Company,
    int? OrganizationId,
    string Portfolio,
    int? PortfolioId,
    string RoleCode
);

public record OrganizationCompanyOptionDto(
    int OrganizationId,
    string OrganizationName,
    int? PortfolioId,
    string? PortfolioName
);

public record UpdateRoleRequestDto(
    string RoleCode
);

public record AccessOptionDto(
    int Id,
    string Name
);

public record UserAccessScopeDto(
    Guid UserId,
    string RoleCode,
    IReadOnlyList<AccessOptionDto> AssignedOrganizations,
    IReadOnlyList<AccessOptionDto> AssignedPortfolios
);

public record UpdateUserAccessRequestDto(
    string RoleCode,
    IReadOnlyList<int>? OrganizationIds,
    IReadOnlyList<int>? PortfolioIds
);

public record AccessGroupListItemDto(
    Guid AccessGroupId,
    string Name,
    string GroupTypeCode,
    string GroupTypeName,
    int? PortfolioId,
    int? OrganizationId,
    int? ParentPortfolioId,
    string? ParentPortfolioName,
    bool IsSystemManaged,
    bool IsActive,
    int EffectiveOrganizationCount,
    int AssignedUserCount
);

public record AccessGroupOrganizationDto(
    int OrganizationId,
    string OrganizationName,
    int ReportToId,
    string ReportToName
);

public record AccessGroupDetailDto(
    Guid AccessGroupId,
    string Name,
    string GroupTypeCode,
    string GroupTypeName,
    string? Description,
    int? PortfolioId,
    int? OrganizationId,
    int? ParentPortfolioId,
    string? ParentPortfolioName,
    bool IsSystemManaged,
    bool IsActive,
    int AssignedUserCount,
    IReadOnlyList<AccessGroupOrganizationDto> EffectiveOrganizations
);

public record AccessGroupAssignmentDto(
    Guid AccessGroupId,
    string Name,
    string GroupTypeCode,
    int EffectiveOrganizationCount,
    bool IsActive
);

public record AccessGroupUserDto(
    Guid UserId,
    string FullName,
    string Email,
    string RoleCode,
    string RoleName,
    bool IsActive,
    DateTimeOffset AssignedAt
);

public record UpdateUserGroupAssignmentsRequestDto(
    IReadOnlyList<Guid>? AccessGroupIds
);

public record MergeUsersRequestDto(
    IReadOnlyList<Guid>? SourceUserIds,
    Guid TargetUserId
);

public record MergeUserCandidateDto(
    Guid UserId,
    string FullName,
    string Email,
    string EmployeeId,
    string Company,
    string Portfolio,
    int? OrganizationId,
    int? PortfolioId,
    string RoleCode,
    string RoleName,
    bool IsActive
);

public record MergeUsersStatisticsDto(
    int QuizAttemptsToMove,
    int SourceCompletionRecordCount,
    bool TargetHasCompletionRecord,
    bool TargetWillBeComplete
);

public record MergeUsersPreviewDto(
    IReadOnlyList<MergeUserCandidateDto> Sources,
    MergeUserCandidateDto? Target,
    MergeUsersStatisticsDto Statistics,
    IReadOnlyList<string> Warnings,
    bool CanMerge
);

public record MergeUsersResultDto(
    Guid TargetUserId,
    int SourceUserCount,
    int QuizAttemptsMoved,
    int SourceUsersDeactivated,
    int SourceCompletionRecordsMerged,
    bool TargetHasCompletionRecord
);

// ─── Filters ─────────────────────────────────────────────────────────────────

public record ReportingFilterDto(
    string? SearchTerm,
    string? Domain,
    int? ModuleOrder,
    int? LastActiveDays,
    int[]? Companies,
    string? Portfolio,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    string[]? Domains = null,
    string? RefreshToken = null,
    string[]? Status = null,
    bool IncludeLeavers = false
);

public record CompanyOptionDto(int OrganizationId, string CompanyName, bool IsPortfolio = false);

public record CompanyGroupDto(string PortfolioName, IReadOnlyList<CompanyOptionDto> Companies);

public record AccessGroupSeedResultDto(
    int PortfolioGroupsCreated,
    int PortfolioSubGroupsCreated
);
