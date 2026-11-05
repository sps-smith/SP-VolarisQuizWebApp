using Microsoft.EntityFrameworkCore;
using System.Data;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Application.Services;
using Volaris101.Domain.Entities;
using Volaris101.Infrastructure.Data;

namespace Volaris101.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly Volaris101DbContext _db;
    public UserRepository(Volaris101DbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public Task<User?> GetByLoginAsync(string login, CancellationToken ct = default) =>
        _db.Users
            .Where(u => u.Login == login.ToLowerInvariant())
            .OrderByDescending(u => u.IsActive)
            .ThenByDescending(u => u.LastLoginAt)
            .ThenByDescending(u => u.AccountCreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users
            .Where(u => u.Email == email.ToLowerInvariant())
            .OrderByDescending(u => u.IsActive)
            .ThenByDescending(u => u.LastLoginAt)
            .ThenByDescending(u => u.AccountCreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<User?> GetByEmployeeIdAsync(string employeeId, CancellationToken ct = default) =>
        _db.Users
            .Where(u => u.EmployeeId == employeeId)
            .OrderByDescending(u => u.IsActive)
            .ThenByDescending(u => u.LastLoginAt)
            .ThenByDescending(u => u.AccountCreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return Array.Empty<User>();

        return await _db.Users
            .Where(u => userIds.Contains(u.UserId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<User>> GetAllActiveAsync(CancellationToken ct = default) =>
        await _db.Users.Where(u => u.IsActive).OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync(ct);

    public async Task<IReadOnlyList<User>> SearchActiveByPrefixAsync(string? searchTerm, int? take = null, CancellationToken ct = default)
    {
        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).StartsWith(term) ||
                u.Email.StartsWith(term));
        }

        query = query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName);
        if (take.HasValue && take.Value > 0)
            query = query.Take(take.Value);

        return await query.ToListAsync(ct);
    }

    public async Task<int> CountActiveAsync(string? searchTerm = null, CancellationToken ct = default)
    {
        var query = BuildRolesAndUsersQuery(searchTerm);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<User>> GetActiveAsync(string? searchTerm = null, int? take = null, CancellationToken ct = default)
    {
        var query = BuildRolesAndUsersQuery(searchTerm);
        query = query.OrderByDescending(u => u.IsActive).ThenBy(u => u.LastName).ThenBy(u => u.FirstName);
        if (take.HasValue && take.Value > 0)
            query = query.Take(take.Value);

        return await query.ToListAsync(ct);
    }

    private IQueryable<User> BuildRolesAndUsersQuery(string? searchTerm)
    {
        var query = _db.Users.AsNoTracking();

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => u.RoleCode == "ReportingAdmin" || u.RoleCode == "OrgAdmin" || u.RoleCode == "PortfolioAdmin" || u.RoleCode == "GlobalAdmin");
        }
        else
        {
            var term = searchTerm.Trim();
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).Contains(term) ||
                u.Email.Contains(term));
        }

        return query;
    }

    private IQueryable<User> BuildReportingUserQuery(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        ReportingFilterDto filter,
        Guid? includeUserId = null)
    {
        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive);

        if (!isGlobalAdmin)
            query = query.Where(u => u.RoleCode == "Learner" || (includeUserId.HasValue && u.UserId == includeUserId.Value));

        if (scopeOrganizationIds is not null)
        {
            if (scopeOrganizationIds.Count == 0)
                return query.Where(_ => false);

            query = query.Where(u => u.OrganizationId.HasValue && scopeOrganizationIds.Contains(u.OrganizationId.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).Contains(term) ||
                u.Email.Contains(term));
        }

        var selectedDomains = NormalizeDomains(filter);
        if (selectedDomains.Count > 0)
        {
            query = query.Where(u => u.Email.Contains("@") &&
                selectedDomains.Contains(u.Email.Substring(u.Email.IndexOf("@") + 1).ToLower()));
        }

        var selectedCompanies = NormalizeIds(filter.Companies);
        if (selectedCompanies.Count > 0)
        {
            query = query.Where(u => u.OrganizationId.HasValue && selectedCompanies.Contains(u.OrganizationId.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Portfolio))
        {
            var portfolio = filter.Portfolio.Trim().ToLower();
            query = query.Where(u => u.Portfolio != null && u.Portfolio.ToLower() == portfolio);
        }

        if (filter.FromDate.HasValue && filter.ToDate.HasValue)
        {
            var start = filter.FromDate.Value;
            var end = filter.ToDate.Value.AddDays(1).AddTicks(-1);
            query = query.Where(u => _db.QuizAttempts.Any(a =>
                a.UserId == u.UserId &&
                !a.IsTestData &&
                a.StartedAt <= end &&
                (a.CompletedAt ?? a.StartedAt) >= start));
        }

        return query;
    }

    public async Task<IReadOnlyList<User>> GetFilteredReportingUsersAsync(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        ReportingFilterDto filter,
        int? take = null,
        CancellationToken ct = default,
        Guid? includeUserId = null) =>
        await ApplyTake(
            BuildReportingUserQuery(isGlobalAdmin, scopeOrganizationIds, filter, includeUserId)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName),
            take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetFilteredReportingUserIdsAsync(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        ReportingFilterDto filter,
        int? take = null,
        CancellationToken ct = default) =>
        await ApplyTake(
            BuildReportingUserQuery(isGlobalAdmin, scopeOrganizationIds, filter)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Select(u => u.UserId),
            take)
            .ToListAsync(ct);

    public async Task<int> CountFilteredReportingUsersAsync(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        ReportingFilterDto filter,
        CancellationToken ct = default) =>
        await BuildReportingUserQuery(isGlobalAdmin, scopeOrganizationIds, filter).CountAsync(ct);

    public async Task<IReadOnlyList<string>> GetDistinctReportingDomainsAsync(
        bool isGlobalAdmin,
        IReadOnlyCollection<int>? scopeOrganizationIds,
        CancellationToken ct = default)
    {
        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive);

        if (!isGlobalAdmin)
            query = query.Where(u => u.RoleCode == "Learner");

        if (scopeOrganizationIds is not null)
        {
            if (scopeOrganizationIds.Count == 0)
                return Array.Empty<string>();

            query = query.Where(u => u.OrganizationId.HasValue && scopeOrganizationIds.Contains(u.OrganizationId.Value));
        }

        return await query
            .Where(u => u.Email.Contains("@"))
            .Select(u => u.Email.Substring(u.Email.IndexOf("@") + 1).ToLower())
            .Distinct()
            .OrderBy(d => d)
            .Select(d => "@" + d)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CompanyGroupDto>> GetAvailableReportingCompaniesAsync(
        IReadOnlyCollection<int>? scopeOrganizationIds,
        CancellationToken ct = default)
    {
        var vbuRows = await _db.VbuMasterList.AsNoTracking().ToListAsync(ct);
        var portfolios = await _db.Portfolios.AsNoTracking().ToListAsync(ct);
        return ReportingCompanySourceBuilder.BuildAvailableCompanies(vbuRows, portfolios, scopeOrganizationIds);
    }

    private static IQueryable<T> ApplyTake<T>(IQueryable<T> query, int? take) =>
        take.HasValue && take.Value > 0 ? query.Take(take.Value) : query;

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

    private static IReadOnlyCollection<int> NormalizeIds(IEnumerable<int>? values) =>
        values?
            .Where(v => v > 0)
            .Distinct()
            .ToArray()
        ?? Array.Empty<int>();

    private static string? NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var normalized = domain.Trim().TrimStart('@').ToLowerInvariant();
        return IsValidDomain(normalized) ? normalized : null;
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

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }
}

