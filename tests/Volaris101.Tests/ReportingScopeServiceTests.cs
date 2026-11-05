using Volaris101.Application.Services;
using Volaris101.Domain.Entities;
using Volaris101.Application.Interfaces;

namespace Volaris101.Tests;

public class ReportingScopeServiceTests
{
    [Fact]
    public async Task GetEffectiveOrganizationScopeAsync_ExpandsOrgHierarchy()
    {
        var repo = new FakeReportingAccessRepository
        {
            OrgAssignments = [new OrgAdminAssignment { OrganizationId = 1 }]
        };
        repo.VbuRows =
        [
            new VbuMasterListRecord { OrganizationId = 1, ReportToId = 0, OrganizationName = "Root" },
            new VbuMasterListRecord { OrganizationId = 2, ReportToId = 1, OrganizationName = "ChildA" },
            new VbuMasterListRecord { OrganizationId = 3, ReportToId = 2, OrganizationName = "ChildB" }
        ];

        var service = new ReportingScopeService(repo);

        var scope = await service.GetEffectiveOrganizationScopeAsync(Guid.NewGuid(), "OrgAdmin");

        Assert.NotNull(scope);
        Assert.Equal(new[] { 1, 2, 3 }, scope!.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task GetEffectiveOrganizationScopeAsync_PortfolioAdminMapsToOrganizations()
    {
        var repo = new FakeReportingAccessRepository
        {
            PortfolioAssignments = [new PortfolioAdminAssignment { PortfolioId = 77 }]
        };
        repo.PortfolioOrgIdsByPortfolioId[77] = [10, 11];
        repo.VbuRows =
        [
            new VbuMasterListRecord { OrganizationId = 10, ReportToId = 0, OrganizationName = "VBU 10" },
            new VbuMasterListRecord { OrganizationId = 11, ReportToId = 0, OrganizationName = "VBU 11" }
        ];

        var service = new ReportingScopeService(repo);

        var scope = await service.GetEffectiveOrganizationScopeAsync(Guid.NewGuid(), "PortfolioAdmin");

        Assert.NotNull(scope);
        Assert.Equal(new[] { 10, 11 }, scope!.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task GetEffectiveOrganizationScopeAsync_ReturnsNull_ForGlobalAdmin()
    {
        var service = new ReportingScopeService(new FakeReportingAccessRepository());

        var scope = await service.GetEffectiveOrganizationScopeAsync(Guid.NewGuid(), "GlobalAdmin");

        Assert.Null(scope);
    }

    [Fact]
    public async Task GetEffectiveOrganizationScopeAsync_ReturnsEmptySet_WhenOrgAdminHasNoAssignments()
    {
        var service = new ReportingScopeService(new FakeReportingAccessRepository());

        var scope = await service.GetEffectiveOrganizationScopeAsync(Guid.NewGuid(), "OrgAdmin");

        Assert.NotNull(scope);
        Assert.Empty(scope!);
    }

    [Fact]
    public async Task GetEffectiveOrganizationScopeAsync_ReportingAdminUsesAssignedGroupsBeforeLegacyAssignments()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var accessRepo = new FakeReportingAccessRepository
        {
            OrgAssignments = [new OrgAdminAssignment { OrganizationId = 999 }],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 100, ReportToId = 0, OrganizationName = "Portfolio" },
                new VbuMasterListRecord { OrganizationId = 101, ReportToId = 100, OrganizationName = "Child" },
                new VbuMasterListRecord { OrganizationId = 999, ReportToId = 0, OrganizationName = "Legacy" }
            ]
        };
        var groupRepo = new FakeAccessGroupRepository
        {
            Assignments = [new UserAccessGroupAssignment { UserId = userId, AccessGroupId = groupId }],
            Groups =
            [
                new AccessGroup
                {
                    AccessGroupId = groupId,
                    GroupTypeCode = AccessGroupTypes.PortfolioGroup,
                    PortfolioId = 100,
                    IsActive = true
                }
            ]
        };
        var service = new ReportingScopeService(accessRepo, groupRepo);

        var scope = await service.GetEffectiveOrganizationScopeAsync(userId, "ReportingAdmin");

        Assert.NotNull(scope);
        Assert.Equal([100, 101], scope!.OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task GetEffectiveOrganizationScopeAsync_ReportingAdminFallsBackToLegacyAssignmentsWhenNoActiveGroups()
    {
        var userId = Guid.NewGuid();
        var inactiveGroupId = Guid.NewGuid();
        var accessRepo = new FakeReportingAccessRepository
        {
            OrgAssignments = [new OrgAdminAssignment { OrganizationId = 999 }],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 100, ReportToId = 0, OrganizationName = "Inactive Group Root" },
                new VbuMasterListRecord { OrganizationId = 999, ReportToId = 0, OrganizationName = "Legacy" }
            ]
        };
        var groupRepo = new FakeAccessGroupRepository
        {
            Assignments = [new UserAccessGroupAssignment { UserId = userId, AccessGroupId = inactiveGroupId }],
            Groups =
            [
                new AccessGroup
                {
                    AccessGroupId = inactiveGroupId,
                    GroupTypeCode = AccessGroupTypes.OrgGroup,
                    OrganizationId = 100,
                    IsActive = false
                }
            ]
        };
        var service = new ReportingScopeService(accessRepo, groupRepo);

        var scope = await service.GetEffectiveOrganizationScopeAsync(userId, "ReportingAdmin");

        Assert.NotNull(scope);
        Assert.Equal([999], scope!.OrderBy(id => id).ToArray());
    }

    private sealed class FakeReportingAccessRepository : IReportingAccessRepository
    {
        public IReadOnlyList<OrgAdminAssignment> OrgAssignments { get; set; } = [];
        public IReadOnlyList<PortfolioAdminAssignment> PortfolioAssignments { get; set; } = [];
        public IReadOnlyList<VbuMasterListRecord> VbuRows { get; set; } = [];
        public Dictionary<int, IReadOnlyList<int>> PortfolioOrgIdsByPortfolioId { get; } = new();

        public Task<IReadOnlyList<OrgAdminAssignment>> GetOrgAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(OrgAssignments);

        public Task<IReadOnlyList<PortfolioAdminAssignment>> GetPortfolioAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(PortfolioAssignments);

        public Task SetOrgAssignmentsAsync(Guid userId, IReadOnlyList<int> organizationIds, Guid actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetPortfolioAssignmentsAsync(Guid userId, IReadOnlyList<int> portfolioIds, Guid actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> SearchOrganizationsAsync(string? searchTerm, int take = 25, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> SearchPortfoliosAsync(string? searchTerm, int take = 25, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Portfolio?> GetPortfolioByNameAsync(string portfolioName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> GetOrganizationsByIdsAsync(IReadOnlyCollection<int> organizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> GetPortfoliosByIdsAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> GetOrganizationIdsForPortfoliosAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default)
        {
            var ids = portfolioIds
                .SelectMany(id => PortfolioOrgIdsByPortfolioId.TryGetValue(id, out var mapped) ? mapped : [])
                .Distinct()
                .ToList();
            return Task.FromResult<IReadOnlyList<int>>(ids);
        }
        public Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) =>
            Task.FromResult(VbuRows);
    }

    private sealed class FakeAccessGroupRepository : IAccessGroupRepository
    {
        public IReadOnlyList<AccessGroup> Groups { get; set; } = [];
        public IReadOnlyList<UserAccessGroupAssignment> Assignments { get; set; } = [];

        public Task<IReadOnlyList<AccessGroup>> GetAccessGroupsAsync(CancellationToken ct = default) =>
            Task.FromResult(Groups);

        public Task<AccessGroup?> GetAccessGroupByIdAsync(Guid accessGroupId, CancellationToken ct = default) =>
            Task.FromResult(Groups.FirstOrDefault(g => g.AccessGroupId == accessGroupId));

        public Task<IReadOnlyList<AccessGroup>> GetAccessGroupsByIdsAsync(IReadOnlyCollection<Guid> accessGroupIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AccessGroup>>(Groups.Where(g => accessGroupIds.Contains(g.AccessGroupId)).ToList());

        public Task UpdateAccessGroupAsync(AccessGroup group, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UserAccessGroupAssignment>> GetUserGroupAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserAccessGroupAssignment>>(Assignments.Where(a => a.UserId == userId).ToList());

        public Task<IReadOnlyList<UserAccessGroupAssignment>> GetGroupUserAssignmentsAsync(Guid accessGroupId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserAccessGroupAssignment>>(Assignments.Where(a => a.AccessGroupId == accessGroupId).ToList());

        public Task SetUserGroupAssignmentsAsync(Guid userId, IReadOnlyList<Guid> accessGroupIds, Guid actorUserId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, int>> CountUserAssignmentsByGroupAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Portfolio>> GetAllPortfoliosAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task AddAccessGroupsAsync(IReadOnlyList<AccessGroup> groups, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
