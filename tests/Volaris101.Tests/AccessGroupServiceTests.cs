using Volaris101.Application.Interfaces;
using Volaris101.Application.Services;
using Volaris101.Domain.Entities;

namespace Volaris101.Tests;

public class AccessGroupServiceTests
{
    [Fact]
    public async Task SeedSystemManagedGroupsAsync_SeedsPortfolioGroups_WithGroupSuffix()
    {
        var repo = new FakeAccessGroupRepository
        {
            Portfolios =
            [
                new Portfolio { PortfolioId = 30350, PortfolioName = "Vencora" }
            ],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 30350, ReportToId = 30417, OrganizationName = "Vencora" }
            ]
        };
        var service = new AccessGroupService(repo);

        var result = await service.SeedSystemManagedGroupsAsync();

        Assert.Equal(1, result.PortfolioGroupsCreated);
        var group = Assert.Single(repo.Groups);
        Assert.Equal(AccessGroupTypes.PortfolioGroup, group.GroupTypeCode);
        Assert.Equal(30350, group.PortfolioId);
        Assert.Null(group.OrganizationId);
        Assert.Null(group.ParentPortfolioId);
        Assert.Equal("Vencora (Group)", group.Name);
        Assert.True(group.IsSystemManaged);
        Assert.True(group.IsActive);
    }

    [Fact]
    public async Task SeedSystemManagedGroupsAsync_SeedsEligibleSubGroups_WithSubGroupSuffixAndParentPortfolio()
    {
        var repo = new FakeAccessGroupRepository
        {
            Portfolios =
            [
                new Portfolio { PortfolioId = 30085, PortfolioName = "Omegro" }
            ],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 30085, ReportToId = 30417, OrganizationName = "Omegro" },
                new VbuMasterListRecord { OrganizationId = 30260, ReportToId = 30085, OrganizationName = "Andy Nelson Portfolio" },
                new VbuMasterListRecord { OrganizationId = 30129, ReportToId = 30260, OrganizationName = "Roddy MacLennan Group" },
                new VbuMasterListRecord { OrganizationId = 30562, ReportToId = 30129, OrganizationName = "tlmNexus" }
            ]
        };
        var service = new AccessGroupService(repo);

        var result = await service.SeedSystemManagedGroupsAsync();

        Assert.Equal(1, result.PortfolioGroupsCreated);
        Assert.Equal(2, result.PortfolioSubGroupsCreated);
        var subGroups = repo.Groups
            .Where(g => g.GroupTypeCode == AccessGroupTypes.PortfolioSubGroup)
            .OrderBy(g => g.OrganizationId)
            .ToList();
        Assert.Equal([30129, 30260], subGroups.Select(g => g.OrganizationId!.Value).ToArray());
        Assert.All(subGroups, g =>
        {
            Assert.Null(g.PortfolioId);
            Assert.Equal(30085, g.ParentPortfolioId);
            Assert.EndsWith(" (Sub-Group)", g.Name);
            Assert.True(g.IsSystemManaged);
            Assert.True(g.IsActive);
        });
    }

    [Fact]
    public async Task SeedSystemManagedGroupsAsync_DoesNotSeedSubGroup_WhenOrganizationIsPortfolioOrLeafOrOutsidePortfolio()
    {
        var repo = new FakeAccessGroupRepository
        {
            Portfolios =
            [
                new Portfolio { PortfolioId = 30085, PortfolioName = "Omegro" }
            ],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 30085, ReportToId = 0, OrganizationName = "Omegro" },
                new VbuMasterListRecord { OrganizationId = 30260, ReportToId = 30085, OrganizationName = "Andy Nelson Portfolio" },
                new VbuMasterListRecord { OrganizationId = 30129, ReportToId = 30260, OrganizationName = "Roddy MacLennan Group" },
                new VbuMasterListRecord { OrganizationId = 30562, ReportToId = 30129, OrganizationName = "tlmNexus" },
                new VbuMasterListRecord { OrganizationId = 40000, ReportToId = 0, OrganizationName = "Detached Parent" },
                new VbuMasterListRecord { OrganizationId = 40001, ReportToId = 40000, OrganizationName = "Detached Child" }
            ]
        };
        var service = new AccessGroupService(repo);

        await service.SeedSystemManagedGroupsAsync();

        var seededOrgIds = repo.Groups
            .Where(g => g.GroupTypeCode == AccessGroupTypes.PortfolioSubGroup)
            .Select(g => g.OrganizationId)
            .ToArray();
        Assert.Equal([30129, 30260], seededOrgIds.OrderBy(id => id).ToArray());
        Assert.DoesNotContain(30085, seededOrgIds);
        Assert.DoesNotContain(30562, seededOrgIds);
        Assert.DoesNotContain(40000, seededOrgIds);
    }

    [Fact]
    public async Task SeedSystemManagedGroupsAsync_IsIdempotent()
    {
        var repo = new FakeAccessGroupRepository
        {
            Portfolios =
            [
                new Portfolio { PortfolioId = 30085, PortfolioName = "Omegro" }
            ],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 30085, ReportToId = 0, OrganizationName = "Omegro" },
                new VbuMasterListRecord { OrganizationId = 30260, ReportToId = 30085, OrganizationName = "Andy Nelson Portfolio" },
                new VbuMasterListRecord { OrganizationId = 30129, ReportToId = 30260, OrganizationName = "Roddy MacLennan Group" }
            ]
        };
        var service = new AccessGroupService(repo);

        var first = await service.SeedSystemManagedGroupsAsync();
        var second = await service.SeedSystemManagedGroupsAsync();

        Assert.Equal(1, first.PortfolioGroupsCreated);
        Assert.Equal(1, first.PortfolioSubGroupsCreated);
        Assert.Equal(0, second.PortfolioGroupsCreated);
        Assert.Equal(0, second.PortfolioSubGroupsCreated);
        Assert.Equal(2, repo.Groups.Count);
    }

    [Fact]
    public async Task ResolveEffectiveOrganizationIdsAsync_ExpandsPortfolioGroupDescendants()
    {
        var groupId = Guid.NewGuid();
        var repo = new FakeAccessGroupRepository
        {
            Groups =
            [
                new AccessGroup
                {
                    AccessGroupId = groupId,
                    GroupTypeCode = AccessGroupTypes.PortfolioGroup,
                    PortfolioId = 30085,
                    IsActive = true
                }
            ],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 30085, ReportToId = 0, OrganizationName = "Omegro" },
                new VbuMasterListRecord { OrganizationId = 30260, ReportToId = 30085, OrganizationName = "Andy Nelson Portfolio" },
                new VbuMasterListRecord { OrganizationId = 30129, ReportToId = 30260, OrganizationName = "Roddy MacLennan Group" }
            ]
        };
        var service = new AccessGroupService(repo);

        var scope = await service.ResolveEffectiveOrganizationIdsAsync(groupId);

        Assert.Equal([30085, 30129, 30260], scope.OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task ResolveEffectiveOrganizationIdsAsync_ExpandsPortfolioSubGroupDescendants()
    {
        var groupId = Guid.NewGuid();
        var repo = new FakeAccessGroupRepository
        {
            Groups =
            [
                new AccessGroup
                {
                    AccessGroupId = groupId,
                    GroupTypeCode = AccessGroupTypes.PortfolioSubGroup,
                    OrganizationId = 30260,
                    ParentPortfolioId = 30085,
                    IsActive = true
                }
            ],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 30085, ReportToId = 0, OrganizationName = "Omegro" },
                new VbuMasterListRecord { OrganizationId = 30260, ReportToId = 30085, OrganizationName = "Andy Nelson Portfolio" },
                new VbuMasterListRecord { OrganizationId = 30129, ReportToId = 30260, OrganizationName = "Roddy MacLennan Group" },
                new VbuMasterListRecord { OrganizationId = 30562, ReportToId = 30129, OrganizationName = "tlmNexus" }
            ]
        };
        var service = new AccessGroupService(repo);

        var scope = await service.ResolveEffectiveOrganizationIdsAsync(groupId);

        Assert.Equal([30129, 30260, 30562], scope.OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task SetUserGroupAssignmentsAsync_RejectsInactiveGroups()
    {
        var groupId = Guid.NewGuid();
        var repo = new FakeAccessGroupRepository
        {
            Groups =
            [
                new AccessGroup
                {
                    AccessGroupId = groupId,
                    Name = "Omegro (Group)",
                    GroupTypeCode = AccessGroupTypes.PortfolioGroup,
                    PortfolioId = 30085,
                    IsActive = false
                }
            ]
        };
        var service = new AccessGroupService(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SetUserGroupAssignmentsAsync(Guid.NewGuid(), [groupId], Guid.NewGuid()));
    }

    [Fact]
    public async Task SetUserGroupAssignmentsAsync_SavesPortfolioGroups()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repo = new FakeAccessGroupRepository
        {
            Groups =
            [
                new AccessGroup
                {
                    AccessGroupId = groupId,
                    Name = "Omegro (Group)",
                    GroupTypeCode = AccessGroupTypes.PortfolioGroup,
                    PortfolioId = 30085,
                    IsActive = true
                }
            ],
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 30085, ReportToId = 0, OrganizationName = "Omegro" },
                new VbuMasterListRecord { OrganizationId = 30260, ReportToId = 30085, OrganizationName = "Andy Nelson Portfolio" }
            ]
        };
        var service = new AccessGroupService(repo);

        var result = await service.SetUserGroupAssignmentsAsync(userId, [groupId], actorId);

        var assignment = Assert.Single(repo.Assignments);
        Assert.Equal(userId, assignment.UserId);
        Assert.Equal(groupId, assignment.AccessGroupId);
        Assert.Equal(actorId, assignment.AssignedByUserId);
        Assert.Equal(groupId, Assert.Single(result).AccessGroupId);
        Assert.Equal(2, result[0].EffectiveOrganizationCount);
    }

    private sealed class FakeAccessGroupRepository : IAccessGroupRepository
    {
        public IReadOnlyList<Portfolio> Portfolios { get; set; } = [];
        public IReadOnlyList<VbuMasterListRecord> VbuRows { get; set; } = [];
        public List<AccessGroup> Groups { get; set; } = [];
        public List<UserAccessGroupAssignment> Assignments { get; } = [];

        public Task<IReadOnlyList<AccessGroup>> GetAccessGroupsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AccessGroup>>(Groups.ToList());

        public Task<AccessGroup?> GetAccessGroupByIdAsync(Guid accessGroupId, CancellationToken ct = default) =>
            Task.FromResult(Groups.FirstOrDefault(g => g.AccessGroupId == accessGroupId));

        public Task<IReadOnlyList<AccessGroup>> GetAccessGroupsByIdsAsync(IReadOnlyCollection<Guid> accessGroupIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AccessGroup>>(Groups.Where(g => accessGroupIds.Contains(g.AccessGroupId)).ToList());

        public Task UpdateAccessGroupAsync(AccessGroup group, CancellationToken ct = default)
        {
            var index = Groups.FindIndex(g => g.AccessGroupId == group.AccessGroupId);
            if (index >= 0)
                Groups[index] = group;
            else
                Groups.Add(group);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserAccessGroupAssignment>> GetUserGroupAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserAccessGroupAssignment>>(Assignments.Where(a => a.UserId == userId).ToList());

        public Task<IReadOnlyList<UserAccessGroupAssignment>> GetGroupUserAssignmentsAsync(Guid accessGroupId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserAccessGroupAssignment>>(Assignments.Where(a => a.AccessGroupId == accessGroupId).ToList());

        public Task SetUserGroupAssignmentsAsync(Guid userId, IReadOnlyList<Guid> accessGroupIds, Guid actorUserId, CancellationToken ct = default)
        {
            Assignments.RemoveAll(a => a.UserId == userId);
            Assignments.AddRange(accessGroupIds.Select(accessGroupId => new UserAccessGroupAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = userId,
                AccessGroupId = accessGroupId,
                AssignedAt = DateTimeOffset.UtcNow,
                AssignedByUserId = actorUserId
            }));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<Guid, int>> CountUserAssignmentsByGroupAsync(CancellationToken ct = default)
        {
            IReadOnlyDictionary<Guid, int> counts = Assignments
                .GroupBy(a => a.AccessGroupId)
                .ToDictionary(g => g.Key, g => g.Count());
            return Task.FromResult(counts);
        }

        public Task<IReadOnlyList<Portfolio>> GetAllPortfoliosAsync(CancellationToken ct = default) =>
            Task.FromResult(Portfolios);

        public Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) =>
            Task.FromResult(VbuRows);

        public Task AddAccessGroupsAsync(IReadOnlyList<AccessGroup> groups, CancellationToken ct = default)
        {
            Groups.AddRange(groups);
            return Task.CompletedTask;
        }
    }
}