public class UserMergeRepository : IUserMergeRepository
{
    private readonly Volaris101DbContext _db;

    public UserMergeRepository(Volaris101DbContext db) => _db = db;

    public async Task<MergeUsersStatisticsDto> GetMergeStatisticsAsync(
        IReadOnlyCollection<Guid> sourceUserIds,
        Guid targetUserId,
        CancellationToken ct = default)
    {
        var normalizedSourceIds = sourceUserIds
            .Where(id => id != Guid.Empty && id != targetUserId)
            .Distinct()
            .ToArray();

        if (normalizedSourceIds.Length == 0 || targetUserId == Guid.Empty)
            return new MergeUsersStatisticsDto(0, 0, false, false);

        var quizAttemptsToMove = await _db.QuizAttempts
            .AsNoTracking()
            .CountAsync(a => normalizedSourceIds.Contains(a.UserId), ct);

        var sourceCompletionRecordCount = await _db.CompletionRecords
            .AsNoTracking()
            .CountAsync(c => normalizedSourceIds.Contains(c.UserId), ct);

        var targetHasCompletionRecord = await _db.CompletionRecords
            .AsNoTracking()
            .AnyAsync(c => c.UserId == targetUserId, ct);

        var evaluationUserIds = normalizedSourceIds
            .Append(targetUserId)
            .Distinct()
            .ToArray();

        var targetWillBeComplete = targetHasCompletionRecord ||
            sourceCompletionRecordCount > 0 ||
            await HasMetCompletionRuleAsync(evaluationUserIds, ct);

        return new MergeUsersStatisticsDto(
            quizAttemptsToMove,
            sourceCompletionRecordCount,
            targetHasCompletionRecord,
            targetWillBeComplete);
    }

