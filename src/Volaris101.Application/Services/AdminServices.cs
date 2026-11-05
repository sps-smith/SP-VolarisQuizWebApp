using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Volaris101.Application.Services;

// ─── Reporting Service ────────────────────────────────────────────────────────

public class ReportingScopeService : IReportingScopeService
{
    private readonly IReportingAccessRepository _accessRepo;
    private readonly IAccessGroupRepository? _accessGroupRepo;

    public ReportingScopeService(IReportingAccessRepository accessRepo, IAccessGroupRepository? accessGroupRepo = null)
    {
        _accessRepo = accessRepo;
        _accessGroupRepo = accessGroupRepo;
    }

    public async Task<HashSet<int>?> GetEffectiveOrganizationScopeAsync(Guid userId, string roleCode, CancellationToken ct = default)
    {
        if (roleCode == "GlobalAdmin")
            return null;

        var groupScope = await ResolveGroupScopeAsync(userId, ct);
        if (groupScope.Count > 0)
            return groupScope;

        var seedOrgIds = new HashSet<int>();

        if (roleCode is "ReportingAdmin" or "OrgAdmin")
        {
            var assigned = await _accessRepo.GetOrgAssignmentsAsync(userId, ct);
            foreach (var orgId in assigned.Select(a => a.OrganizationId).Where(id => id > 0))
                seedOrgIds.Add(orgId);
        }

        if (roleCode is "ReportingAdmin" or "PortfolioAdmin")
        {
            var assigned = await _accessRepo.GetPortfolioAssignmentsAsync(userId, ct);
            var portfolioIds = assigned.Select(a => a.PortfolioId).Where(id => id > 0).Distinct().ToList();
            var orgIds = await _accessRepo.GetOrganizationIdsForPortfoliosAsync(portfolioIds, ct);
            foreach (var orgId in orgIds.Where(id => id > 0))
                seedOrgIds.Add(orgId);
        }

        if (seedOrgIds.Count == 0)
            return new HashSet<int>();

        var vbuRows = await _accessRepo.GetVbuMasterListAsync(ct);
        return ExpandOrganizationScope(seedOrgIds, vbuRows);
    }

    private async Task<HashSet<int>> ResolveGroupScopeAsync(Guid userId, CancellationToken ct)
    {
        if (_accessGroupRepo is null)
            return [];

        var assignments = await _accessGroupRepo.GetUserGroupAssignmentsAsync(userId, ct);
        if (assignments.Count == 0)
            return [];

        var groupIds = assignments.Select(a => a.AccessGroupId).Distinct().ToArray();
        var groups = await _accessGroupRepo.GetAccessGroupsByIdsAsync(groupIds, ct);
        var activeGroups = groups.Where(g => g.IsActive).ToList();
        if (activeGroups.Count == 0)
            return [];

        var vbuRows = await _accessRepo.GetVbuMasterListAsync(ct);
        var scope = new HashSet<int>();
        foreach (var group in activeGroups)
        {
            var seedId = group.GroupTypeCode switch
            {
                AccessGroupTypes.PortfolioGroup => group.PortfolioId,
                AccessGroupTypes.PortfolioSubGroup => group.OrganizationId,
                AccessGroupTypes.OrgGroup => group.OrganizationId,
                _ => null
            };

            if (seedId.HasValue && seedId.Value > 0)
                scope.UnionWith(ExpandOrganizationScope([seedId.Value], vbuRows));
        }

        return scope;
    }

    private static HashSet<int> ExpandOrganizationScope(HashSet<int> seedOrgIds, IReadOnlyList<VbuMasterListRecord> vbuRows)
    {
        var scoped = new HashSet<int>(seedOrgIds);
        var childrenByParent = vbuRows
            .Where(v => v.OrganizationId > 0 && v.ReportToId > 0 && v.OrganizationId != v.ReportToId)
            .GroupBy(v => v.ReportToId)
            .ToDictionary(g => g.Key, g => g.Select(v => v.OrganizationId).Distinct().ToList());

        var queue = new Queue<int>(seedOrgIds);
        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!childrenByParent.TryGetValue(parentId, out var children))
                continue;

            foreach (var childId in children)
            {
                if (scoped.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        return scoped;
    }
}

public class ReportingService : IReportingService
{
    private readonly IReportingDashboardRepository _dashboardRepo;
    private readonly IUserRepository _userRepo;
    private readonly IQuizRepository _quizRepo;
    private readonly IAttemptRepository _attemptRepo;
    private readonly ICompletionRepository _completionRepo;
    private readonly IReportingScopeService _scopeService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPeopleDirectoryService _peopleDirectory;
    private readonly IMemoryCache _cache;

    private bool CanViewAllScopedUsers =>
        _currentUser.IsGlobalAdmin || _currentUser.RoleCode is "ReportingAdmin" or "OrgAdmin";

    public ReportingService(
        IReportingDashboardRepository dashboardRepo,
        IUserRepository userRepo,
        IQuizRepository quizRepo,
        IAttemptRepository attemptRepo,
        ICompletionRepository completionRepo,
        IReportingScopeService scopeService,
        ICurrentUserContext currentUser,
        IPeopleDirectoryService peopleDirectory,
        IMemoryCache cache)
    {
        _dashboardRepo = dashboardRepo;
        _userRepo = userRepo;
        _quizRepo = quizRepo;
        _attemptRepo = attemptRepo;
        _completionRepo = completionRepo;
        _scopeService = scopeService;
        _currentUser = currentUser;
        _peopleDirectory = peopleDirectory;
        _cache = cache;
    }

    public async Task<ReportingOverviewDto> GetOverviewAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default)
    {
        var cacheKey = await BuildReportingCacheKeyAsync("overview", filter, includeDrafts, take, ct);
        return await GetCachedAsync(cacheKey, async () =>
        {
            var users = await GetFilteredUsersAsync(filter, includeDrafts, take, ct);
            var quizzes = await GetCompletionQuizzesAsync(filter, ct);
            var passSummary = await GetAttemptSummaryAsync(quizzes, false, ct);

            var userIdSet = users.Select(u => u.UserId).ToHashSet();
            var visibleSummary = passSummary.Where(p => userIdSet.Contains(p.UserId)).ToList();
            var summaryByUser = visibleSummary
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            int moduleCount = quizzes.Count;
            var appUsers = users.Select(user =>
            {
                var rows = summaryByUser.TryGetValue(user.UserId, out var matchedRows)
                    ? matchedRows
                    : new List<ReportingAttemptSummary>();
                var status = GetOverviewStatus(moduleCount, rows);
                return new
                {
                    User = user,
                    Rows = rows,
                    Status = status
                };
            }).ToList();

            var selectedStatuses = GetSelectedOverviewStatuses(filter);
            var hasStatusFilter = selectedStatuses.Count > 0;
            var hasDetailFilter = HasOverviewActivityFilter(filter);
            var filteredAppUsers = hasStatusFilter
                ? appUsers.Where(x => selectedStatuses.Contains(x.Status)).ToList()
                : appUsers;
            var selectedUserIds = filteredAppUsers.Select(x => x.User.UserId).ToHashSet();
            var filteredSummary = visibleSummary.Where(p => selectedUserIds.Contains(p.UserId)).ToList();
            var directoryOnlyCount = (!hasStatusFilter || selectedStatuses.Contains(OverviewStatus.NotStarted))
                ? await CountDirectoryOnlyNotStartedAsync(filter, users, ct)
                : 0;

            int completed = filteredAppUsers.Count(x => x.Status == OverviewStatus.Completed);
            int inProgress = filteredAppUsers.Count(x => x.Status == OverviewStatus.InProgress);
            int notStarted = filteredAppUsers.Count(x => x.Status == OverviewStatus.NotStarted) + directoryOnlyCount;
            if (hasDetailFilter)
                notStarted = 0;
            int total = completed + inProgress + notStarted;
            int done = completed;

            int avgCompletion = 0;
            if (total > 0)
            {
                var passedCounts = filteredAppUsers.Select(x => x.Rows.Count(p => p.HasPassed))
                    .Concat(Enumerable.Repeat(0, hasDetailFilter ? 0 : directoryOnlyCount));
                avgCompletion = moduleCount > 0 ? (int)Math.Round(passedCounts.Average() / moduleCount * 100) : 0;
            }

            var completionBars = quizzes.OrderBy(q => q.DisplayOrder).Select(q =>
            {
                int passed = filteredSummary.Count(p => p.QuizId == q.QuizId && p.HasPassed);
                int pct = total > 0 ? (int)Math.Round((double)passed / total * 100) : 0;
                return new ModuleCompletionBarDto(q.DisplayOrder, q.GetTitle(), passed, total, pct);
            }).ToList();

            var scoreBars = quizzes.OrderBy(q => q.DisplayOrder).Select(q =>
            {
                var scores = filteredSummary
                    .Where(p => p.QuizId == q.QuizId && p.HasPassed && p.BestScore.HasValue)
                    .Select(p => (double)p.BestScore!.Value)
                    .ToList();
                int avg = scores.Count > 0 ? (int)Math.Round(scores.Average()) : 0;
                return new ModuleScoreBarDto(q.DisplayOrder, q.GetTitle(), avg);
            }).ToList();

            return new ReportingOverviewDto(
                total,
                done,
                avgCompletion,
                notStarted,
                completionBars,
                scoreBars,
                inProgress,
                total > 0 ? (int)Math.Round((double)completed / total * 100) : 0,
                total > 0 ? (int)Math.Round((double)inProgress / total * 100) : 0,
                total > 0 && !hasDetailFilter ? (int)Math.Round((double)notStarted / total * 100) : 0);
        }, ct);
    }

