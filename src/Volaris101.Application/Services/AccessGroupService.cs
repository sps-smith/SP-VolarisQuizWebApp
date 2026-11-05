using System.Text.Json;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;

namespace Volaris101.Application.Services;

public class AccessGroupService : IAccessGroupService
{
    private readonly IAccessGroupRepository _repo;
    private readonly IAuditLogRepository? _auditLog;
    private readonly IUserRepository? _users;

    public AccessGroupService(IAccessGroupRepository repo, IAuditLogRepository? auditLog = null, IUserRepository? users = null)
    {
        _repo = repo;
        _auditLog = auditLog;
        _users = users;
    }

    public Task<IReadOnlyList<AccessGroup>> GetAccessGroupsAsync(CancellationToken ct = default) =>
        _repo.GetAccessGroupsAsync(ct);

    public Task<AccessGroup?> GetAccessGroupByIdAsync(Guid accessGroupId, CancellationToken ct = default) =>
        _repo.GetAccessGroupByIdAsync(accessGroupId, ct);

    public async Task<IReadOnlyList<AccessGroupListItemDto>> GetGroupListAsync(
        string? searchTerm = null,
        string? groupTypeCode = null,
        bool includeInactive = true,
        string? organizationSearchTerm = null,
        CancellationToken ct = default)
    {
        var groups = await _repo.GetAccessGroupsAsync(ct);
        var portfolios = await _repo.GetAllPortfoliosAsync(ct);
        var vbuRows = await _repo.GetVbuMasterListAsync(ct);
        var assignmentCounts = await _repo.CountUserAssignmentsByGroupAsync(ct);

        var query = groups.AsEnumerable();
        if (!includeInactive)
            query = query.Where(g => g.IsActive);
        if (!string.IsNullOrWhiteSpace(groupTypeCode))
            query = query.Where(g => string.Equals(g.GroupTypeCode, groupTypeCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(g =>
                g.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (g.OrganizationId?.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (g.PortfolioId?.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (!string.IsNullOrWhiteSpace(organizationSearchTerm))
        {
            var organizationTerm = organizationSearchTerm.Trim();
            var matchingOrganizationIds = vbuRows
                .Where(v =>
                    v.OrganizationId > 0 &&
                    (v.OrganizationName.Contains(organizationTerm, StringComparison.OrdinalIgnoreCase) ||
                     v.OrganizationId.ToString().Contains(organizationTerm, StringComparison.OrdinalIgnoreCase)))
                .Select(v => v.OrganizationId)
                .ToHashSet();

            query = matchingOrganizationIds.Count == 0
                ? []
                : query.Where(g => ResolveSeedId(g) is int seedId && ExpandDescendants(seedId, vbuRows).Overlaps(matchingOrganizationIds));
        }

        var portfolioNames = portfolios.ToDictionary(p => p.PortfolioId, p => p.PortfolioName);
        return query
            .Select(g => new AccessGroupListItemDto(
                g.AccessGroupId,
                g.Name,
                g.GroupTypeCode,
                GroupTypeName(g.GroupTypeCode),
                g.PortfolioId,
                g.OrganizationId,
                g.ParentPortfolioId,
                g.ParentPortfolioId.HasValue && portfolioNames.TryGetValue(g.ParentPortfolioId.Value, out var parentName) ? parentName : null,
                g.IsSystemManaged,
                g.IsActive,
                ResolveSeedId(g) is int seedId ? ExpandDescendants(seedId, vbuRows).Count : 0,
                assignmentCounts.TryGetValue(g.AccessGroupId, out var count) ? count : 0))
            .OrderBy(g => g.GroupTypeName)
            .ThenBy(g => g.Name)
            .ToList();
    }

    public async Task<AccessGroupDetailDto> GetGroupDetailAsync(Guid accessGroupId, CancellationToken ct = default)
    {
        var group = await _repo.GetAccessGroupByIdAsync(accessGroupId, ct)
            ?? throw new KeyNotFoundException($"Access group {accessGroupId} not found.");

        return await BuildDetailDtoAsync(group, ct);
    }

    public async Task<AccessGroupDetailDto> SetGroupActiveStatusAsync(Guid accessGroupId, bool isActive, Guid actorUserId, CancellationToken ct = default)
    {
        var group = await _repo.GetAccessGroupByIdAsync(accessGroupId, ct)
            ?? throw new KeyNotFoundException($"Access group {accessGroupId} not found.");

        var before = JsonSerializer.Serialize(new { group.AccessGroupId, group.Name, group.IsActive });
        group.IsActive = isActive;
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAccessGroupAsync(group, ct);

        var after = JsonSerializer.Serialize(new { group.AccessGroupId, group.Name, group.IsActive });
        if (_auditLog is not null)
        {
            await _auditLog.WriteAsync(
                "admin.access_group_status_change",
                "success",
                actorUserId,
                "user",
                null,
                before,
                after,
                "admin.groups",
                additionalDataJson: after,
                ct: ct);
        }

        return await BuildDetailDtoAsync(group, ct);
    }

    public async Task<IReadOnlyList<AccessGroupAssignmentDto>> GetUserGroupAssignmentsAsync(Guid userId, CancellationToken ct = default)
    {
        var assignments = await _repo.GetUserGroupAssignmentsAsync(userId, ct);
        var groups = await _repo.GetAccessGroupsByIdsAsync(assignments.Select(a => a.AccessGroupId).Distinct().ToArray(), ct);
        return await BuildAssignmentDtosAsync(groups, ct);
    }

    public async Task<IReadOnlyList<AccessGroupAssignmentDto>> SetUserGroupAssignmentsAsync(Guid userId, IReadOnlyList<Guid>? accessGroupIds, Guid actorUserId, CancellationToken ct = default)
    {
        var normalizedIds = (accessGroupIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var before = await GetUserGroupAssignmentsAsync(userId, ct);
        var groups = await _repo.GetAccessGroupsByIdsAsync(normalizedIds, ct);
        if (groups.Count != normalizedIds.Count)
            throw new ArgumentException("One or more selected groups no longer exist.");
        if (groups.Any(g => !g.IsActive))
            throw new ArgumentException("Inactive groups cannot be assigned to a user.");
        await _repo.SetUserGroupAssignmentsAsync(userId, normalizedIds, actorUserId, ct);

        var after = await BuildAssignmentDtosAsync(groups, ct);
        if (_auditLog is not null)
        {
            var additionalDataJson = JsonSerializer.Serialize(new
            {
                before,
                after
            });
            await _auditLog.WriteAsync(
                "admin.user_access_group_change",
                "success",
                actorUserId,
                "user",
                userId,
                $"{before.Count} group(s)",
                $"{after.Count} group(s)",
                "admin.roles",
                additionalDataJson: additionalDataJson,
                ct: ct);
        }

        return after;
    }

    public async Task<IReadOnlyList<AccessGroupUserDto>> GetGroupUsersAsync(Guid accessGroupId, CancellationToken ct = default)
    {
        _ = await _repo.GetAccessGroupByIdAsync(accessGroupId, ct)
            ?? throw new KeyNotFoundException($"Access group {accessGroupId} not found.");

        return await BuildGroupUserDtosAsync(accessGroupId, ct);
    }

    public async Task<IReadOnlyList<AccessGroupUserDto>> AddUserToGroupAsync(Guid accessGroupId, Guid userId, Guid actorUserId, CancellationToken ct = default)
    {
        var group = await _repo.GetAccessGroupByIdAsync(accessGroupId, ct)
            ?? throw new KeyNotFoundException($"Access group {accessGroupId} not found.");
        if (!group.IsActive)
            throw new ArgumentException("Inactive groups cannot be assigned to a user.");

        var user = await GetRequiredUserAsync(userId, ct);
        if (!user.IsActive)
            throw new ArgumentException("Inactive users cannot be added to a group.");
        if (!IsReportingAdminRole(user.RoleCode))
        {
            var beforeRole = user.RoleCode;
            user.RoleCode = "ReportingAdmin";
            await _users!.UpdateAsync(user, ct);

            if (_auditLog is not null)
            {
                await _auditLog.WriteAsync(
                    "admin.role_change",
                    "success",
                    actorUserId,
                    "user",
                    user.UserId,
                    beforeRole,
                    user.RoleCode,
                    "admin.groups",
                    ct: ct);
            }
        }

        var currentGroupIds = (await _repo.GetUserGroupAssignmentsAsync(userId, ct))
            .Select(a => a.AccessGroupId)
            .Append(accessGroupId)
            .Distinct()
            .ToList();

        await SetUserGroupAssignmentsAsync(userId, currentGroupIds, actorUserId, ct);
        return await BuildGroupUserDtosAsync(accessGroupId, ct);
    }

    public async Task<IReadOnlyList<AccessGroupUserDto>> RemoveUserFromGroupAsync(Guid accessGroupId, Guid userId, Guid actorUserId, CancellationToken ct = default)
    {
        _ = await _repo.GetAccessGroupByIdAsync(accessGroupId, ct)
            ?? throw new KeyNotFoundException($"Access group {accessGroupId} not found.");
        _ = await GetRequiredUserAsync(userId, ct);

        var currentGroupIds = (await _repo.GetUserGroupAssignmentsAsync(userId, ct))
            .Select(a => a.AccessGroupId)
            .Where(id => id != accessGroupId)
            .Distinct()
            .ToList();

        await SetUserGroupAssignmentsAsync(userId, currentGroupIds, actorUserId, ct);
        return await BuildGroupUserDtosAsync(accessGroupId, ct);
    }

    public async Task<AccessGroupSeedResultDto> SeedSystemManagedGroupsAsync(CancellationToken ct = default)
    {
        var portfolios = await _repo.GetAllPortfoliosAsync(ct);
        var vbuRows = await _repo.GetVbuMasterListAsync(ct);
        var existingGroups = await _repo.GetAccessGroupsAsync(ct);

        var portfolioIds = portfolios
            .Where(p => p.PortfolioId > 0)
            .Select(p => p.PortfolioId)
            .ToHashSet();

        var existingPortfolioGroupIds = existingGroups
            .Where(g => g.GroupTypeCode == AccessGroupTypes.PortfolioGroup && g.PortfolioId.HasValue)
            .Select(g => g.PortfolioId!.Value)
            .ToHashSet();

        var existingSubGroupOrgIds = existingGroups
            .Where(g => g.GroupTypeCode == AccessGroupTypes.PortfolioSubGroup && g.OrganizationId.HasValue)
            .Select(g => g.OrganizationId!.Value)
            .ToHashSet();

        var now = DateTimeOffset.UtcNow;
        var newGroups = new List<AccessGroup>();

        foreach (var portfolio in portfolios.Where(p => p.PortfolioId > 0).OrderBy(p => p.PortfolioName))
        {
            if (existingPortfolioGroupIds.Contains(portfolio.PortfolioId))
                continue;

            newGroups.Add(new AccessGroup
            {
                AccessGroupId = Guid.NewGuid(),
                GroupTypeCode = AccessGroupTypes.PortfolioGroup,
                Name = $"{portfolio.PortfolioName} (Group)",
                PortfolioId = portfolio.PortfolioId,
                IsSystemManaged = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var reportToIds = vbuRows
            .Select(v => v.ReportToId)
            .Where(id => id > 0)
            .ToHashSet();

        var rowsByOrganizationId = vbuRows
            .Where(v => v.OrganizationId > 0)
            .GroupBy(v => v.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var row in rowsByOrganizationId.Values.OrderBy(v => v.OrganizationName))
        {
            if (!IsEligiblePortfolioSubGroupAnchor(row.OrganizationId, reportToIds, portfolioIds))
                continue;
            if (existingSubGroupOrgIds.Contains(row.OrganizationId))
                continue;

            var parentPortfolioId = ResolveParentPortfolioId(row.OrganizationId, rowsByOrganizationId, portfolioIds);
            if (!parentPortfolioId.HasValue)
                continue;

            newGroups.Add(new AccessGroup
            {
                AccessGroupId = Guid.NewGuid(),
                GroupTypeCode = AccessGroupTypes.PortfolioSubGroup,
                Name = $"{row.OrganizationName} (Sub-Group)",
                OrganizationId = row.OrganizationId,
                ParentPortfolioId = parentPortfolioId.Value,
                IsSystemManaged = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (newGroups.Count > 0)
            await _repo.AddAccessGroupsAsync(newGroups, ct);

        return new AccessGroupSeedResultDto(
            newGroups.Count(g => g.GroupTypeCode == AccessGroupTypes.PortfolioGroup),
            newGroups.Count(g => g.GroupTypeCode == AccessGroupTypes.PortfolioSubGroup));
    }

    public async Task<HashSet<int>> ResolveEffectiveOrganizationIdsAsync(Guid accessGroupId, CancellationToken ct = default)
    {
        var group = await _repo.GetAccessGroupByIdAsync(accessGroupId, ct);
        if (group is null || !group.IsActive)
            return [];

        var vbuRows = await _repo.GetVbuMasterListAsync(ct);
        var seedId = group.GroupTypeCode switch
        {
            AccessGroupTypes.PortfolioGroup => group.PortfolioId,
            AccessGroupTypes.PortfolioSubGroup => group.OrganizationId,
            _ => null
        };

        return seedId.HasValue && seedId.Value > 0
            ? ExpandDescendants(seedId.Value, vbuRows)
            : [];
    }

    private async Task<AccessGroupDetailDto> BuildDetailDtoAsync(AccessGroup group, CancellationToken ct)
    {
        var portfolios = await _repo.GetAllPortfoliosAsync(ct);
        var vbuRows = await _repo.GetVbuMasterListAsync(ct);
        var assignmentCounts = await _repo.CountUserAssignmentsByGroupAsync(ct);
        var parentPortfolioName = group.ParentPortfolioId.HasValue
            ? portfolios.FirstOrDefault(p => p.PortfolioId == group.ParentPortfolioId.Value)?.PortfolioName
            : null;

        var effectiveIds = ResolveSeedId(group) is int seedId
            ? ExpandDescendants(seedId, vbuRows)
            : [];
        var organizationNames = vbuRows
            .Where(v => v.OrganizationId > 0)
            .GroupBy(v => v.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First().OrganizationName);
        foreach (var portfolio in portfolios)
            organizationNames.TryAdd(portfolio.PortfolioId, portfolio.PortfolioName);

        var organizations = vbuRows
            .Where(v => effectiveIds.Contains(v.OrganizationId))
            .GroupBy(v => v.OrganizationId)
            .Select(g => g.First())
            .OrderBy(v => v.OrganizationName)
            .ThenBy(v => v.OrganizationId)
            .Select(v => new AccessGroupOrganizationDto(
                v.OrganizationId,
                v.OrganizationName,
                v.ReportToId,
                v.ReportToId > 0 && organizationNames.TryGetValue(v.ReportToId, out var reportToName)
                    ? reportToName
                    : "-"))
            .ToList();

        return new AccessGroupDetailDto(
            group.AccessGroupId,
            group.Name,
            group.GroupTypeCode,
            GroupTypeName(group.GroupTypeCode),
            group.Description,
            group.PortfolioId,
            group.OrganizationId,
            group.ParentPortfolioId,
            parentPortfolioName,
            group.IsSystemManaged,
            group.IsActive,
            assignmentCounts.TryGetValue(group.AccessGroupId, out var count) ? count : 0,
            organizations);
    }

    private async Task<IReadOnlyList<AccessGroupAssignmentDto>> BuildAssignmentDtosAsync(IReadOnlyList<AccessGroup> groups, CancellationToken ct)
    {
        var vbuRows = await _repo.GetVbuMasterListAsync(ct);
        return groups
            .OrderBy(g => GroupTypeName(g.GroupTypeCode))
            .ThenBy(g => g.Name)
            .Select(g => new AccessGroupAssignmentDto(
                g.AccessGroupId,
                g.Name,
                g.GroupTypeCode,
                ResolveSeedId(g) is int seedId ? ExpandDescendants(seedId, vbuRows).Count : 0,
                g.IsActive))
            .ToList();
    }

    private async Task<IReadOnlyList<AccessGroupUserDto>> BuildGroupUserDtosAsync(Guid accessGroupId, CancellationToken ct)
    {
        if (_users is null)
            return [];

        var assignments = await _repo.GetGroupUserAssignmentsAsync(accessGroupId, ct);
        var users = await _users.GetByIdsAsync(assignments.Select(a => a.UserId).Distinct().ToArray(), ct);
        var usersById = users.ToDictionary(u => u.UserId);

        return assignments
            .Where(a => usersById.ContainsKey(a.UserId))
            .Select(a =>
            {
                var user = usersById[a.UserId];
                return new AccessGroupUserDto(
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.RoleCode,
                    RoleDisplayName(user.RoleCode),
                    user.IsActive,
                    a.AssignedAt);
            })
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Email)
            .ToList();
    }

    private async Task<User> GetRequiredUserAsync(Guid userId, CancellationToken ct)
    {
        if (_users is null)
            throw new InvalidOperationException("User lookup is not available.");

        return await _users.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
    }

    private static bool IsReportingAdminRole(string roleCode) =>
        roleCode is "ReportingAdmin" or "OrgAdmin" or "PortfolioAdmin";

    private static string RoleDisplayName(string roleCode) =>
        roleCode switch
        {
            "GlobalAdmin" => "Super Admin",
            "ReportingAdmin" => "Reporting Admin",
            "OrgAdmin" => "Reporting Admin",
            "PortfolioAdmin" => "Reporting Admin",
            _ => "Learner"
        };

    private static int? ResolveSeedId(AccessGroup group) =>
        group.GroupTypeCode switch
        {
            AccessGroupTypes.PortfolioGroup => group.PortfolioId,
            AccessGroupTypes.PortfolioSubGroup => group.OrganizationId,
            _ => null
        };

    private static string GroupTypeName(string groupTypeCode) =>
        groupTypeCode switch
        {
            AccessGroupTypes.PortfolioGroup => "Portfolio group",
            AccessGroupTypes.PortfolioSubGroup => "Portfolio sub-group",
            AccessGroupTypes.OrgGroup => "Org group",
            _ => groupTypeCode
        };

    private static bool IsEligiblePortfolioSubGroupAnchor(int organizationId, HashSet<int> reportToIds, HashSet<int> portfolioIds) =>
        organizationId > 0 &&
        reportToIds.Contains(organizationId) &&
        !portfolioIds.Contains(organizationId);

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

    private static HashSet<int> ExpandDescendants(int seedOrganizationId, IReadOnlyList<VbuMasterListRecord> vbuRows)
    {
        var scoped = new HashSet<int> { seedOrganizationId };
        var childrenByParent = vbuRows
            .Where(v => v.OrganizationId > 0 && v.ReportToId > 0 && v.OrganizationId != v.ReportToId)
            .GroupBy(v => v.ReportToId)
            .ToDictionary(g => g.Key, g => g.Select(v => v.OrganizationId).Distinct().ToList());

        var queue = new Queue<int>([seedOrganizationId]);
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