    public async Task<MergeUsersResultDto> ExecuteMergeAsync(
        IReadOnlyCollection<Guid> sourceUserIds,
        Guid targetUserId,
        CancellationToken ct = default)
    {
        var normalizedSourceIds = sourceUserIds
            .Where(id => id != Guid.Empty && id != targetUserId)
            .Distinct()
            .ToArray();

        if (normalizedSourceIds.Length == 0)
            throw new ArgumentException("At least one source user must be supplied.");
        if (targetUserId == Guid.Empty)
            throw new ArgumentException("A target user must be supplied.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var targetUser = await _db.Users
                .FirstOrDefaultAsync(u => u.UserId == targetUserId, ct)
                ?? throw new KeyNotFoundException($"Target user {targetUserId} was not found.");

            var sourceUsers = await _db.Users
                .Where(u => normalizedSourceIds.Contains(u.UserId))
                .ToListAsync(ct);

            var sourceEmployeeIds = sourceUsers
                .Select(GetNormalizedEmployeeId)
                .Where(id => id is not null)
                .Select(id => id!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (HasMissingEmployeeId(targetUser))
            {
                if (sourceEmployeeIds.Length > 1)
                {
                    throw new InvalidOperationException("Cannot merge into a target user with Employee ID 0 when the source users have multiple different non-zero Employee IDs.");
                }

                if (sourceEmployeeIds.Length == 1)
                    targetUser.EmployeeId = sourceEmployeeIds[0];
            }

            var quizAttemptsMoved = await _db.QuizAttempts
                .CountAsync(a => normalizedSourceIds.Contains(a.UserId), ct);

            var sourceCompletionRecords = await _db.CompletionRecords
                .Where(c => normalizedSourceIds.Contains(c.UserId))
                .ToListAsync(ct);

            var sourceCompletionRecordsMerged = sourceCompletionRecords.Count;

            var targetCompletionRecord = await _db.CompletionRecords
                .FirstOrDefaultAsync(c => c.UserId == targetUserId, ct);

            await _db.QuizAttempts
                .Where(a => normalizedSourceIds.Contains(a.UserId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(a => a.UserId, targetUserId), ct);

            var targetShouldBeComplete = targetCompletionRecord is not null ||
                sourceCompletionRecordsMerged > 0 ||
                await HasMetCompletionRuleAsync(new[] { targetUserId }, ct);

            if (targetShouldBeComplete)
            {
                var completionAt = await ResolveCompletionDateAsync(targetUserId, targetCompletionRecord, sourceCompletionRecords, ct);

                if (targetCompletionRecord is null)
                {
                    _db.CompletionRecords.Add(new CompletionRecord
                    {
                        CompletionId = Guid.NewGuid(),
                        UserId = targetUserId,
                        CompletedAt = completionAt,
                        EmailNotificationSentAt = sourceCompletionRecords
                            .Where(c => c.EmailNotificationSentAt.HasValue)
                            .Select(c => c.EmailNotificationSentAt)
                            .Append(null)
                            .Min(),
                        ExportedAt = sourceCompletionRecords
                            .Where(c => c.ExportedAt.HasValue)
                            .Select(c => c.ExportedAt)
                            .Append(null)
                            .Min(),
                        SnapshotFirstName = targetUser.FirstName,
                        SnapshotLastName = targetUser.LastName,
                        SnapshotEmail = targetUser.Email,
                        SnapshotEmployeeId = targetUser.EmployeeId,
                        SnapshotCompany = targetUser.Company ?? string.Empty,
                        SnapshotPortfolio = targetUser.Portfolio ?? string.Empty
                    });
                }
                else
                {
                    targetCompletionRecord.CompletedAt = completionAt;
                    targetCompletionRecord.EmailNotificationSentAt = MergeNullableDates(
                        targetCompletionRecord.EmailNotificationSentAt,
                        sourceCompletionRecords.Select(c => c.EmailNotificationSentAt));
                    targetCompletionRecord.ExportedAt = MergeNullableDates(
                        targetCompletionRecord.ExportedAt,
                        sourceCompletionRecords.Select(c => c.ExportedAt));
                    targetCompletionRecord.SnapshotFirstName = targetUser.FirstName;
                    targetCompletionRecord.SnapshotLastName = targetUser.LastName;
                    targetCompletionRecord.SnapshotEmail = targetUser.Email;
                    targetCompletionRecord.SnapshotEmployeeId = targetUser.EmployeeId;
                    targetCompletionRecord.SnapshotCompany = targetUser.Company ?? string.Empty;
                    targetCompletionRecord.SnapshotPortfolio = targetUser.Portfolio ?? string.Empty;
                }
            }

            if (sourceCompletionRecordsMerged > 0)
                _db.CompletionRecords.RemoveRange(sourceCompletionRecords);

            var sourceUsersDeactivated = await _db.Users
                .Where(u => normalizedSourceIds.Contains(u.UserId) && u.IsActive)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.IsActive, false)
                    .SetProperty(u => u.EmployeeId, "0"), ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new MergeUsersResultDto(
                targetUserId,
                normalizedSourceIds.Length,
                quizAttemptsMoved,
                sourceUsersDeactivated,
                sourceCompletionRecordsMerged,
                targetShouldBeComplete);
        });
    }

    private async Task<bool> HasMetCompletionRuleAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
            return false;