    public async Task<ReportingDashboardDto> GetLanguageDashboardAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsGlobalAdmin)
            throw new UnauthorizedAccessException("Dashboard is only available to GlobalAdmin users.");

        return await _dashboardRepo.GetLanguageDashboardAsync(ct);
    }

    public async Task<int> GetUserActivityCountAsync(ReportingFilterDto filter, bool includeDrafts = false, CancellationToken ct = default)
    {
        var cacheKey = await BuildReportingCacheKeyAsync("user-activity-count", filter, includeDrafts, null, ct);
        return await GetCachedAsync(cacheKey, async () => (await BuildUserActivityRowsAsync(filter, includeDrafts, ct)).Count, ct);
    }

    public async Task<IReadOnlyList<UserActivityRowDto>> GetUserActivityAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default)
    {
        var cacheKey = await BuildReportingCacheKeyAsync("user-activity", filter, includeDrafts, null, ct);
        var rows = await GetCachedAsync(cacheKey, () => BuildUserActivityRowsAsync(filter, includeDrafts, ct), ct);
        return take.HasValue
            ? rows.Take(take.Value).ToList()
            : rows;
    }

    public async Task<int> GetExportDataCountAsync(ReportingFilterDto filter, bool includeDrafts = false, CancellationToken ct = default)
    {
        var cacheKey = await BuildReportingCacheKeyAsync("export-count", filter, includeDrafts, null, ct);
        return await GetCachedAsync(cacheKey, async () => (await GetExportDataAsync(filter, includeDrafts, null, ct)).Count, ct);
    }

    public async Task<IReadOnlyList<ReportingExportRowDto>> GetExportDataAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default)
    {
        var snapshot = await GetReportingSnapshotAsync(filter, includeDrafts, null, ct);

        var summaryByUser = snapshot.PassSummary
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var moduleCount = snapshot.Quizzes.Count;
        var hasActivityFilter = HasModuleOrDateFilter(filter);
        var selectedStatuses = GetSelectedOverviewStatuses(filter);
        var appRows = snapshot.Users
            .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
            .Select(u =>
        {
            var userSummary = summaryByUser.GetValueOrDefault(u.UserId, new());
            var userPasses = userSummary.Where(p => p.HasPassed).ToList();
            var passed = userPasses.Count;
            var isFullyCompleted = CompletionRuleEvaluator.HasMetCompletionRule(
                moduleCount,
                passed,
                userPasses.Select(p => p.LastCompletedAt));
            var hasCompletion = snapshot.CompletionsByUserId.TryGetValue(u.UserId, out var cr);
            var lastActive = LatestActivity(userSummary);
            var completedAt = hasCompletion
                ? cr?.CompletedAt
                : isFullyCompleted
                    ? CompletionRuleEvaluator.GetDerivedCompletionDate(
                        moduleCount,
                        passed,
                        userPasses.Select(p => p.LastCompletedAt))
                    : null;

            return new
            {
                OrganizationId = u.OrganizationId!.Value,
                HasActivity = userSummary.Count > 0,
                Row = new ReportingExportRowDto(
                    u.FullName,
                    u.Email,
                    u.EmployeeId,
                    u.Company ?? string.Empty,
                    u.Portfolio ?? string.Empty,
                    passed,
                    isFullyCompleted,
                    completedAt,
                    lastActive
                )
            };
        })
        .ToList();

        var selectedCompanyIds = NormalizeIds(filter.Companies);
        if (selectedCompanyIds.Count == 0)
        {
            selectedCompanyIds = appRows
                .Where(x => x.HasActivity)
                .Select(x => x.OrganizationId)
                .Distinct()
                .ToList();
        }

        if (selectedCompanyIds.Count == 0)
            return Array.Empty<ReportingExportRowDto>();

        var directoryOnlyLearners = await GetDirectoryOnlyLearnersAsync(
            filter,
            snapshot.Users,
            ct,
            excludeWhenLastActiveSelected: false,
            organizationIdsOverride: selectedCompanyIds);

        var directoryRows = directoryOnlyLearners.Select(x => new ReportingExportRowDto(
            x.Employee.EmployeeName,
            x.Employee.Email,
            x.Employee.EmployeeUserId > 0 ? x.Employee.EmployeeUserId.ToString() : string.Empty,
            x.Company.CompanyName,
            x.Company.PortfolioName,
            0,
            false,
            null,
            null));

        var rows = appRows
            .Where(x => selectedCompanyIds.Contains(x.OrganizationId))
            .Select(x => x.Row)
            .Concat(directoryRows)
            .Where(r => !hasActivityFilter || r.IsFullyCompleted || r.LastActiveAt.HasValue || r.ModulesPassed > 0)
            .Where(r => selectedStatuses.Count == 0 || selectedStatuses.Contains(GetExportRowStatus(r)))
            .Where(r => !filter.LastActiveDays.HasValue || !r.LastActiveAt.HasValue || MatchesLastActiveDays(r.LastActiveAt ?? r.CompletedAt, filter.LastActiveDays.Value))
            .OrderBy(r => r.FullName)
            .ToList();

        return take.HasValue
            ? rows.Take(take.Value).ToList()
            : rows;
    }

    public async Task<IReadOnlyList<MonthlyCompletionReportRowDto>> GetMonthlyCompletionReportAsync(ReportingFilterDto filter, CancellationToken ct = default)
    {
        if (!filter.FromDate.HasValue || !filter.ToDate.HasValue || filter.FromDate > filter.ToDate)
            return Array.Empty<MonthlyCompletionReportRowDto>();

        // Reuse the existing reporting filters for everything except the date
        // window so the monthly report follows the current UI state.
        var filteredRows = await GetExportDataAsync(filter with { FromDate = null, ToDate = null }, _currentUser.IsGlobalAdmin, null, ct);
        var start = filter.FromDate.Value;
        var end = filter.ToDate.Value.AddDays(1).AddTicks(-1);

        return filteredRows
            .Where(r => r.IsFullyCompleted && r.CompletedAt.HasValue && r.CompletedAt.Value >= start && r.CompletedAt.Value <= end)
            .Select(r => new MonthlyCompletionReportRowDto(
                r.FullName,
                r.EmployeeId,
                r.Portfolio ?? string.Empty
            ))
            .OrderBy(r => r.Name)
            .ThenBy(r => r.EmployeeId)
            .ToList();
    }

    public async Task<PortfolioReportDto> GetPortfolioReportAsync(ReportingFilterDto filter, CancellationToken ct = default)
    {
        var availableGroups = await GetAvailableCompaniesAsync(ct);
        var companyLookup = availableGroups
            .SelectMany(g => g.Companies.Select(c => new
            {
                c.OrganizationId,
                c.CompanyName,
                g.PortfolioName
            }))
            .GroupBy(c => c.OrganizationId)
            .Select(g => g.First())
            .ToDictionary(c => c.OrganizationId);

        var selectedCompanyIds = NormalizeIds(filter.Companies);

        if (selectedCompanyIds.Count == 0)
        {
            var derivedUsers = await GetFilteredUsersAsync(filter, _currentUser.IsGlobalAdmin, null, ct);
            var derivedQuizzes = await GetCompletionQuizzesAsync(filter, ct);
            var derivedPassSummary = await GetAttemptSummaryAsync(derivedQuizzes, _currentUser.IsGlobalAdmin, ct);
            var derivedSummaryByUser = derivedPassSummary
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            selectedCompanyIds = derivedUsers
                .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
                .Where(u => derivedSummaryByUser.TryGetValue(u.UserId, out var userSummary) && userSummary.Count > 0)
                .Select(u => u.OrganizationId!.Value)
                .Distinct()
                .ToList();

            if (selectedCompanyIds.Count == 0)
                return new PortfolioReportDto(string.Empty, Array.Empty<PortfolioReportOrganizationDto>());
        }

        var selectedCompanies = selectedCompanyIds
            .Where(companyLookup.ContainsKey)
            .Select(id => new SelectedCompanyReference(
                id,
                companyLookup[id].CompanyName,
                companyLookup[id].PortfolioName))
            .ToList();

        if (selectedCompanies.Count == 0)
            return new PortfolioReportDto(string.Empty, Array.Empty<PortfolioReportOrganizationDto>());

        if (selectedCompanies.Count != selectedCompanyIds.Count)
            throw new InvalidOperationException("One or more selected companies are not available for the current reporting scope.");

        var portfolioNames = selectedCompanies
            .Select(c => c.PortfolioName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (portfolioNames.Count != 1)
            throw new InvalidOperationException("Portfolio report requires all selected companies to belong to the same portfolio.");

        var effectiveFilter = filter with { Companies = selectedCompanyIds.ToArray() };
        var users = await GetFilteredUsersAsync(effectiveFilter, _currentUser.IsGlobalAdmin, null, ct);
        var quizzes = await GetCompletionQuizzesAsync(effectiveFilter, ct);
        var passSummary = await GetAttemptSummaryAsync(quizzes, _currentUser.IsGlobalAdmin, ct);
        var completions = await _completionRepo.GetAllAsync(ct);
        var directoryEmployees = await GetCachedAsync(
            BuildPeopleDirectoryCacheKey(selectedCompanies.Select(c => c.CompanyName)),
            () => _peopleDirectory.GetEmployeesByCompaniesAsync(selectedCompanies.Select(c => c.CompanyName).ToList(), ct),
            ct);
        directoryEmployees = directoryEmployees.Where(employee => !IsDisallowedDirectoryEmployee(employee)).ToList();

        var completionDict = completions.ToDictionary(c => c.UserId);
        var summaryByUser = passSummary
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var moduleCount = quizzes.Count;
        var selectedCompaniesByOrganizationId = selectedCompanies
            .GroupBy(c => c.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());
        var selectedCompaniesByName = selectedCompanies
            .GroupBy(c => NormalizeCompanyName(c.CompanyName))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(g => g.Key, g => g.First());
        var activeDirectoryEmployees = directoryEmployees
            .GroupBy(BuildPeopleDirectoryEmployeeKey)
            .Select(g => g.First())
            .Select(employee => new
            {
                Employee = employee,
                Company = ResolveSelectedCompany(employee, selectedCompaniesByOrganizationId, selectedCompaniesByName)
            })
            .Where(x => x.Company is not null)
            .Select(x => new
            {
                x.Employee,
                Company = x.Company!
            })
            .ToList();

        var activeDirectoryEmployeeKeys = activeDirectoryEmployees
            .Where(x => x.Employee.EmployeeUserId > 0)
            .Select(x => BuildEmployeeOrganizationKey(x.Employee.EmployeeUserId, x.Company.OrganizationId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeDirectoryEmailKeys = activeDirectoryEmployees
            .Select(x => BuildEmailOrganizationKey(x.Employee.Email, x.Company.OrganizationId))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedUsers = users
            .Where(u => u.OrganizationId.HasValue && selectedCompanyIds.Contains(u.OrganizationId.Value))
            .Where(u => filter.IncludeLeavers || ExistsInDirectorySelection(u, activeDirectoryEmployeeKeys, activeDirectoryEmailKeys))
            .ToList();

        var currentSelectionEmployeeKeys = selectedUsers
            .Select(u => NormalizePositiveEmployeeId(u.EmployeeId))
            .Zip(selectedUsers.Select(u => u.OrganizationId), (employeeId, organizationId) => new { employeeId, organizationId })
            .Where(x => x.employeeId.HasValue && x.organizationId.HasValue && x.organizationId.Value > 0)
            .Select(x => BuildEmployeeOrganizationKey(x.employeeId!.Value, x.organizationId!.Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentSelectionEmailKeys = selectedUsers
            .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
            .Select(u => BuildEmailOrganizationKey(u.Email, u.OrganizationId!.Value))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var applicationRows = selectedUsers
            .Select(u =>
            {
                var userSummary = summaryByUser.GetValueOrDefault(u.UserId, new());
                var userPasses = userSummary.Where(p => p.HasPassed).ToList();
                var passed = userPasses.Count;
                var isFullyCompleted = CompletionRuleEvaluator.HasMetCompletionRule(
                    moduleCount,
                    passed,
                    userPasses.Select(p => p.LastCompletedAt));
                var lastActive = LatestActivity(userSummary);
                var completedAt = completionDict.TryGetValue(u.UserId, out var cr)
                    ? cr?.CompletedAt
                    : isFullyCompleted
                        ? CompletionRuleEvaluator.GetDerivedCompletionDate(
                            moduleCount,
                            passed,
                            userPasses.Select(p => p.LastCompletedAt))
                        : null;
                var onlineStatus = isFullyCompleted
                    ? "Completed"
                    : lastActive.HasValue
                        ? "In progress"
                        : "Not Started";

                var organizationId = u.OrganizationId!.Value;
                var organizationName = companyLookup.TryGetValue(organizationId, out var company)
                    ? company.CompanyName
                    : (u.Company ?? $"Organization {organizationId}");

                return new
                {
                    organizationId,
                    organizationName,
                    Row = new PortfolioReportRowDto(
                        u.FullName,
                        organizationName,
                        onlineStatus,
                        isFullyCompleted ? completedAt ?? lastActive : lastActive)
                };
            })
            .Where(x => MatchesPortfolioSearchTerm(x.Row.Employee, x.Row.Vbu, filter.SearchTerm))
            .Select(x => (x.organizationId, x.organizationName, x.Row))
            .ToList();

        var directoryRows = HasModuleOrDateFilter(filter)
            ? new List<(int organizationId, string organizationName, PortfolioReportRowDto Row)>()
            : activeDirectoryEmployees
                .Where(x => !ExistsInCurrentSelection(x.Employee, x.Company.OrganizationId, currentSelectionEmployeeKeys, currentSelectionEmailKeys))
                .Where(x => DomainMatchesFilter(x.Employee.Email, NormalizeDomains(filter)))
                .Select(x =>
                {
                    return new
                    {
                        organizationId = x.Company.OrganizationId,
                        organizationName = x.Company.CompanyName,
                        Row = new PortfolioReportRowDto(
                            x.Employee.EmployeeName,
                            x.Company.CompanyName,
                            "Not Started",
                            null)
                    };
                })
                .Where(x => MatchesPortfolioSearchTerm(x.Row.Employee, x.Row.Vbu, filter.SearchTerm))
                .Select(x => (x.organizationId, x.organizationName, x.Row))
                .ToList();

        var selectedStatuses = GetSelectedOverviewStatuses(filter);
        var organizations = applicationRows
            .Concat(directoryRows)
            .Where(x => selectedStatuses.Count == 0 || selectedStatuses.Contains(x.Row.Online))
            .Where(x => !filter.LastActiveDays.HasValue || MatchesLastActiveDays(x.Row.LastParticipated, filter.LastActiveDays.Value))
            .GroupBy(x => new { x.organizationId, x.organizationName })
            .OrderBy(g => g.Key.organizationName)
            .Select(g =>
            {
                var rows = g
                    .Select(x => x.Row)
                    .OrderBy(r => r.Employee)
                    .ToList();

                return new PortfolioReportOrganizationDto(
                    g.Key.organizationId,
                    g.Key.organizationName,
                    rows.Count,
                    rows.Count(r => string.Equals(r.Online, "Completed", StringComparison.OrdinalIgnoreCase)),
                    rows.Count(r => string.Equals(r.Online, "In progress", StringComparison.OrdinalIgnoreCase)),
                    rows);
            })
            .ToList();

        return new PortfolioReportDto(portfolioNames[0], organizations);
    }

    public async Task<PortfolioReportDiagnosticDto> GetPortfolioReportDiagnosticAsync(ReportingFilterDto filter, CancellationToken ct = default)
    {
        var availableGroups = await GetAvailableCompaniesAsync(ct);
        var companyLookup = availableGroups
            .SelectMany(g => g.Companies.Select(c => new
            {
                c.OrganizationId,
                c.CompanyName,
                g.PortfolioName
            }))
            .GroupBy(c => c.OrganizationId)
            .Select(g => g.First())
            .ToDictionary(c => c.OrganizationId);

        var selectedCompanyIds = NormalizeIds(filter.Companies);

        if (selectedCompanyIds.Count == 0)
        {
            var derivedUsers = await GetFilteredUsersAsync(filter, _currentUser.IsGlobalAdmin, null, ct);
            var derivedQuizzes = await GetCompletionQuizzesAsync(filter, ct);
            var derivedPassSummary = await GetAttemptSummaryAsync(derivedQuizzes, _currentUser.IsGlobalAdmin, ct);
            var derivedSummaryByUser = derivedPassSummary
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            selectedCompanyIds = derivedUsers
                .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
                .Where(u => derivedSummaryByUser.TryGetValue(u.UserId, out var userSummary) && userSummary.Count > 0)
                .Select(u => u.OrganizationId!.Value)
                .Distinct()
                .ToList();

            if (selectedCompanyIds.Count == 0)
                return new PortfolioReportDiagnosticDto(
                    string.Empty,
                    Array.Empty<PortfolioReportDiagnosticSelectedCompanyDto>(),
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<PortfolioReportDiagnosticEmployeeDto>(),
                    Array.Empty<PortfolioReportDiagnosticAppUserDto>(),
                    Array.Empty<PortfolioReportDiagnosticEmployeeDto>());
        }

        var selectedCompanies = selectedCompanyIds
            .Where(companyLookup.ContainsKey)
            .Select(id => new SelectedCompanyReference(
                id,
                companyLookup[id].CompanyName,
                companyLookup[id].PortfolioName))
            .ToList();

        if (selectedCompanies.Count == 0)
            return new PortfolioReportDiagnosticDto(
                string.Empty,
                Array.Empty<PortfolioReportDiagnosticSelectedCompanyDto>(),
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<PortfolioReportDiagnosticEmployeeDto>(),
                Array.Empty<PortfolioReportDiagnosticAppUserDto>(),
                Array.Empty<PortfolioReportDiagnosticEmployeeDto>());

        if (selectedCompanies.Count != selectedCompanyIds.Count)
            throw new InvalidOperationException("One or more selected companies are not available for the current reporting scope.");

        var portfolioNames = selectedCompanies
            .Select(c => c.PortfolioName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (portfolioNames.Count != 1)
            throw new InvalidOperationException("Portfolio report requires all selected companies to belong to the same portfolio.");

        var effectiveFilter = filter with { Companies = selectedCompanyIds.ToArray() };
        var users = await GetFilteredUsersAsync(effectiveFilter, _currentUser.IsGlobalAdmin, null, ct);
        var directoryEmployees = await GetCachedAsync(
            BuildPeopleDirectoryCacheKey(selectedCompanies.Select(c => c.CompanyName)),
            () => _peopleDirectory.GetEmployeesByCompaniesAsync(selectedCompanies.Select(c => c.CompanyName).ToList(), ct),
            ct);
        directoryEmployees = directoryEmployees.Where(employee => !IsDisallowedDirectoryEmployee(employee)).ToList();

        var selectedCompaniesByOrganizationId = selectedCompanies
            .GroupBy(c => c.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());
        var selectedCompaniesByName = selectedCompanies
            .GroupBy(c => NormalizeCompanyName(c.CompanyName))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(g => g.Key, g => g.First());

        var dedupedDirectoryEmployees = directoryEmployees
            .GroupBy(BuildPeopleDirectoryEmployeeKey)
            .Select(g => g.First())
            .ToList();

        var mappedDirectoryEmployees = dedupedDirectoryEmployees
            .Select(employee => new
            {
                Employee = employee,
                Company = ResolveSelectedCompany(employee, selectedCompaniesByOrganizationId, selectedCompaniesByName)
            })
            .ToList();

        var activeDirectoryEmployees = mappedDirectoryEmployees
            .Where(x => x.Company is not null)
            .Select(x => new
            {
                x.Employee,
                Company = x.Company!
            })
            .ToList();

        var activeDirectoryEmployeeKeys = activeDirectoryEmployees
            .Where(x => x.Employee.EmployeeUserId > 0)
            .Select(x => BuildEmployeeOrganizationKey(x.Employee.EmployeeUserId, x.Company.OrganizationId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeDirectoryEmailKeys = activeDirectoryEmployees
            .Select(x => BuildEmailOrganizationKey(x.Employee.Email, x.Company.OrganizationId))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedUsers = users
            .Where(u => u.OrganizationId.HasValue && selectedCompanyIds.Contains(u.OrganizationId.Value))
            .ToList();

        var activeSelectedUsers = selectedUsers
            .Where(u => ExistsInDirectorySelection(u, activeDirectoryEmployeeKeys, activeDirectoryEmailKeys))
            .ToList();

        var droppedAppUsers = selectedUsers
            .Where(u => !ExistsInDirectorySelection(u, activeDirectoryEmployeeKeys, activeDirectoryEmailKeys))
            .Select(u => new PortfolioReportDiagnosticAppUserDto(
                u.UserId,
                u.FullName,
                u.Email,
                u.EmployeeId,
                u.OrganizationId,
                u.Company ?? string.Empty,
                "Dropped",
                "No active People Directory match by employee ID or email within the current report selection."))
            .OrderBy(u => u.FullName)
            .ToList();

        var currentSelectionEmployeeKeys = activeSelectedUsers
            .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
            .Select(u => new { EmployeeId = NormalizePositiveEmployeeId(u.EmployeeId), OrganizationId = u.OrganizationId!.Value })
            .Where(x => x.EmployeeId.HasValue)
            .Select(x => BuildEmployeeOrganizationKey(x.EmployeeId!.Value, x.OrganizationId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentSelectionEmailKeys = activeSelectedUsers
            .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
            .Select(u => BuildEmailOrganizationKey(u.Email, u.OrganizationId!.Value))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var notStartedEmployees = activeDirectoryEmployees
            .Where(x => !ExistsInCurrentSelection(x.Employee, x.Company.OrganizationId, currentSelectionEmployeeKeys, currentSelectionEmailKeys))
            .Select(x => new PortfolioReportDiagnosticEmployeeDto(
                x.Employee.EmployeeName,
                x.Employee.Email,
                x.Employee.Company,
                x.Employee.Vbu,
                x.Employee.EmployeeUserId,
                x.Employee.OrganizationId,
                "Not Started",
                $"Mapped to selected company '{x.Company.CompanyName}' ({x.Company.OrganizationId})."))
            .OrderBy(e => e.EmployeeName)
            .ToList();

        var unmappedApiEmployees = mappedDirectoryEmployees
            .Where(x => x.Company is null)
            .Select(x => new PortfolioReportDiagnosticEmployeeDto(
                x.Employee.EmployeeName,
                x.Employee.Email,
                x.Employee.Company,
                x.Employee.Vbu,
                x.Employee.EmployeeUserId,
                x.Employee.OrganizationId,
                "Unmapped",
                "Could not map the People Directory row to any selected company by organization ID, company name, or VBU."))
            .OrderBy(e => e.EmployeeName)
            .ToList();

        return new PortfolioReportDiagnosticDto(
            portfolioNames[0],
            selectedCompanies
                .Select(c => new PortfolioReportDiagnosticSelectedCompanyDto(c.OrganizationId, c.CompanyName, c.PortfolioName))
                .OrderBy(c => c.CompanyName)
                .ToList(),
            directoryEmployees.Count,
            dedupedDirectoryEmployees.Count,
            activeDirectoryEmployees.Count,
            unmappedApiEmployees.Count,
            selectedUsers.Count,
            activeSelectedUsers.Count,
            notStartedEmployees.Count,
            unmappedApiEmployees,
            droppedAppUsers,
            notStartedEmployees);
    }

    public async Task<NotStartedUsersResultDto> GetNotStartedUsersAsync(ReportingFilterDto filter, CancellationToken ct = default)
    {
        var normalizedFilter = new ReportingFilterDto(
            filter.SearchTerm,
            null,
            null,
            null,
            filter.Companies,
            null,
            null,
            null,
            null,
            null,
            IncludeLeavers: filter.IncludeLeavers);
        var cacheKey = await BuildReportingCacheKeyAsync("not-started-users", normalizedFilter, true, null, ct);

        return await GetCachedAsync(cacheKey, async () =>
        {
            var availableGroups = await GetAvailableCompaniesAsync(ct);
            var companyLookup = availableGroups
                .SelectMany(g => g.Companies.Select(c => new
                {
                    c.OrganizationId,
                    c.CompanyName,
                    g.PortfolioName
                }))
                .GroupBy(c => c.OrganizationId)
                .Select(g => g.First())
                .ToDictionary(c => c.OrganizationId);

            var selectedCompanyIds = (filter.Companies ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (selectedCompanyIds.Count == 0)
            {
                if (_currentUser.IsGlobalAdmin)
                    return new NotStartedUsersResultDto(true, Array.Empty<NotStartedUserRowDto>());

                selectedCompanyIds = companyLookup.Keys.OrderBy(id => id).ToList();
            }

            var selectedCompanies = selectedCompanyIds
                .Where(companyLookup.ContainsKey)
                .Select(id => new SelectedCompanyReference(
                    id,
                    companyLookup[id].CompanyName,
                    companyLookup[id].PortfolioName))
                .ToList();

            if (selectedCompanies.Count != selectedCompanyIds.Count)
                throw new InvalidOperationException("One or more selected companies are not available for the current reporting scope.");

            if (selectedCompanies.Count == 0)
                return new NotStartedUsersResultDto(false, Array.Empty<NotStartedUserRowDto>());

            var scopeFilter = new ReportingFilterDto(
                null,
                null,
                null,
                null,
                selectedCompanyIds.ToArray(),
                null,
                null,
                null,
                null,
                null);
            var users = await GetScopedUsersAsync(scopeFilter, includeAllRoles: true, null, ct);
            users = await ApplyLeaverFilterAsync(users, filter, ct);
            var quizzes = await GetCompletionQuizzesAsync(scopeFilter, ct);
            var passSummary = await GetAttemptSummaryAsync(quizzes, false, ct);
            var summaryByUser = passSummary
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var directoryEmployees = await GetCachedAsync(
                BuildPeopleDirectoryCacheKey(selectedCompanies.Select(c => c.CompanyName)),
                () => _peopleDirectory.GetEmployeesByCompaniesAsync(selectedCompanies.Select(c => c.CompanyName).ToList(), ct),
                ct);
            directoryEmployees = directoryEmployees.Where(employee => !IsDisallowedDirectoryEmployee(employee)).ToList();

            var selectedCompaniesByOrganizationId = selectedCompanies
                .GroupBy(c => c.OrganizationId)
                .ToDictionary(g => g.Key, g => g.First());
            var selectedCompaniesByName = selectedCompanies
                .GroupBy(c => NormalizeCompanyName(c.CompanyName))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToDictionary(g => g.Key, g => g.First());

            var activeDirectoryEmployees = directoryEmployees
                .GroupBy(BuildPeopleDirectoryEmployeeKey)
                .Select(g => g.First())
                .Select(employee => new
                {
                    Employee = employee,
                    Company = ResolveSelectedCompany(employee, selectedCompaniesByOrganizationId, selectedCompaniesByName)
                })
                .Where(x => x.Company is not null)
                .Select(x => new
                {
                    x.Employee,
                    Company = x.Company!
                })
                .ToList();

            var activeDirectoryEmployeeKeys = activeDirectoryEmployees
                .Where(x => x.Employee.EmployeeUserId > 0)
                .Select(x => BuildEmployeeOrganizationKey(x.Employee.EmployeeUserId, x.Company.OrganizationId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var activeDirectoryEmailKeys = activeDirectoryEmployees
                .Select(x => BuildEmailOrganizationKey(x.Employee.Email, x.Company.OrganizationId))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var activeSelectedUsers = users
                .Where(u => u.OrganizationId.HasValue && selectedCompanyIds.Contains(u.OrganizationId.Value))
                .Where(u => ExistsInDirectorySelection(u, activeDirectoryEmployeeKeys, activeDirectoryEmailKeys))
                .ToList();

            var startedSelectedUsers = activeSelectedUsers
                .Where(u => summaryByUser.ContainsKey(u.UserId))
                .ToList();

            var startedSelectionEmployeeKeys = startedSelectedUsers
                .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
                .Select(u => new { EmployeeId = NormalizePositiveEmployeeId(u.EmployeeId), OrganizationId = u.OrganizationId!.Value })
                .Where(x => x.EmployeeId.HasValue)
                .Select(x => BuildEmployeeOrganizationKey(x.EmployeeId!.Value, x.OrganizationId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var startedSelectionEmailKeys = startedSelectedUsers
                .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
                .Select(u => BuildEmailOrganizationKey(u.Email, u.OrganizationId!.Value))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rows = activeDirectoryEmployees
                .Where(x => !ExistsInCurrentSelection(x.Employee, x.Company.OrganizationId, startedSelectionEmployeeKeys, startedSelectionEmailKeys))
                .Select(x => new NotStartedUserRowDto(
                    x.Employee.EmployeeName,
                    x.Employee.Email,
                    string.Empty,
                    string.Empty,
                    x.Company.CompanyName,
                    x.Company.PortfolioName))
                .OrderBy(r => r.Company)
                .ThenBy(r => r.EmployeeName)
                .ToList();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                rows = rows
                    .Where(r =>
                        r.EmployeeName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        r.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new NotStartedUsersResultDto(false, rows);
        }, ct);
    }

    public async Task<IReadOnlyList<string>> GetDistinctDomainsAsync(CancellationToken ct = default)
    {
        var cacheKey = await BuildReportingCacheKeyAsync("domains", new ReportingFilterDto(null, null, null, null, null, null, null, null, null), CanViewAllScopedUsers, null, ct);
        return await GetCachedAsync(cacheKey, async () =>
        {
            var scope = await _scopeService.GetEffectiveOrganizationScopeAsync(_currentUser.UserId, _currentUser.RoleCode, ct);
            if (scope is { Count: 0 })
                return Array.Empty<string>();

            return await _userRepo.GetDistinctReportingDomainsAsync(CanViewAllScopedUsers, scope, ct);
        }, ct);
    }

    public async Task<IReadOnlyList<CompanyGroupDto>> GetAvailableCompaniesAsync(CancellationToken ct = default)
    {
        var cacheKey = await BuildReportingCacheKeyAsync("companies", new ReportingFilterDto(null, null, null, null, null, null, null, null, null), _currentUser.IsGlobalAdmin, null, ct);
        return await GetCachedAsync(cacheKey, async () =>
        {
            var scope = await _scopeService.GetEffectiveOrganizationScopeAsync(_currentUser.UserId, _currentUser.RoleCode, ct);
            if (scope is { Count: 0 })
                return Array.Empty<CompanyGroupDto>();

            return await _userRepo.GetAvailableReportingCompaniesAsync(scope, ct);
        }, ct);
    }

    private async Task<IReadOnlyList<User>> GetFilteredUsersAsync(ReportingFilterDto filter, bool includeDrafts, int? take, CancellationToken ct)
    {
        var scope = await _scopeService.GetEffectiveOrganizationScopeAsync(_currentUser.UserId, _currentUser.RoleCode, ct);
        if (scope is { Count: 0 })
            return Array.Empty<User>();

        var users = await _userRepo.GetFilteredReportingUsersAsync(
            CanViewAllScopedUsers,
            scope,
            filter,
            take,
            ct,
            includeUserId: null);
        users = await RemoveDisallowedUsersAsync(users, ct);
        return await ApplyLeaverFilterAsync(users, filter, ct);
    }

    private async Task<IReadOnlyList<User>> ApplyLeaverFilterAsync(
        IReadOnlyList<User> users,
        ReportingFilterDto filter,
        CancellationToken ct)
    {
        if (filter.IncludeLeavers || users.Count == 0)
            return users;

        var directoryEmployees = await GetPeopleDirectoryEmployeesForUsersAsync(users, ct);
        var directoryByOrganization = directoryEmployees
            .Where(employee => employee.OrganizationId is > 0)
            .ToLookup(employee => employee.OrganizationId!.Value);

        return users
            .Where(user => !IsLeaver(user, directoryByOrganization[user.OrganizationId ?? 0]))
            .ToList();
    }

    private async Task<IReadOnlyList<PeopleDirectoryEmployeeDto>> GetPeopleDirectoryEmployeesForUsersAsync(
        IReadOnlyList<User> users,
        CancellationToken ct)
    {
        var companyNames = users
            .Select(user => user.Company?.Trim())
            .Where(companyName => !string.IsNullOrWhiteSpace(companyName))
            .Select(companyName => companyName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (companyNames.Length == 0)
            return Array.Empty<PeopleDirectoryEmployeeDto>();

        return await GetCachedAsync(
            BuildPeopleDirectoryCacheKey(companyNames),
            () => _peopleDirectory.GetEmployeesByCompaniesAsync(companyNames, ct),
            ct);
    }

    private async Task<IReadOnlyList<User>> RemoveDisallowedUsersAsync(
        IReadOnlyList<User> users,
        CancellationToken ct)
    {
        var directoryEmployees = await GetPeopleDirectoryEmployeesForUsersAsync(users, ct);
        if (directoryEmployees.Count == 0)
            return users;

        var disallowedEmployeeIds = directoryEmployees
            .Where(IsDisallowedDirectoryEmployee)
            .Select(employee => employee.EmployeeUserId.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return users
            .Where(user =>
            {
                var employeeId = NormalizePositiveEmployeeId(user.EmployeeId);
                return !employeeId.HasValue || !disallowedEmployeeIds.Contains(employeeId.Value.ToString());
            })
            .ToList();
    }

    private static bool IsLeaver(
        User user,
        IEnumerable<PeopleDirectoryEmployeeDto> organizationEmployees)
    {
        var employeeId = NormalizeEmployeeId(user.EmployeeId);
        var hasEmployeeId = !string.IsNullOrWhiteSpace(employeeId) &&
            !string.Equals(employeeId, "0", StringComparison.OrdinalIgnoreCase);
        var employees = organizationEmployees.ToList();

        var employeeIdFound = hasEmployeeId && employees.Any(employee =>
            employee.EmployeeUserId > 0 &&
            string.Equals(employee.EmployeeUserId.ToString(), employeeId, StringComparison.OrdinalIgnoreCase));

        return !hasEmployeeId || !employeeIdFound;
    }

    private async Task<int> CountFilteredUsersAsync(ReportingFilterDto filter, bool includeDrafts, CancellationToken ct)
    {
        var scope = await _scopeService.GetEffectiveOrganizationScopeAsync(_currentUser.UserId, _currentUser.RoleCode, ct);
        if (scope is { Count: 0 })
            return 0;

        return await _userRepo.CountFilteredReportingUsersAsync(CanViewAllScopedUsers, scope, filter, ct);
    }

    private async Task<IReadOnlyList<UserActivityRowDto>> BuildUserActivityRowsAsync(ReportingFilterDto filter, bool includeDrafts, CancellationToken ct)
    {
        var snapshot = await GetReportingSnapshotAsync(filter, includeDrafts, null, ct);
        var quizById = snapshot.Quizzes.ToDictionary(q => q.QuizId);
        var summaryByUser = snapshot.PassSummary
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var appRows = snapshot.Users.Select(u =>
        {
            var userSummary = summaryByUser.GetValueOrDefault(u.UserId, new List<ReportingAttemptSummary>());
            var userPasses = userSummary.Where(p => p.HasPassed).ToList();
            var activityStatus = GetOverviewStatus(snapshot.Quizzes.Count, userSummary);
            var isFullyCompleted = string.Equals(activityStatus, OverviewStatus.Completed, StringComparison.OrdinalIgnoreCase);
            var passedOrders = userPasses
                .Select(p => quizById.TryGetValue(p.QuizId, out var quiz) ? quiz.DisplayOrder : 0)
                .Where(o => o > 0)
                .OrderBy(o => o)
                .ToList();
            var moduleDetails = userSummary
                .Where(p => quizById.ContainsKey(p.QuizId))
                .Select(p =>
                {
                    var quiz = quizById[p.QuizId];
                    var scoreLabel = p.HasPassed ? "Pass" : p.HasFailed ? "Fail" : "In progress";
                    return new UserActivityModuleDto(
                        quiz.DisplayOrder,
                        $"Module {quiz.DisplayOrder} - {quiz.GetTitle()}",
                        scoreLabel,
                        p.LastCompletedAt
                    );
                })
                .OrderBy(m => m.DisplayOrder)
                .ToList();

            return new UserActivityRowDto(
                u.UserId,
                u.FullName,
                u.Email,
                u.Company ?? string.Empty,
                u.Portfolio ?? string.Empty,
                userPasses.Count,
                isFullyCompleted,
                LatestActivity(userSummary),
                u.IsActive,
                activityStatus,
                passedOrders,
                moduleDetails
            );
        }).ToList();

        var directoryRows = await BuildDirectoryOnlyUserActivityRowsAsync(filter, snapshot.Users, ct);
        var selectedStatuses = GetSelectedOverviewStatuses(filter);
        var hasActivityFilter = HasOverviewActivityFilter(filter);
        var rows = appRows
            .Concat(directoryRows)
            .Where(row => !hasActivityFilter || row.ActivityStatus != OverviewStatus.NotStarted)
            .Where(row => selectedStatuses.Count == 0 || selectedStatuses.Contains(row.ActivityStatus))
            .OrderBy(row => row.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            rows = rows
                .Where(row =>
                    row.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    row.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    row.Company.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    row.Portfolio.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return rows;
    }

    private async Task<IReadOnlyList<UserActivityRowDto>> BuildDirectoryOnlyUserActivityRowsAsync(
        ReportingFilterDto filter,
        IReadOnlyList<User> users,
        CancellationToken ct)
    {
        var organizationIdsOverride = NormalizeIds(filter.Companies);
        if (organizationIdsOverride.Count == 0)
        {
            organizationIdsOverride = users
                .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
                .Select(u => u.OrganizationId!.Value)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        var directoryOnlyLearners = await GetDirectoryOnlyLearnersAsync(
            filter,
            users,
            ct,
            organizationIdsOverride: organizationIdsOverride);
        var rows = directoryOnlyLearners
            .Select(x => new UserActivityRowDto(
                Guid.NewGuid(),
                x.Employee.EmployeeName,
                x.Employee.Email,
                x.Company.CompanyName,
                x.Company.PortfolioName,
                0,
                false,
                null,
                false,
                OverviewStatus.NotStarted,
                Array.Empty<int>(),
                Array.Empty<UserActivityModuleDto>()))
            .ToList();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            rows = rows
                .Where(row =>
                    row.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    row.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return rows;
    }

    private async Task<int> CountDirectoryOnlyNotStartedAsync(ReportingFilterDto filter, IReadOnlyList<User> users, CancellationToken ct)
    {
        var organizationIdsOverride = NormalizeIds(filter.Companies);
        if (organizationIdsOverride.Count == 0)
        {
            organizationIdsOverride = users
                .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
                .Select(u => u.OrganizationId!.Value)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        var directoryOnlyLearners = await GetDirectoryOnlyLearnersAsync(
            filter,
            users,
            ct,
            organizationIdsOverride: organizationIdsOverride);
        return directoryOnlyLearners.Count;
    }

    private async Task<IReadOnlyList<DirectoryOnlyLearnerRow>> GetDirectoryOnlyLearnersAsync(
        ReportingFilterDto filter,
        IReadOnlyList<User> users,
        CancellationToken ct,
        bool excludeWhenLastActiveSelected = true,
        IReadOnlyCollection<int>? organizationIdsOverride = null)
    {
        if (HasModuleOrDateFilter(filter) || (excludeWhenLastActiveSelected && filter.LastActiveDays.HasValue))
            return Array.Empty<DirectoryOnlyLearnerRow>();

        var selectedDomains = NormalizeDomains(filter);
        var availableGroups = await GetAvailableCompaniesAsync(ct);
        var companyLookup = availableGroups
            .SelectMany(g => g.Companies.Select(c => new
            {
                c.OrganizationId,
                c.CompanyName,
                g.PortfolioName
            }))
            .GroupBy(c => c.OrganizationId)
            .Select(g => g.First())
            .ToDictionary(c => c.OrganizationId);

        var selectedCompanyIds = (organizationIdsOverride ?? NormalizeIds(filter.Companies))
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (selectedCompanyIds.Count == 0 && !string.IsNullOrWhiteSpace(filter.Portfolio))
        {
            var portfolio = filter.Portfolio.Trim();
            selectedCompanyIds = availableGroups
                .Where(g => g.PortfolioName.Equals(portfolio, StringComparison.OrdinalIgnoreCase))
                .SelectMany(g => g.Companies)
                .Select(c => c.OrganizationId)
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        if (selectedCompanyIds.Count == 0)
            return Array.Empty<DirectoryOnlyLearnerRow>();

        var selectedCompanies = selectedCompanyIds
            .Where(companyLookup.ContainsKey)
            .Select(id => new SelectedCompanyReference(
                id,
                companyLookup[id].CompanyName,
                companyLookup[id].PortfolioName))
            .ToList();

        if (selectedCompanies.Count == 0)
            return Array.Empty<DirectoryOnlyLearnerRow>();

        if (!string.IsNullOrWhiteSpace(filter.Portfolio))
        {
            var portfolio = filter.Portfolio.Trim();
            selectedCompanies = selectedCompanies
                .Where(c => c.PortfolioName.Equals(portfolio, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (selectedCompanies.Count == 0)
                return Array.Empty<DirectoryOnlyLearnerRow>();
        }

        var directoryEmployees = await GetCachedAsync(
            BuildPeopleDirectoryCacheKey(selectedCompanies.Select(c => c.CompanyName)),
            () => _peopleDirectory.GetEmployeesByCompaniesAsync(selectedCompanies.Select(c => c.CompanyName).ToList(), ct),
            ct);
        directoryEmployees = directoryEmployees.Where(employee => !IsDisallowedDirectoryEmployee(employee)).ToList();

        var selectedCompaniesByOrganizationId = selectedCompanies
            .GroupBy(c => c.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());
        var selectedCompaniesByName = selectedCompanies
            .GroupBy(c => NormalizeCompanyName(c.CompanyName))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(g => g.Key, g => g.First());

        var activeDirectoryEmployees = directoryEmployees
            .GroupBy(BuildPeopleDirectoryEmployeeKey)
            .Select(g => g.First())
            .Select(employee => new
            {
                Employee = employee,
                Company = ResolveSelectedCompany(employee, selectedCompaniesByOrganizationId, selectedCompaniesByName)
            })
            .Where(x => x.Company is not null)
            .Select(x => new
            {
                x.Employee,
                Company = x.Company!
            })
            .ToList();

        var userKeys = users
            .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
            .SelectMany(u =>
            {
                var organizationId = u.OrganizationId!.Value;
                var keys = new List<string>(2);
                var employeeId = NormalizePositiveEmployeeId(u.EmployeeId);
                if (employeeId.HasValue)
                    keys.Add(BuildEmployeeOrganizationKey(employeeId.Value, organizationId));

                var emailKey = BuildEmailOrganizationKey(u.Email, organizationId);
                if (!string.IsNullOrWhiteSpace(emailKey))
                    keys.Add(emailKey);

                return keys;
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return activeDirectoryEmployees
            .Where(x =>
                !userKeys.Contains(BuildEmployeeOrganizationKey(x.Employee.EmployeeUserId, x.Company.OrganizationId)) &&
                !userKeys.Contains(BuildEmailOrganizationKey(x.Employee.Email, x.Company.OrganizationId)) &&
                DomainMatchesFilter(x.Employee.Email, selectedDomains) &&
                MatchesSearchTerm(x.Employee.EmployeeName, x.Employee.Email, filter.SearchTerm))
            .Select(x => new DirectoryOnlyLearnerRow(x.Employee, x.Company))
            .ToList();
    }

    private static HashSet<string> GetSelectedOverviewStatuses(ReportingFilterDto filter)
    {
        if (filter.Status is not { Length: > 0 })
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return filter.Status
            .Select(NormalizeOverviewStatus)
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeOverviewStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
            ? string.Empty
            : status.Trim();

    private static HashSet<string> NormalizeDomains(ReportingFilterDto filter)
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (filter.Domains is { Length: > 0 })
        {
            foreach (var domain in filter.Domains)
            {
                var normalized = NormalizeDomain(domain);
                if (normalized != null)
                    domains.Add(normalized);
            }
        }
        else
        {
            var normalized = NormalizeDomain(filter.Domain);
            if (normalized != null)
                domains.Add(normalized);
        }

        return domains;
    }

    private static string? NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var normalized = domain.Trim().TrimStart('@').ToLowerInvariant();
        return IsValidDomain(normalized) ? normalized : null;
    }

    private static bool DomainMatchesFilter(string? email, HashSet<string> selectedDomains)
    {
        if (selectedDomains.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return false;

        var domain = email[(email.IndexOf('@') + 1)..].Trim().ToLowerInvariant();
        return selectedDomains.Contains(domain);
    }

    private static bool MatchesSearchTerm(string? fullName, string? email, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return true;

        var term = searchTerm.Trim();
        return (fullName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string GetExportRowStatus(ReportingExportRowDto row) =>
        row.IsFullyCompleted
            ? OverviewStatus.Completed
            : row.LastActiveAt.HasValue
                ? OverviewStatus.InProgress
                : OverviewStatus.NotStarted;

    private static bool MatchesLastActiveDays(DateTimeOffset? date, int lastActiveDays)
    {
        if (!date.HasValue || lastActiveDays <= 0)
            return false;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-lastActiveDays);
        return date.Value >= cutoff;
    }

    private static bool MatchesPortfolioSearchTerm(string? employee, string? company, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return true;

        var term = searchTerm.Trim();
        return (employee?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (company?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool IsValidDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain) || domain.Length > 253 || !domain.Contains('.'))
            return false;

        return domain.Split('.').All(label =>
            label.Length is > 0 and <= 63 &&
            !label.StartsWith('-') &&
            !label.EndsWith('-') &&
            label.All(c => char.IsLetterOrDigit(c) || c == '-'));
    }

    private static bool HasOverviewActivityFilter(ReportingFilterDto filter)
    {
        var hasModule = filter.ModuleOrder.HasValue;
        var hasDateRange = filter.FromDate.HasValue || filter.ToDate.HasValue;
        var hasLastActive = filter.LastActiveDays.HasValue;
        return hasModule || hasDateRange || hasLastActive;
    }

    private static bool HasModuleOrDateFilter(ReportingFilterDto filter) =>
        filter.ModuleOrder.HasValue || filter.FromDate.HasValue || filter.ToDate.HasValue;

    private static string GetOverviewStatus(
        int moduleCount,
        IReadOnlyList<ReportingAttemptSummary> rows)
    {
        if (rows.Count == 0)
            return OverviewStatus.NotStarted;

        return CompletionRuleEvaluator.HasMetCompletionRule(
            moduleCount,
            rows.Count(p => p.HasPassed),
            rows.Where(p => p.HasPassed).Select(p => p.LastCompletedAt))
            ? OverviewStatus.Completed
            : OverviewStatus.InProgress;
    }

    private async Task<IReadOnlyList<User>> GetScopedUsersAsync(ReportingFilterDto filter, bool includeAllRoles, int? take, CancellationToken ct)
    {
        var scope = await _scopeService.GetEffectiveOrganizationScopeAsync(_currentUser.UserId, _currentUser.RoleCode, ct);
        if (scope is { Count: 0 })
            return Array.Empty<User>();

        return await _userRepo.GetFilteredReportingUsersAsync(includeAllRoles || CanViewAllScopedUsers, scope, filter, take, ct);
    }

    private async Task<ReportingSnapshot> GetReportingSnapshotAsync(ReportingFilterDto filter, bool includeDrafts, int? take, CancellationToken ct)
    {
        var cacheKey = await BuildReportingCacheKeyAsync("snapshot", filter, includeDrafts, take, ct);
        return await GetCachedAsync(cacheKey, async () =>
        {
            var users = await GetFilteredUsersAsync(filter, includeDrafts, take, ct);
            var quizzes = await GetCompletionQuizzesAsync(filter, ct);
            var passSummary = await GetAttemptSummaryAsync(quizzes, false, ct);
            var completions = await _completionRepo.GetAllAsync(ct);

            return new ReportingSnapshot(
                users,
                quizzes,
                passSummary,
                completions.ToDictionary(c => c.UserId));
        }, ct);
    }

    private async Task<IReadOnlyList<Guid>> GetFilteredUserIdsAsync(ReportingFilterDto filter, bool includeDrafts, int? take, CancellationToken ct)
    {
        var scope = await _scopeService.GetEffectiveOrganizationScopeAsync(_currentUser.UserId, _currentUser.RoleCode, ct);
        if (scope is { Count: 0 })
            return Array.Empty<Guid>();

        return await _userRepo.GetFilteredReportingUserIdsAsync(CanViewAllScopedUsers, scope, filter, take, ct);
    }

    private async Task<IReadOnlyList<Quiz>> GetCompletionQuizzesAsync(ReportingFilterDto filter, CancellationToken ct)
    {
        var quizzes = await _quizRepo.GetAllPublishedAsync("en", ct);

        if (filter.ModuleOrder.HasValue)
            quizzes = quizzes.Where(q => q.DisplayOrder == filter.ModuleOrder.Value).ToList();

        return quizzes;
    }

    private async Task<IReadOnlyList<ReportingAttemptSummary>> GetAttemptSummaryAsync(
        IReadOnlyList<Quiz> quizzes,
        bool includeDrafts,
        CancellationToken ct)
    {
        var quizById = quizzes.ToDictionary(q => q.QuizId);
        if (quizById.Count == 0)
            return Array.Empty<ReportingAttemptSummary>();

        var attempts = await _attemptRepo.GetAttemptsForReportingAsync(includeDrafts, ct);

        return attempts
            .Where(a => quizById.ContainsKey(a.QuizId))
            .GroupBy(a => new { a.UserId, a.QuizId })
            .Select(g =>
            {
                var quiz = quizById[g.Key.QuizId];
                var evaluatedAttempts = g.Select(a => new
                {
                    Attempt = a,
                    InferredScore = QuizAttemptEvaluator.TryInferScoreFromAnswers(a, quiz)
                }).ToList();

                var passedAttempts = evaluatedAttempts
                    .Where(a => QuizAttemptEvaluator.IsPassedAttempt(a.Attempt, a.InferredScore, quiz.PassingScore))
                    .ToList();
                var failedAttempts = evaluatedAttempts
                    .Where(a => QuizAttemptEvaluator.IsFailedAttempt(a.Attempt, a.InferredScore, quiz.PassingScore))
                    .ToList();
                var scores = passedAttempts
                    .Select(a => a.Attempt.ScorePercent ?? a.InferredScore?.ScorePercent)
                    .Where(s => s.HasValue)
                    .Select(s => s!.Value)
                    .ToList();
                var lastCompletedAt = passedAttempts.Count > 0
                    ? passedAttempts.Max(a => a.Attempt.CompletedAt ?? a.Attempt.StartedAt)
                    : failedAttempts.Count > 0
                        ? failedAttempts.Max(a => a.Attempt.CompletedAt ?? a.Attempt.StartedAt)
                        : (DateTimeOffset?)null;

                return new ReportingAttemptSummary(
                    g.Key.UserId,
                    g.Key.QuizId,
                    passedAttempts.Count > 0,
                    failedAttempts.Count > 0,
                    evaluatedAttempts.Count,
                    scores.Count > 0 ? scores.Max() : null,
                    evaluatedAttempts.Max(a => a.Attempt.CompletedAt ?? a.Attempt.StartedAt),
                    lastCompletedAt
                );
            })
            .ToList();
    }

    private async Task<string> BuildReportingCacheKeyAsync(string name, ReportingFilterDto filter, bool includeDrafts, int? take, CancellationToken ct)
    {
        var scope = await _scopeService.GetEffectiveOrganizationScopeAsync(_currentUser.UserId, _currentUser.RoleCode, ct);
        var scopeKey = scope is null
            ? "all"
            : scope.Count == 0
                ? "none"
                : string.Join(",", scope.OrderBy(id => id));
        var filterKey = JsonSerializer.Serialize(filter);
        var takeKey = take.HasValue ? take.Value.ToString() : "all";
        return $"reporting:{name}:{_currentUser.RoleCode}:{scopeKey}:{includeDrafts}:{takeKey}:{filterKey}";
    }

    private async Task<T> GetCachedAsync<T>(string cacheKey, Func<Task<T>> factory, CancellationToken ct)
    {
        if (_cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
            return cached;

        var value = await factory();
        _cache.Set(cacheKey, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = GetCacheExpiration(),
            SlidingExpiration = TimeSpan.FromHours(6)
        });

        return value;
    }

    private static DateTimeOffset GetCacheExpiration()
    {
        return DateTimeOffset.UtcNow.Date.AddDays(1);
    }

    private static int GetActivityStatusSortOrder(string? status) =>
        string.Equals(status, OverviewStatus.Completed, StringComparison.OrdinalIgnoreCase) ? 0 :
        string.Equals(status, OverviewStatus.InProgress, StringComparison.OrdinalIgnoreCase) ? 1 :
        2;

    private static DateTimeOffset? LatestActivity(IEnumerable<ReportingAttemptSummary> summaries) =>
        summaries
            .Select(s => s.LastAttemptAt)
            .Where(d => d.HasValue)
            .DefaultIfEmpty()
            .Max();

    private static string BuildPeopleDirectoryCacheKey(IEnumerable<string> companyNames) =>
        "people-directory:" + string.Join("|", companyNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

    private static bool IsDisallowedDirectoryEmployee(PeopleDirectoryEmployeeDto employee) =>
        string.Equals(
            employee.ManagerUserPrincipalName?.Trim(),
            "Contractor - ourVolaris Disallowed",
            StringComparison.OrdinalIgnoreCase);

    private sealed record ReportingSnapshot(
        IReadOnlyList<User> Users,
        IReadOnlyList<Quiz> Quizzes,
        IReadOnlyList<ReportingAttemptSummary> PassSummary,
        IReadOnlyDictionary<Guid, CompletionRecord> CompletionsByUserId);

    private static string BuildPeopleDirectoryEmployeeKey(PeopleDirectoryEmployeeDto employee)
    {
        if (employee.EmployeeUserId > 0)
            return $"employee-id:{employee.EmployeeUserId}";

        var email = NormalizeEmail(employee.Email);
        if (!string.IsNullOrWhiteSpace(email))
            return $"email:{email}";

        return $"name:{NormalizeCompanyName(employee.EmployeeName)}|vbu:{NormalizeCompanyName(employee.Vbu)}";
    }

    private static bool ExistsInDirectorySelection(User user, HashSet<string> activeDirectoryEmployeeKeys, HashSet<string> activeDirectoryEmailKeys)
    {
        if (!user.OrganizationId.HasValue || user.OrganizationId.Value <= 0)
            return false;

        var employeeId = NormalizePositiveEmployeeId(user.EmployeeId);
        if (employeeId.HasValue && activeDirectoryEmployeeKeys.Contains(BuildEmployeeOrganizationKey(employeeId.Value, user.OrganizationId.Value)))
            return true;

        var emailKey = BuildEmailOrganizationKey(user.Email, user.OrganizationId.Value);
        return !string.IsNullOrWhiteSpace(emailKey) && activeDirectoryEmailKeys.Contains(emailKey);
    }

    private static bool ExistsInCurrentSelection(PeopleDirectoryEmployeeDto employee, int organizationId, HashSet<string> currentSelectionEmployeeKeys, HashSet<string> currentSelectionEmailKeys)
    {
        if (employee.EmployeeUserId > 0 && currentSelectionEmployeeKeys.Contains(BuildEmployeeOrganizationKey(employee.EmployeeUserId, organizationId)))
            return true;

        var emailKey = BuildEmailOrganizationKey(employee.Email, organizationId);
        return !string.IsNullOrWhiteSpace(emailKey) && currentSelectionEmailKeys.Contains(emailKey);
    }

    private static int? NormalizePositiveEmployeeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value.Trim(), out var parsed) && parsed > 0
            ? parsed
            : null;
    }

    private static string NormalizeEmployeeId(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static string BuildEmployeeOrganizationKey(int employeeId, int organizationId) =>
        $"{employeeId}|{organizationId}";

    private static string BuildEmailOrganizationKey(string? email, int organizationId)
    {
        var normalizedEmail = NormalizeEmail(email);
        return string.IsNullOrWhiteSpace(normalizedEmail)
            ? string.Empty
            : $"{normalizedEmail}|{organizationId}";
    }

    private static SelectedCompanyReference? ResolveSelectedCompany(
        PeopleDirectoryEmployeeDto employee,
        IReadOnlyDictionary<int, SelectedCompanyReference> selectedCompaniesByOrganizationId,
        IReadOnlyDictionary<string, SelectedCompanyReference> selectedCompaniesByName)
    {
        if (employee.OrganizationId.HasValue &&
            selectedCompaniesByOrganizationId.TryGetValue(employee.OrganizationId.Value, out var organizationMatch))
            return organizationMatch;

        var companyKey = NormalizeCompanyName(employee.Company);
        if (!string.IsNullOrWhiteSpace(companyKey) && selectedCompaniesByName.TryGetValue(companyKey, out var companyMatch))
            return companyMatch;

        var vbuKey = NormalizeCompanyName(employee.Vbu);
        if (!string.IsNullOrWhiteSpace(vbuKey) && selectedCompaniesByName.TryGetValue(vbuKey, out var vbuMatch))
            return vbuMatch;

        if (selectedCompaniesByName.Count == 1)
            return selectedCompaniesByName.Values.First();

        return null;
    }

    private static string NormalizeCompanyName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private static string NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private sealed record ReportingAttemptSummary(
        Guid UserId,
        Guid QuizId,
        bool HasPassed,
        bool HasFailed,
        int TotalAttempts,
        decimal? BestScore,
        DateTimeOffset? LastAttemptAt,
        DateTimeOffset? LastCompletedAt);

    private sealed record SelectedCompanyReference(
        int OrganizationId,
        string CompanyName,
        string PortfolioName);

    private sealed record DirectoryOnlyLearnerRow(
        PeopleDirectoryEmployeeDto Employee,
        SelectedCompanyReference Company);

    private static IReadOnlyList<int> NormalizeIds(IReadOnlyList<int>? ids) =>
        (IReadOnlyList<int>?)ids?
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList()
        ?? Array.Empty<int>();

    private static class OverviewStatus
    {
        public const string Completed = "Completed";
        public const string InProgress = "In Progress";
        public const string NotStarted = "Not Started";
    }

}

// ─── User Admin Service ───────────────────────────────────────────────────────

public class UserAdminService : IUserAdminService
{
    private static readonly HashSet<string> ValidRoles = new() { "Learner", "ReportingAdmin", "OrgAdmin", "PortfolioAdmin", "GlobalAdmin" };

    private readonly IUserRepository _userRepo;
    private readonly IUserMergeRepository _mergeRepo;
    private readonly IReportingAccessRepository _accessRepo;
    private readonly IAuditLogRepository _auditLog;
    private readonly IMemoryCache _cache;
    private readonly IAccessGroupService? _accessGroups;

    public UserAdminService(IUserRepository userRepo, IUserMergeRepository mergeRepo, IReportingAccessRepository accessRepo, IAuditLogRepository auditLog, IMemoryCache cache, IAccessGroupService? accessGroups = null)
    {
        _userRepo = userRepo;
        _mergeRepo = mergeRepo;
        _accessRepo = accessRepo;
        _auditLog = auditLog;
        _cache = cache;
        _accessGroups = accessGroups;
    }

    public Task<int> GetUsersCountAsync(string? searchTerm, CancellationToken ct = default) =>
        _userRepo.CountActiveAsync(searchTerm, ct);

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(string? searchTerm, int? take = null, CancellationToken ct = default)
    {
        var users = await _userRepo.GetActiveAsync(searchTerm, take, ct);
        var results = new List<AdminUserDto>(users.Count);

        foreach (var user in users)
            results.Add(await ToDtoAsync(user, ct));

        return results;
    }

    public async Task<IReadOnlyList<MergeUserCandidateDto>> SearchMergeCandidatesAsync(string? searchTerm, int take = 25, CancellationToken ct = default)
    {
        var users = await _userRepo.SearchActiveByPrefixAsync(searchTerm, take, ct);
        return users
            .OrderBy(u => HasMissingEmployeeId(u) ? 0 : 1)
            .ThenBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(ToMergeCandidate)
            .ToList();
    }

    public async Task<MergeUsersPreviewDto> PreviewMergeUsersAsync(MergeUsersRequestDto request, Guid actorUserId, CancellationToken ct = default)
    {
        var normalized = NormalizeMergeRequest(request);
        var warnings = new List<string>();

        if (normalized.SourceUserIds.Count == 0)
            return new MergeUsersPreviewDto(Array.Empty<MergeUserCandidateDto>(), null, new MergeUsersStatisticsDto(0, 0, false, false), new[] { "Select at least one source user." }, false);

        if (normalized.TargetUserId == Guid.Empty)
            return new MergeUsersPreviewDto(Array.Empty<MergeUserCandidateDto>(), null, new MergeUsersStatisticsDto(0, 0, false, false), new[] { "Select one target user." }, false);

        if (normalized.SourceUserIds.Contains(normalized.TargetUserId))
            warnings.Add("The target user cannot also be selected as a source user.");

        if (normalized.SourceUserIds.Contains(actorUserId))
            warnings.Add("You cannot merge and deactivate your own signed-in user record.");

        var allIds = normalized.SourceUserIds.Append(normalized.TargetUserId).Distinct().ToArray();
        var users = await _userRepo.GetByIdsAsync(allIds, ct);
        var userLookup = users.ToDictionary(u => u.UserId);

        var sources = normalized.SourceUserIds
            .Where(userLookup.ContainsKey)
            .Select(id => userLookup[id])
            .ToList();
        var missingSourceCount = normalized.SourceUserIds.Count - sources.Count;
        if (missingSourceCount > 0)
            warnings.Add("One or more selected source users no longer exist.");

        if (!userLookup.TryGetValue(normalized.TargetUserId, out var target))
            warnings.Add("The selected target user no longer exists.");

        if (target is not null && !target.IsActive)
            warnings.Add("The target user must be active.");

        if (sources.Any(u => !u.IsActive))
            warnings.Add("All source users must be active.");

        var sourceEmployeeIds = sources
            .Select(GetNormalizedEmployeeId)
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var source in sources)
        {
            if (!HasMissingEmployeeId(source))
                warnings.Add($"{source.FullName} has a non-zero Employee ID. Source users are expected to be duplicate records with Employee ID 0.");

            if (target is not null)
            {
                if (!string.Equals(source.FullName, target.FullName, StringComparison.OrdinalIgnoreCase))
                    warnings.Add($"{source.FullName} does not exactly match target name {target.FullName}.");
                if (source.OrganizationId != target.OrganizationId)
                    warnings.Add($"{source.FullName} has a different organization than the target user.");
                if (source.PortfolioId != target.PortfolioId)
                    warnings.Add($"{source.FullName} has a different portfolio than the target user.");
            }
        }

        if (target is not null && HasMissingEmployeeId(target) && sourceEmployeeIds.Count > 1)
        {
            warnings.Add("The selected target user has Employee ID 0 and the source users have multiple different non-zero Employee IDs. Select a single canonical employee record before merging.");
        }

        var canMerge = warnings.All(w =>
            !string.Equals(w, "The selected target user no longer exists.", StringComparison.Ordinal) &&
            !string.Equals(w, "The target user must be active.", StringComparison.Ordinal) &&
            !string.Equals(w, "All source users must be active.", StringComparison.Ordinal) &&
            !string.Equals(w, "The target user cannot also be selected as a source user.", StringComparison.Ordinal) &&
            !string.Equals(w, "You cannot merge and deactivate your own signed-in user record.", StringComparison.Ordinal) &&
            !string.Equals(w, "One or more selected source users no longer exist.", StringComparison.Ordinal) &&
            !string.Equals(w, "The selected target user has Employee ID 0 and the source users have multiple different non-zero Employee IDs. Select a single canonical employee record before merging.", StringComparison.Ordinal));

        var stats = canMerge && target is not null
            ? await _mergeRepo.GetMergeStatisticsAsync(sources.Select(u => u.UserId).ToArray(), target.UserId, ct)
            : new MergeUsersStatisticsDto(0, 0, false, false);

        return new MergeUsersPreviewDto(
            sources.Select(ToMergeCandidate).ToList(),
            target is null ? null : ToMergeCandidate(target),
            stats,
            warnings.Distinct().ToList(),
            canMerge);
    }

    public async Task<MergeUsersResultDto> ExecuteMergeUsersAsync(MergeUsersRequestDto request, Guid actorUserId, CancellationToken ct = default)
    {
        var preview = await PreviewMergeUsersAsync(request, actorUserId, ct);
        if (!preview.CanMerge || preview.Target is null)
            throw new ArgumentException(preview.Warnings.FirstOrDefault() ?? "The selected users cannot be merged.");

        var result = await _mergeRepo.ExecuteMergeAsync(
            preview.Sources.Select(s => s.UserId).ToArray(),
            preview.Target.UserId,
            ct);

        var auditData = JsonSerializer.Serialize(new
        {
            targetUserId = result.TargetUserId,
            sourceUserIds = preview.Sources.Select(s => s.UserId).ToArray(),
            result.QuizAttemptsMoved,
            result.SourceUsersDeactivated,
            result.SourceCompletionRecordsMerged,
            result.TargetHasCompletionRecord
        });

        await _auditLog.WriteAsync(
            "admin.user_merge",
            "success",
            actorUserId,
            "user",
            result.TargetUserId,
            null,
            auditData,
            "admin.roles",
            additionalDataJson: auditData,
            ct: ct);

        if (_cache is MemoryCache memoryCache)
            memoryCache.Compact(1.0);

        return result;
    }

    public async Task<AdminUserDto> AddUserAsync(AddUserRequestDto request, Guid actorUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName)) throw new ArgumentException("First name is required.");
        if (string.IsNullOrWhiteSpace(request.LastName)) throw new ArgumentException("Last name is required.");
        if (string.IsNullOrWhiteSpace(request.Email)) throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(request.EmployeeId)) throw new ArgumentException("Employee ID is required.");
        if (!ValidRoles.Contains(request.RoleCode)) throw new ArgumentException($"Invalid role: {request.RoleCode}");

        var existing = await _userRepo.GetByEmailAsync(request.Email, ct);
        if (existing != null) throw new InvalidOperationException("A user with this email already exists.");

        if (!request.OrganizationId.HasValue || request.OrganizationId.Value <= 0)
            throw new ArgumentException("Company is required.");

        var selectedOrganization = await ResolveOrganizationCompanyAsync(request.OrganizationId.Value, ct)
            ?? throw new ArgumentException("Selected company was not found.");

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Login = request.Email.ToLowerInvariant(),
            IdpSubjectIdentifier = string.Empty,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            EmployeeId = request.EmployeeId.Trim(),
            Company = selectedOrganization.OrganizationName,
            OrganizationId = selectedOrganization.OrganizationId,
            Portfolio = selectedOrganization.PortfolioName ?? request.Portfolio?.Trim() ?? string.Empty,
            PortfolioId = selectedOrganization.PortfolioId ?? request.PortfolioId,
            RoleCode = request.RoleCode,
            AccountCreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        await _userRepo.CreateAsync(user, ct);
        await _auditLog.WriteAsync("admin.user_add", "success", actorUserId, "user",
            user.UserId, null, request.RoleCode, "admin.roles", ct: ct);

        return ToDto(user);
    }

    public async Task<AdminUserDto> UpdateRoleAsync(Guid targetUserId, UpdateRoleRequestDto request, Guid actorUserId, CancellationToken ct = default)
    {
        if (!ValidRoles.Contains(request.RoleCode)) throw new ArgumentException($"Invalid role: {request.RoleCode}");

        var user = await _userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException($"User {targetUserId} not found.");

        var before = user.RoleCode;
        user.RoleCode = request.RoleCode;
        await _userRepo.UpdateAsync(user, ct);

        await _auditLog.WriteAsync("admin.role_change", "success", actorUserId, "user",
            targetUserId, before, request.RoleCode, "admin.roles", ct: ct);

        return ToDto(user);
    }

    public async Task<UserAccessScopeDto> GetAccessAsync(Guid targetUserId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException($"User {targetUserId} not found.");

        return await BuildAccessDtoAsync(user, ct);
    }

    public async Task<UserAccessScopeDto> UpdateAccessAsync(Guid targetUserId, UpdateUserAccessRequestDto request, Guid actorUserId, CancellationToken ct = default)
    {
        if (!ValidRoles.Contains(request.RoleCode))
            throw new ArgumentException($"Invalid role: {request.RoleCode}");

        var user = await _userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException($"User {targetUserId} not found.");

        var organizationIds = NormalizeIds(request.OrganizationIds);
        var portfolioIds = NormalizeIds(request.PortfolioIds);

        if (request.RoleCode == "OrgAdmin" && organizationIds.Count == 0)
            throw new ArgumentException("At least one VBU must be assigned to an Org Admin.");
        if (request.RoleCode == "PortfolioAdmin" && portfolioIds.Count == 0)
            throw new ArgumentException("At least one portfolio must be assigned to a Portfolio Admin.");

        if (request.RoleCode != "OrgAdmin")
            organizationIds = Array.Empty<int>();
        if (request.RoleCode != "PortfolioAdmin")
            portfolioIds = Array.Empty<int>();

        var before = await BuildAccessDtoAsync(user, ct);
        var beforeJson = JsonSerializer.Serialize(before);

        user.RoleCode = request.RoleCode;
        await _userRepo.UpdateAsync(user, ct);
        await _accessRepo.SetOrgAssignmentsAsync(targetUserId, organizationIds, actorUserId, ct);
        await _accessRepo.SetPortfolioAssignmentsAsync(targetUserId, portfolioIds, actorUserId, ct);

        var after = await BuildAccessDtoAsync(user, ct);
        var afterJson = JsonSerializer.Serialize(after);

        await _auditLog.WriteAsync("admin.user_access_change", "success", actorUserId, "user",
            targetUserId, beforeJson, afterJson, "admin.roles", ct: ct);

        return after;
    }

    public async Task<IReadOnlyList<AccessOptionDto>> SearchOrganizationsAsync(string? searchTerm, CancellationToken ct = default)
    {
        var organizations = await _accessRepo.SearchOrganizationsAsync(searchTerm, ct: ct);
        return organizations.Select(o => new AccessOptionDto(o.OrganizationId, $"{o.OrganizationName} ({o.OrganizationId})")).ToList();
    }

    public async Task<IReadOnlyList<OrganizationCompanyOptionDto>> SearchOrganizationCompaniesAsync(string? searchTerm, CancellationToken ct = default)
    {
        var rows = await _accessRepo.GetVbuMasterListAsync(ct);
        var portfolios = await _accessRepo.SearchPortfoliosAsync(null, int.MaxValue, ct);
        var portfolioIds = portfolios.Select(p => p.PortfolioId).ToHashSet();
        var portfolioNames = portfolios.ToDictionary(p => p.PortfolioId, p => p.PortfolioName);
        var rowsByOrganizationId = rows
            .Where(v => v.OrganizationId > 0)
            .GroupBy(v => v.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());

        var query = rowsByOrganizationId.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(v =>
                v.OrganizationName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                v.OrganizationId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var options = query
            .OrderBy(v => v.OrganizationName)
            .Select(v =>
            {
                var isPortfolio = portfolioIds.Contains(v.OrganizationId);
                var parentPortfolioId = isPortfolio
                    ? v.OrganizationId
                    : ResolveParentPortfolioId(v.OrganizationId, rowsByOrganizationId, portfolioIds);
                return new OrganizationCompanyOptionDto(
                    v.OrganizationId,
                    isPortfolio && portfolioNames.TryGetValue(v.OrganizationId, out var ownPortfolioName) ? ownPortfolioName : v.OrganizationName,
                    parentPortfolioId,
                    parentPortfolioId.HasValue && portfolioNames.TryGetValue(parentPortfolioId.Value, out var portfolioName)
                        ? portfolioName
                        : null);
            })
            .ToList();

        var existingOptionIds = options.Select(o => o.OrganizationId).ToHashSet();
        var portfolioOptions = portfolios
            .Where(p => !existingOptionIds.Contains(p.PortfolioId))
            .Where(p => string.IsNullOrWhiteSpace(searchTerm) ||
                p.PortfolioName.Contains(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase) ||
                p.PortfolioId.ToString().Contains(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(p => new OrganizationCompanyOptionDto(
                p.PortfolioId,
                p.PortfolioName,
                p.PortfolioId,
                p.PortfolioName));

        return options
            .Concat(portfolioOptions)
            .OrderBy(o => o.OrganizationName)
            .ThenBy(o => o.OrganizationId)
            .ToList();
    }

    public async Task<IReadOnlyList<AccessOptionDto>> SearchPortfoliosAsync(string? searchTerm, CancellationToken ct = default)
    {
        var portfolios = await _accessRepo.SearchPortfoliosAsync(searchTerm, ct: ct);
        return portfolios.Select(p => new AccessOptionDto(p.PortfolioId, $"{p.PortfolioName} ({p.PortfolioId})")).ToList();
    }

    public async Task<AdminUserDto> SetActiveStatusAsync(Guid targetUserId, bool isActive, Guid actorUserId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException($"User {targetUserId} not found.");

        var before = user.IsActive.ToString();
        user.IsActive = isActive;
        await _userRepo.UpdateAsync(user, ct);

        var action = isActive ? "admin.user_restore" : "admin.user_deactivate";
        await _auditLog.WriteAsync(action, "success", actorUserId, "user",
            targetUserId, before, isActive.ToString(), "admin.roles", ct: ct);

        return ToDto(user);
    }

    private async Task<UserAccessScopeDto> BuildAccessDtoAsync(User user, CancellationToken ct)
    {
        var orgIds = (await _accessRepo.GetOrgAssignmentsAsync(user.UserId, ct))
            .Select(a => a.OrganizationId)
            .Distinct()
            .ToList();
        var portfolioIds = (await _accessRepo.GetPortfolioAssignmentsAsync(user.UserId, ct))
            .Select(a => a.PortfolioId)
            .Distinct()
            .ToList();

        var organizations = await _accessRepo.GetOrganizationsByIdsAsync(orgIds, ct);
        var portfolios = await _accessRepo.GetPortfoliosByIdsAsync(portfolioIds, ct);

        return new UserAccessScopeDto(
            user.UserId,
            user.RoleCode,
            organizations.Select(o => new AccessOptionDto(o.OrganizationId, $"{o.OrganizationName} ({o.OrganizationId})")).ToList(),
            portfolios.Select(p => new AccessOptionDto(p.PortfolioId, $"{p.PortfolioName} ({p.PortfolioId})")).ToList()
        );
    }

    private static IReadOnlyList<int> NormalizeIds(IReadOnlyList<int>? ids) =>
        (IReadOnlyList<int>?)ids?
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList()
        ?? Array.Empty<int>();

    private async Task<OrganizationCompanyOptionDto?> ResolveOrganizationCompanyAsync(int organizationId, CancellationToken ct)
    {
        var companies = await SearchOrganizationCompaniesAsync(organizationId.ToString(), ct);
        return companies.FirstOrDefault(c => c.OrganizationId == organizationId);
    }

    private static int? ResolveParentPortfolioId(
        int organizationId,
        IReadOnlyDictionary<int, VbuMasterListRecord> rowsByOrganizationId,
        HashSet<int> portfolioIds)
    {
        var seen = new HashSet<int>();
        var currentId = organizationId;

        while (rowsByOrganizationId.TryGetValue(currentId, out var row))
        {
            if (!seen.Add(currentId))
                return null;

            var parentId = row.ReportToId;
            if (parentId <= 0)
                return null;
            if (portfolioIds.Contains(parentId))
                return parentId;

            currentId = parentId;
        }

        return null;
    }

    private async Task<AdminUserDto> ToDtoAsync(User u, CancellationToken ct)
    {
        var scopeSummary = await BuildScopeSummaryAsync(u, ct);
        return new AdminUserDto(
            u.UserId,
            u.FullName,
            u.Email,
            u.EmployeeId,
            u.Company ?? string.Empty,
            u.Portfolio ?? string.Empty,
            scopeSummary,
            u.OrganizationId,
            u.PortfolioId,
            u.RoleCode,
            RoleDisplayName(u.RoleCode),
            u.LastLoginAt,
            u.IsActive
        );
    }

    private async Task<string> BuildScopeSummaryAsync(User user, CancellationToken ct)
    {
        if (user.RoleCode == "GlobalAdmin")
            return "All companies / portfolios";

        if (_accessGroups is not null && user.RoleCode is "ReportingAdmin" or "OrgAdmin" or "PortfolioAdmin")
        {
            var groups = await _accessGroups.GetUserGroupAssignmentsAsync(user.UserId, ct);
            if (groups.Count > 0)
                return string.Join(", ", groups.Select(g => g.Name));
        }

        var access = await BuildAccessDtoAsync(user, ct);
        if (access.AssignedOrganizations.Count > 0)
            return string.Join(", ", access.AssignedOrganizations.Select(a => a.Name));
        if (access.AssignedPortfolios.Count > 0)
            return string.Join(", ", access.AssignedPortfolios.Select(a => a.Name));
        return "-";
    }

    private static AdminUserDto ToDto(User u) => new(
        u.UserId,
        u.FullName,
        u.Email,
        u.EmployeeId,
        u.Company ?? string.Empty,
        u.Portfolio ?? string.Empty,
        "-",
        u.OrganizationId,
        u.PortfolioId,
        u.RoleCode,
        RoleDisplayName(u.RoleCode),
        u.LastLoginAt,
        u.IsActive
    );

    private static MergeUserCandidateDto ToMergeCandidate(User u) => new(
        u.UserId,
        u.FullName,
        u.Email,
        u.EmployeeId,
        u.Company ?? string.Empty,
        u.Portfolio ?? string.Empty,
        u.OrganizationId,
        u.PortfolioId,
        u.RoleCode,
        RoleDisplayName(u.RoleCode),
        u.IsActive
    );

    private static bool HasMissingEmployeeId(User user) =>
        string.IsNullOrWhiteSpace(user.EmployeeId) || string.Equals(user.EmployeeId.Trim(), "0", StringComparison.OrdinalIgnoreCase);

    private static string? GetNormalizedEmployeeId(User user) =>
        HasMissingEmployeeId(user) ? null : user.EmployeeId.Trim();

    private static (IReadOnlyList<Guid> SourceUserIds, Guid TargetUserId) NormalizeMergeRequest(MergeUsersRequestDto request)
    {
        var sourceIds = (request.SourceUserIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        return (sourceIds, request.TargetUserId);
    }

    private static string RoleDisplayName(string code) => code switch
    {
        "GlobalAdmin" => "Super Admin",
        "ReportingAdmin" => "Reporting Admin",
        "PortfolioAdmin" => "Reporting Admin",
        "OrgAdmin" => "Reporting Admin",
        _ => "Learner"
    };
}