        var publishedQuizIds = await _db.Quizzes
            .AsNoTracking()
            .Where(q => q.IsActive && q.IsPublished)
            .Select(q => q.QuizId)
            .ToListAsync(ct);

        if (publishedQuizIds.Count == 0)
            return false;

        var passedAttempts = await _db.QuizAttempts
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && !a.IsTestData && a.Status == "Passed")
            .Where(a => publishedQuizIds.Contains(a.QuizId))
            .ToListAsync(ct);

        var perQuizCompletionDates = passedAttempts
            .GroupBy(a => a.QuizId)
            .Select(g => g
                .Select(a => a.CompletedAt ?? a.StartedAt)
                .OrderBy(date => date)
                .Cast<DateTimeOffset?>()
                .FirstOrDefault())
            .ToList();

        return CompletionRuleEvaluator.HasMetCompletionRule(
            publishedQuizIds.Count,
            passedAttempts.Select(a => a.QuizId).Distinct().Count(),
            perQuizCompletionDates);
    }

    private async Task<DateTimeOffset> ResolveCompletionDateAsync(
        Guid targetUserId,
        CompletionRecord? targetCompletionRecord,
        IReadOnlyCollection<CompletionRecord> sourceCompletionRecords,
        CancellationToken ct)
    {
        var existingCompletionDates = sourceCompletionRecords
            .Select(c => (DateTimeOffset?)c.CompletedAt)
            .Append(targetCompletionRecord?.CompletedAt)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .OrderBy(d => d)
            .ToList();

        if (existingCompletionDates.Count > 0)
            return existingCompletionDates[0];

        var publishedQuizIds = await _db.Quizzes
            .AsNoTracking()
            .Where(q => q.IsActive && q.IsPublished)
            .Select(q => q.QuizId)
            .ToListAsync(ct);

        var passedAttempts = await _db.QuizAttempts
            .AsNoTracking()
            .Where(a => a.UserId == targetUserId && !a.IsTestData && a.Status == "Passed")
            .Where(a => publishedQuizIds.Contains(a.QuizId))
            .ToListAsync(ct);

        var perQuizCompletionDates = passedAttempts
            .GroupBy(a => a.QuizId)
            .Select(g => g
                .Select(a => a.CompletedAt ?? a.StartedAt)
                .OrderBy(date => date)
                .Cast<DateTimeOffset?>()
                .FirstOrDefault())
            .ToList();

        var derivedCompletionDate = CompletionRuleEvaluator.GetDerivedCompletionDate(
            publishedQuizIds.Count,
            passedAttempts.Select(a => a.QuizId).Distinct().Count(),
            perQuizCompletionDates);
        if (derivedCompletionDate.HasValue)
            return derivedCompletionDate.Value;

        var completionDate = await _db.QuizAttempts
            .AsNoTracking()
            .Where(a => a.UserId == targetUserId && !a.IsTestData && a.Status == "Passed")
            .Select(a => a.CompletedAt ?? a.StartedAt)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(ct);

        return completionDate == default
            ? DateTimeOffset.UtcNow
            : completionDate;
    }

    private static DateTimeOffset? MergeNullableDates(DateTimeOffset? current, IEnumerable<DateTimeOffset?> otherValues)
    {
        return otherValues
            .Append(current)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .OrderBy(d => d)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();
    }

    private static bool HasMissingEmployeeId(User user) =>
        string.IsNullOrWhiteSpace(user.EmployeeId) || string.Equals(user.EmployeeId.Trim(), "0", StringComparison.OrdinalIgnoreCase);

    private static string? GetNormalizedEmployeeId(User user) =>
        HasMissingEmployeeId(user) ? null : user.EmployeeId.Trim();
}

public class ReportingAccessRepository : IReportingAccessRepository
{
    private readonly Volaris101DbContext _db;
    public ReportingAccessRepository(Volaris101DbContext db) => _db = db;

    public async Task<IReadOnlyList<OrgAdminAssignment>> GetOrgAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
        await _db.OrgAdminAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.OrganizationId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PortfolioAdminAssignment>> GetPortfolioAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
        await _db.PortfolioAdminAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.PortfolioId)
            .ToListAsync(ct);

    public async Task SetOrgAssignmentsAsync(Guid userId, IReadOnlyList<int> organizationIds, Guid actorUserId, CancellationToken ct = default)
    {
        var distinctIds = organizationIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
        var validOrgIds = await _db.VbuMasterList
            .AsNoTracking()
            .Where(v => distinctIds.Contains(v.OrganizationId))
            .Where(v => !_db.Portfolios.Any(p => p.PortfolioId == v.OrganizationId))
            .Select(v => v.OrganizationId)
            .Distinct()
            .ToListAsync(ct);

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await _db.OrgAdminAssignments
                .Where(a => a.UserId == userId)
                .ExecuteDeleteAsync(ct);

            if (validOrgIds.Count > 0)
            {
                _db.OrgAdminAssignments.AddRange(validOrgIds.Select(id => new OrgAdminAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationId = id,
                    AssignedAt = DateTimeOffset.UtcNow,
                    AssignedByUserId = actorUserId
                }));
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        });
    }

    public async Task SetPortfolioAssignmentsAsync(Guid userId, IReadOnlyList<int> portfolioIds, Guid actorUserId, CancellationToken ct = default)
    {
        var distinctIds = portfolioIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await _db.PortfolioAdminAssignments
                .Where(a => a.UserId == userId)
                .ExecuteDeleteAsync(ct);

            if (distinctIds.Count > 0)
            {
                _db.PortfolioAdminAssignments.AddRange(distinctIds.Select(id => new PortfolioAdminAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    UserId = userId,
                    PortfolioId = id,
                    AssignedAt = DateTimeOffset.UtcNow,
                    AssignedByUserId = actorUserId
                }));
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        });
    }

    public async Task<IReadOnlyList<Organization>> SearchOrganizationsAsync(string? searchTerm, int take = 25, CancellationToken ct = default)
    {
        var query = _db.VbuMasterList.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(v =>
                v.OrganizationName.Contains(term) ||
                v.OrganizationId.ToString().Contains(term));
        }

        return await query
            .Where(v => v.OrganizationId > 0)
            .Where(v => !_db.Portfolios.Any(p => p.PortfolioId == v.OrganizationId))
            .GroupBy(v => new { v.OrganizationId, v.OrganizationName })
            .OrderBy(g => g.Key.OrganizationName)
            .Take(take)
            .Select(g => new Organization
            {
                OrganizationId = g.Key.OrganizationId,
                OrganizationName = g.Key.OrganizationName
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Portfolio>> SearchPortfoliosAsync(string? searchTerm, int take = 25, CancellationToken ct = default)
    {
        var query = _db.Portfolios.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(p =>
                p.PortfolioName.Contains(term) ||
                p.PortfolioId.ToString().Contains(term));
        }

        return await query
            .Where(p => p.PortfolioId > 0)
            .OrderBy(p => p.PortfolioName)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<Portfolio?> GetPortfolioByNameAsync(string portfolioName, CancellationToken ct = default)
    {
        var normalizedName = portfolioName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            return Task.FromResult<Portfolio?>(null);

        return _db.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioName == normalizedName, ct);
    }

    public async Task<IReadOnlyList<Organization>> GetOrganizationsByIdsAsync(IReadOnlyCollection<int> organizationIds, CancellationToken ct = default)
    {
        if (organizationIds.Count == 0)
            return Array.Empty<Organization>();

        return await _db.VbuMasterList
            .AsNoTracking()
            .Where(v => organizationIds.Contains(v.OrganizationId))
            .Where(v => !_db.Portfolios.Any(p => p.PortfolioId == v.OrganizationId))
            .GroupBy(v => new { v.OrganizationId, v.OrganizationName })
            .OrderBy(g => g.Key.OrganizationName)
            .Select(g => new Organization
            {
                OrganizationId = g.Key.OrganizationId,
                OrganizationName = g.Key.OrganizationName
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Portfolio>> GetPortfoliosByIdsAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default)
    {
        if (portfolioIds.Count == 0)
            return Array.Empty<Portfolio>();

        return await _db.Portfolios
            .AsNoTracking()
            .Where(p => portfolioIds.Contains(p.PortfolioId))
            .OrderBy(p => p.PortfolioName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetOrganizationIdsForPortfoliosAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default)
    {
        if (portfolioIds.Count == 0)
            return Array.Empty<int>();

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.PortfolioId.HasValue && portfolioIds.Contains(u.PortfolioId.Value) && u.OrganizationId.HasValue && u.OrganizationId.Value > 0)
            .Select(u => u.OrganizationId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) =>
        await _db.VbuMasterList.AsNoTracking().ToListAsync(ct);
}

public class AccessGroupRepository : IAccessGroupRepository
{
    private readonly Volaris101DbContext _db;
    public AccessGroupRepository(Volaris101DbContext db) => _db = db;

    public async Task<IReadOnlyList<AccessGroup>> GetAccessGroupsAsync(CancellationToken ct = default) =>
        await _db.AccessGroups
            .AsNoTracking()
            .OrderBy(g => g.GroupTypeCode)
            .ThenBy(g => g.Name)
            .ToListAsync(ct);

    public Task<AccessGroup?> GetAccessGroupByIdAsync(Guid accessGroupId, CancellationToken ct = default) =>
        _db.AccessGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.AccessGroupId == accessGroupId, ct);

    public async Task<IReadOnlyList<AccessGroup>> GetAccessGroupsByIdsAsync(IReadOnlyCollection<Guid> accessGroupIds, CancellationToken ct = default)
    {
        if (accessGroupIds.Count == 0)
            return Array.Empty<AccessGroup>();

        return await _db.AccessGroups
            .AsNoTracking()
            .Where(g => accessGroupIds.Contains(g.AccessGroupId))
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
    }

    public async Task UpdateAccessGroupAsync(AccessGroup group, CancellationToken ct = default)
    {
        _db.AccessGroups.Update(group);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserAccessGroupAssignment>> GetUserGroupAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserAccessGroupAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.AccessGroupId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserAccessGroupAssignment>> GetGroupUserAssignmentsAsync(Guid accessGroupId, CancellationToken ct = default) =>
        await _db.UserAccessGroupAssignments
            .AsNoTracking()
            .Where(a => a.AccessGroupId == accessGroupId)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync(ct);

    public async Task SetUserGroupAssignmentsAsync(Guid userId, IReadOnlyList<Guid> accessGroupIds, Guid actorUserId, CancellationToken ct = default)
    {
        var normalizedIds = accessGroupIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var existing = await _db.UserAccessGroupAssignments
                .Where(a => a.UserId == userId)
                .ToListAsync(ct);

            if (existing.Count > 0)
                _db.UserAccessGroupAssignments.RemoveRange(existing);

            if (normalizedIds.Count > 0)
            {
                var now = DateTimeOffset.UtcNow;
                _db.UserAccessGroupAssignments.AddRange(normalizedIds.Select(groupId => new UserAccessGroupAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    UserId = userId,
                    AccessGroupId = groupId,
                    AssignedAt = now,
                    AssignedByUserId = actorUserId
                }));
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountUserAssignmentsByGroupAsync(CancellationToken ct = default) =>
        await _db.UserAccessGroupAssignments
            .AsNoTracking()
            .GroupBy(a => a.AccessGroupId)
            .Select(g => new { AccessGroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.AccessGroupId, g => g.Count, ct);

    public async Task<IReadOnlyList<Portfolio>> GetAllPortfoliosAsync(CancellationToken ct = default) =>
        await _db.Portfolios
            .AsNoTracking()
            .Where(p => p.PortfolioId > 0)
            .OrderBy(p => p.PortfolioName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) =>
        await _db.VbuMasterList
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAccessGroupsAsync(IReadOnlyList<AccessGroup> groups, CancellationToken ct = default)
    {
        if (groups.Count == 0)
            return;

        _db.AccessGroups.AddRange(groups);
        await _db.SaveChangesAsync(ct);
    }
}

public class ReportingDashboardRepository : IReportingDashboardRepository
{
    private readonly Volaris101DbContext _db;

    public ReportingDashboardRepository(Volaris101DbContext db) => _db = db;

    public async Task<ReportingDashboardDto> GetLanguageDashboardAsync(CancellationToken ct = default)
    {
        var rows = new List<ReportingDashboardRowDto>();
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.SP_ReportingLanguageSummary_Get";
            command.CommandType = CommandType.StoredProcedure;

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var languageCode = reader.GetString(0).Trim();
                var languageName = reader.GetString(1);
                var inProgressCount = reader.GetInt32(2);
                var completedCount = reader.GetInt32(3);
                var totalUsers = reader.GetInt32(4);
                var rowLastRefreshedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(5);
                var sortOrder = reader.GetInt32(6);
                var isTotalsRow = sortOrder > 0 || string.Equals(languageName, "Totals", StringComparison.OrdinalIgnoreCase);

                rows.Add(new ReportingDashboardRowDto(
                    languageCode,
                    languageName,
                    inProgressCount,
                    completedCount,
                    totalUsers,
                    rowLastRefreshedAt,
                    isTotalsRow,
                    sortOrder));
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        var lastRefreshedAt = rows
            .Select(r => r.LastRefreshedAt)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .DefaultIfEmpty()
            .Max();

        return new ReportingDashboardDto(lastRefreshedAt, rows);
    }
}

public class QuizRepository : IQuizRepository
{
    private readonly Volaris101DbContext _db;
    public QuizRepository(Volaris101DbContext db) => _db = db;

    private IQueryable<Quiz> QuizWithContent() =>
        _db.Quizzes
            .Include(q => q.Translations)
            .Include(q => q.Questions.Where(q => q.IsActive))
                .ThenInclude(q => q.Translations)
            .Include(q => q.Questions.Where(q => q.IsActive))
                .ThenInclude(q => q.Options)
                    .ThenInclude(o => o.Translations);

    public async Task<IReadOnlyList<Quiz>> GetAllActiveAsync(string lang = "en", CancellationToken ct = default) =>
        await QuizWithContent()
            .Where(q => q.IsActive)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Quiz>> GetAllPublishedAsync(string lang = "en", CancellationToken ct = default) =>
        await QuizWithContent()
            .Where(q => q.IsActive && q.IsPublished)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync(ct);

    public Task<Quiz?> GetByIdAsync(Guid quizId, string lang = "en", CancellationToken ct = default) =>
        QuizWithContent()
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.IsActive, ct);

    public Task<Quiz?> GetByIdForLearnerAsync(Guid quizId, bool includeDrafts, string lang = "en", CancellationToken ct = default) =>
        QuizWithContent()
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.IsActive && (q.IsPublished || includeDrafts), ct);

    public async Task<Quiz> CreateAsync(Quiz quiz, CancellationToken ct = default)
    {
        _db.Quizzes.Add(quiz);
        await _db.SaveChangesAsync(ct);
        return quiz;
    }

    public async Task<Quiz> UpdateAsync(Quiz quiz, CancellationToken ct = default)
    {
        quiz.UpdatedAt = DateTimeOffset.UtcNow;
        _db.Quizzes.Update(quiz);
        await _db.SaveChangesAsync(ct);
        return quiz;
    }

    public async Task<int> PublishAsync(Quiz quiz, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var testAttemptIds = _db.QuizAttempts
                .Where(a => a.QuizId == quiz.QuizId && a.IsTestData)
                .Select(a => a.AttemptId);

            await _db.AttemptAnswers
                .Where(aa => testAttemptIds.Contains(aa.AttemptId))
                .ExecuteDeleteAsync(ct);

            var deletedAttempts = await _db.QuizAttempts
                .Where(a => a.QuizId == quiz.QuizId && a.IsTestData)
                .ExecuteDeleteAsync(ct);

            var now = DateTimeOffset.UtcNow;
            var updated = await _db.Quizzes
                .Where(q => q.QuizId == quiz.QuizId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(q => q.IsPublished, true)
                    .SetProperty(q => q.UpdatedAt, now), ct);

            if (updated == 0)
                throw new InvalidOperationException("Quiz no longer exists.");

            quiz.IsPublished = true;
            quiz.UpdatedAt = now;

            await tx.CommitAsync(ct);
            return deletedAttempts;
        });
    }

    public Task<Question?> GetQuestionByIdAsync(Guid questionId, string lang = "en", CancellationToken ct = default) =>
        _db.Questions
            .Include(q => q.Translations)
            .Include(q => q.Options).ThenInclude(o => o.Translations)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId, ct);

    public async Task<Question> CreateQuestionAsync(Question question, CancellationToken ct = default)
    {
        _db.Questions.Add(question);
        await _db.SaveChangesAsync(ct);
        return question;
    }

    public async Task<Question> UpdateQuestionAsync(Question question, CancellationToken ct = default)
    {
        question.UpdatedAt = DateTimeOffset.UtcNow;
        _db.Questions.Update(question);
        await _db.SaveChangesAsync(ct);
        return question;
    }

    public async Task DeleteQuestionAsync(Guid questionId, CancellationToken ct = default)
    {
        var q = await _db.Questions.FindAsync(new object[] { questionId }, ct);
        if (q != null) { q.IsActive = false; await _db.SaveChangesAsync(ct); }
    }

    public async Task<IReadOnlyList<Question>> SearchQuestionsAsync(string? searchTerm, Guid? quizId, string lang = "en", CancellationToken ct = default)
    {
        var query = _db.Questions
            .Include(q => q.Translations)
            .Include(q => q.Options).ThenInclude(o => o.Translations)
            .Where(q => q.IsActive);

        if (quizId.HasValue)
            query = query.Where(q => q.QuizId == quizId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(q => q.Translations.Any(t =>
                t.QuestionText.Contains(searchTerm)));

        return await query.OrderBy(q => q.DisplayOrder).ToListAsync(ct);
    }
}

public class AttemptRepository : IAttemptRepository
{
    private readonly Volaris101DbContext _db;
    public AttemptRepository(Volaris101DbContext db) => _db = db;

    public Task<QuizAttempt?> GetAttemptByIdAsync(Guid attemptId, CancellationToken ct = default) =>
        _db.QuizAttempts.Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.AttemptId == attemptId, ct);

    public Task<int> GetAttemptCountAsync(Guid userId, Guid quizId, CancellationToken ct = default) =>
        _db.QuizAttempts.CountAsync(a => a.UserId == userId && a.QuizId == quizId, ct);

    public async Task<IReadOnlyList<QuizAttempt>> GetUserAttemptsForQuizAsync(Guid userId, Guid quizId, CancellationToken ct = default) =>
        await _db.QuizAttempts
            .Include(a => a.Answers)
            .Where(a => a.UserId == userId && a.QuizId == quizId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<QuizAttempt>> GetAllAttemptsForUserAsync(Guid userId, CancellationToken ct = default) =>
        await _db.QuizAttempts
            .Include(a => a.Answers)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

    public async Task<QuizAttempt> CreateAttemptAsync(QuizAttempt attempt, CancellationToken ct = default)
    {
        _db.QuizAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
        return attempt;
    }

    public async Task<QuizAttempt> UpdateAttemptAsync(QuizAttempt attempt, CancellationToken ct = default)
    {
        var submittedAnswers = attempt.Answers
            .Select(a => new AttemptAnswer
            {
                AttemptAnswerId = a.AttemptAnswerId != Guid.Empty ? a.AttemptAnswerId : Guid.NewGuid(),
                AttemptId = attempt.AttemptId,
                QuestionId = a.QuestionId,
                OptionId = a.OptionId,
                SelectedAt = a.SelectedAt
            })
            .ToList();

        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            foreach (var entry in _db.ChangeTracker.Entries<AttemptAnswer>()
                         .Where(e => e.Entity.AttemptId == attempt.AttemptId)
                         .ToList())
                entry.State = EntityState.Detached;

            var attemptEntry = _db.Entry(attempt);
            if (attemptEntry.State != EntityState.Detached)
                attemptEntry.State = EntityState.Unchanged;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var updated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE dbo.QuizAttempts
                SET Status = {attempt.Status},
                    RawScore = {attempt.CorrectCount},
                    TotalQuestions = {attempt.TotalCount},
                    ScorePercent = {attempt.ScorePercent},
                    CurrentQuestionPosition = {attempt.CurrentQuestionPosition},
                    CompletedAt = {attempt.CompletedAt}
                WHERE AttemptID = {attempt.AttemptId}
                  AND Status = {"InProgress"};", ct);

            if (updated == 0)
                throw new InvalidOperationException("Attempt has already been submitted or no longer exists.");

            await _db.AttemptAnswers
                .Where(a => a.AttemptId == attempt.AttemptId)
                .ExecuteDeleteAsync(ct);

            if (submittedAnswers.Count > 0)
            {
                var answersToInsert = submittedAnswers.Select(a => new AttemptAnswer
                {
                    AttemptAnswerId = a.AttemptAnswerId,
                    AttemptId = a.AttemptId,
                    QuestionId = a.QuestionId,
                    OptionId = a.OptionId,
                    SelectedAt = a.SelectedAt
                }).ToList();

                _db.AttemptAnswers.AddRange(answersToInsert);
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        });

        return attempt;
    }

    public async Task<IReadOnlyList<QuizAttempt>> GetAttemptsForReportingAsync(bool includeTestData = false, CancellationToken ct = default)
    {
        var query = _db.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Answers)
            .AsQueryable();

        if (!includeTestData)
            query = query.Where(a => !a.IsTestData);

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(Guid UserId, Guid QuizId, bool HasPassed, int TotalAttempts, decimal? BestScore, DateTimeOffset? LastAttemptAt)>>
        GetPassStatusSummaryAsync(
            bool includeTestData = false,
            IReadOnlyCollection<Guid>? userIds = null,
            CancellationToken ct = default)
    {
        var query = _db.ReportingAttemptSummaryRows
            .AsNoTracking();

        if (!includeTestData)
            query = query.Where(r => !r.IsTestData);

        if (userIds is { Count: 0 })
            return Array.Empty<(Guid UserId, Guid QuizId, bool HasPassed, int TotalAttempts, decimal? BestScore, DateTimeOffset? LastAttemptAt)>();

        if (userIds is { Count: > 0 })
            query = query.Where(r => userIds.Contains(r.UserId));

        var raw = await query
            .OrderBy(r => r.UserId)
            .ThenBy(r => r.QuizId)
            .ToListAsync(ct);

        return raw.Select(r => (r.UserId, r.QuizId, r.HasPassed, r.TotalAttempts, r.BestScore, r.LastAttemptAt))
            .ToList();
    }
}

public class CompletionRepository : ICompletionRepository
{
    private readonly Volaris101DbContext _db;
    public CompletionRepository(Volaris101DbContext db) => _db = db;

    public Task<CompletionRecord?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.CompletionRecords.FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public async Task<CompletionRecord> CreateAsync(CompletionRecord record, CancellationToken ct = default)
    {
        _db.CompletionRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<CompletionRecord> UpdateAsync(CompletionRecord record, CancellationToken ct = default)
    {
        _db.CompletionRecords.Update(record);
        await _db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<IReadOnlyList<CompletionRecord>> GetAllAsync(CancellationToken ct = default) =>
        await _db.CompletionRecords.ToListAsync(ct);
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly Volaris101DbContext _db;
    public AuditLogRepository(Volaris101DbContext db) => _db = db;

    public async Task WriteAsync(string actionType, string outcome, Guid? actorUserId, string actorType,
        Guid? targetUserId, string? beforeValue, string? afterValue, string? sourceComponent,
        string? denialReason = null, string? ipAddress = null, string? additionalDataJson = null,
        CancellationToken ct = default)
    {
        _db.AdminAuditLog.Add(new AuditLogEntry
        {
            LogEntryId = Guid.NewGuid(),
            LoggedAt = DateTimeOffset.UtcNow,
            ActionType = actionType,
            Outcome = outcome,
            ActorUserId = actorUserId,
            ActorType = actorType,
            TargetUserId = targetUserId,
            BeforeValue = beforeValue,
            AfterValue = afterValue,
            SourceComponent = sourceComponent,
            DenialReason = denialReason,
            IpAddress = ipAddress,
            AdditionalData = additionalDataJson
        });
        await _db.SaveChangesAsync(ct);
    }
}
