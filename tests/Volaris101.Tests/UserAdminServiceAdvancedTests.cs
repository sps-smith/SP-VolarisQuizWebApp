using Microsoft.Extensions.Caching.Memory;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Application.Services;
using Volaris101.Domain.Entities;

namespace Volaris101.Tests;

public class UserAdminServiceAdvancedTests
{
    [Fact]
    public async Task AddUserAsync_RejectsInvalidRoleCode()
    {
        var service = CreateService(new TrackingUserRepository(), new TrackingReportingAccessRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddUserAsync(new AddUserRequestDto("Jasmine", "Test", "jasmine@example.com", "12345", "VBU", 30300, "Portfolio", null, "BadRole"), Guid.NewGuid()));
    }

    [Fact]
    public async Task AddUserAsync_WhenCompanySelectionIsPortfolio_SavesSamePortfolioValues()
    {
        var userRepo = new TrackingUserRepository();
        var accessRepo = new TrackingReportingAccessRepository
        {
            Portfolios =
            [
                new Portfolio { PortfolioId = 30085, PortfolioName = "Omegro" }
            ]
        };
        var service = CreateService(userRepo, accessRepo);

        var result = await service.AddUserAsync(
            new AddUserRequestDto("Pia", "Portfolio", "pia@example.com", "24680", "ignored", 30085, "ignored", null, "Learner"),
            Guid.NewGuid());

        var user = Assert.Single(userRepo.Users.Values);
        Assert.Equal("Omegro", user.Company);
        Assert.Equal(30085, user.OrganizationId);
        Assert.Equal("Omegro", user.Portfolio);
        Assert.Equal(30085, user.PortfolioId);
        Assert.Equal("Omegro", result.Company);
        Assert.Equal(30085, result.OrganizationId);
        Assert.Equal("Omegro", result.Portfolio);
        Assert.Equal(30085, result.PortfolioId);
    }

    [Fact]
    public async Task UpdateAccessAsync_RejectsOrgAdminWithoutOrganizations()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserRepository(CreateUser(userId));
        var service = CreateService(repo, new TrackingReportingAccessRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAccessAsync(userId, new UpdateUserAccessRequestDto("OrgAdmin", [], null), Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAccessAsync_PersistsOnlyPortfolioAssignments_ForPortfolioAdmin()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserRepository(CreateUser(userId));
        var accessRepo = new TrackingReportingAccessRepository
        {
        };
        accessRepo.PortfoliosById[7] = new Portfolio { PortfolioId = 7, PortfolioName = "North" };
        accessRepo.PortfoliosById[8] = new Portfolio { PortfolioId = 8, PortfolioName = "South" };
        var service = CreateService(repo, accessRepo);

        var result = await service.UpdateAccessAsync(
            userId,
            new UpdateUserAccessRequestDto("PortfolioAdmin", [1, 2], [7, 7, 0, 8]),
            Guid.NewGuid());

        Assert.Equal("PortfolioAdmin", repo.Users[userId].RoleCode);
        Assert.Empty(accessRepo.LastOrgAssignments);
        Assert.Equal(new[] { 7, 8 }, accessRepo.LastPortfolioAssignments);
        Assert.Equal("PortfolioAdmin", result.RoleCode);
        Assert.Equal(new[] { 7, 8 }, result.AssignedPortfolios.Select(p => p.Id).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task SearchOrganizationsAsync_FormatsOrganizationLabels()
    {
        var accessRepo = new TrackingReportingAccessRepository
        {
            Organizations =
            [
                new Organization { OrganizationId = 30300, OrganizationName = "Aion" }
            ]
        };
        var service = CreateService(new TrackingUserRepository(), accessRepo);

        var result = await service.SearchOrganizationsAsync("aio");

        Assert.Single(result);
        Assert.Equal(new AccessOptionDto(30300, "Aion (30300)"), result[0]);
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsAdminRoles_AndSearchesAllUsers()
    {
        var learner = CreateUser(Guid.NewGuid(), "Alex", "Learner", "alex.learner@example.com", true);
        var inactiveUser = CreateUser(Guid.NewGuid(), "Mia", "Inactive", "mia.inactive@example.com", false);
        inactiveUser.RoleCode = "OrgAdmin";
        var portfolioAdmin = CreateUser(Guid.NewGuid(), "Pia", "Portfolio", "pia.portfolio@example.com", true);
        portfolioAdmin.RoleCode = "PortfolioAdmin";

        var repo = new TrackingUserRepository(learner, inactiveUser, portfolioAdmin);
        var accessRepo = new TrackingReportingAccessRepository();
        accessRepo.AssignmentsByUser[inactiveUser.UserId] = [
            new OrgAdminAssignment { OrganizationId = 30300 }
        ];
        accessRepo.OrganizationsById[30300] = new Organization { OrganizationId = 30300, OrganizationName = "Aion" };

        var service = CreateService(repo, accessRepo);

        var allUsers = await service.GetUsersAsync(null, 25);
        Assert.Equal(2, allUsers.Count);
        Assert.DoesNotContain(allUsers, u => u.RoleCode == "Learner");
        Assert.Contains(allUsers, u => u.UserId == inactiveUser.UserId);

        var users = await service.GetUsersAsync("alex", 25);

        Assert.Single(users);
        Assert.Equal("Alex Learner", users[0].FullName);
        Assert.True(users[0].IsActive);
        Assert.Equal("Learner", users[0].RoleCode);
    }

    private static UserAdminService CreateService(TrackingUserRepository userRepo, TrackingReportingAccessRepository accessRepo) =>
        new(userRepo, new NoOpMergeRepository(), accessRepo, new NoOpAuditLogRepository(), new MemoryCache(new MemoryCacheOptions()));

    private static User CreateUser(Guid userId) =>
        CreateUser(userId, "Jasmine", "Test", "jasmine@example.com", true);

    private static User CreateUser(Guid userId, string firstName, string lastName, string email, bool isActive) =>
        new()
        {
            UserId = userId,
            Login = email,
            IdpSubjectIdentifier = "sub",
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            EmployeeId = "123",
            Company = "VBU",
            Portfolio = "Portfolio",
            RoleCode = "Learner",
            IsActive = isActive,
            AccountCreatedAt = DateTimeOffset.UtcNow
        };

    private sealed class TrackingUserRepository : IUserRepository
    {
        public Dictionary<Guid, User> Users { get; } = new();

        public TrackingUserRepository(params User[] users)
        {
            foreach (var user in users)
                Users[user.UserId] = user;
        }

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.TryGetValue(userId, out var user) ? user : null);

        public Task<User?> GetByLoginAsync(string login, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(Users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
        public Task<User?> GetByEmployeeIdAsync(string employeeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetAllActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> SearchActiveByPrefixAsync(string? searchTerm, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountActiveAsync(string? searchTerm = null, CancellationToken ct = default)
        {
            var query = Users.Values.AsQueryable();
            if (string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(u => u.RoleCode == "OrgAdmin" || u.RoleCode == "PortfolioAdmin" || u.RoleCode == "GlobalAdmin");
            else
            {
                var term = searchTerm.Trim();
                query = query.Where(u => (u.FirstName + " " + u.LastName).Contains(term) || u.Email.Contains(term));
            }
            return Task.FromResult(query.Count());
        }
        public Task<IReadOnlyList<User>> GetActiveAsync(string? searchTerm = null, int? take = null, CancellationToken ct = default)
        {
            var query = Users.Values.AsQueryable();
            if (string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(u => u.RoleCode == "OrgAdmin" || u.RoleCode == "PortfolioAdmin" || u.RoleCode == "GlobalAdmin");
            else
            {
                var term = searchTerm.Trim();
                query = query.Where(u => (u.FirstName + " " + u.LastName).Contains(term) || u.Email.Contains(term));
            }
            query = query.OrderByDescending(u => u.IsActive).ThenBy(u => u.LastName).ThenBy(u => u.FirstName);
            if (take.HasValue && take.Value > 0)
                query = query.Take(take.Value);
            return Task.FromResult<IReadOnlyList<User>>(query.ToList());
        }
        public Task<IReadOnlyList<User>> GetFilteredReportingUsersAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, int? take = null, CancellationToken ct = default, Guid? includeUserId = null) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetFilteredReportingUserIdsAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountFilteredReportingUsersAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetDistinctReportingDomainsAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompanyGroupDto>> GetAvailableReportingCompaniesAsync(IReadOnlyCollection<int>? scopeOrganizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User> CreateAsync(User user, CancellationToken ct = default)
        {
            Users[user.UserId] = user;
            return Task.FromResult(user);
        }
        public Task<User> UpdateAsync(User user, CancellationToken ct = default)
        {
            Users[user.UserId] = user;
            return Task.FromResult(user);
        }
    }

    private sealed class TrackingReportingAccessRepository : IReportingAccessRepository
    {
        public List<int> LastOrgAssignments { get; private set; } = [];
        public List<int> LastPortfolioAssignments { get; private set; } = [];
        public Dictionary<Guid, IReadOnlyList<OrgAdminAssignment>> AssignmentsByUser { get; } = new();
        public Dictionary<Guid, IReadOnlyList<PortfolioAdminAssignment>> PortfolioAssignmentsByUser { get; } = new();
        public IReadOnlyList<Organization> Organizations { get; set; } = [];
        public IReadOnlyList<Portfolio> Portfolios { get; set; } = [];
        public Dictionary<int, Organization> OrganizationsById { get; } = new();
        public Dictionary<int, Portfolio> PortfoliosById { get; } = new();

        public Task<IReadOnlyList<OrgAdminAssignment>> GetOrgAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OrgAdminAssignment>>(
                AssignmentsByUser.TryGetValue(userId, out var assignments)
                    ? assignments
                    : LastOrgAssignments.Select(id => new OrgAdminAssignment { OrganizationId = id }).ToList());

        public Task<IReadOnlyList<PortfolioAdminAssignment>> GetPortfolioAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PortfolioAdminAssignment>>(
                PortfolioAssignmentsByUser.TryGetValue(userId, out var assignments)
                    ? assignments
                    : LastPortfolioAssignments.Select(id => new PortfolioAdminAssignment { PortfolioId = id }).ToList());

        public Task SetOrgAssignmentsAsync(Guid userId, IReadOnlyList<int> organizationIds, Guid actorUserId, CancellationToken ct = default)
        {
            LastOrgAssignments = organizationIds.ToList();
            return Task.CompletedTask;
        }

        public Task SetPortfolioAssignmentsAsync(Guid userId, IReadOnlyList<int> portfolioIds, Guid actorUserId, CancellationToken ct = default)
        {
            LastPortfolioAssignments = portfolioIds.ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Organization>> SearchOrganizationsAsync(string? searchTerm, int take = 25, CancellationToken ct = default) =>
            Task.FromResult(Organizations);

        public Task<IReadOnlyList<Portfolio>> SearchPortfoliosAsync(string? searchTerm, int take = 25, CancellationToken ct = default) =>
            Task.FromResult(Portfolios);
        public Task<Portfolio?> GetPortfolioByNameAsync(string portfolioName, CancellationToken ct = default) =>
            Task.FromResult<Portfolio?>(Portfolios.FirstOrDefault(p => string.Equals(p.PortfolioName, portfolioName, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<Organization>> GetOrganizationsByIdsAsync(IReadOnlyCollection<int> organizationIds, CancellationToken ct = default)
        {
            var items = organizationIds
                .Select(id => OrganizationsById.TryGetValue(id, out var organization) ? organization : null)
                .Where(organization => organization is not null)
                .Cast<Organization>()
                .ToList();
            return Task.FromResult<IReadOnlyList<Organization>>(items);
        }

        public Task<IReadOnlyList<Portfolio>> GetPortfoliosByIdsAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default)
        {
            var items = portfolioIds
                .Select(id => PortfoliosById.TryGetValue(id, out var portfolio) ? portfolio : null)
                .Where(portfolio => portfolio is not null)
                .Cast<Portfolio>()
                .ToList();
            return Task.FromResult<IReadOnlyList<Portfolio>>(items);
        }

        public Task<IReadOnlyList<int>> GetOrganizationIdsForPortfoliosAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(portfolioIds.ToList());

        public Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<VbuMasterListRecord>>([]);
    }

    private sealed class NoOpMergeRepository : IUserMergeRepository
    {
        public Task<MergeUsersStatisticsDto> GetMergeStatisticsAsync(IReadOnlyCollection<Guid> sourceUserIds, Guid targetUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MergeUsersResultDto> ExecuteMergeAsync(IReadOnlyCollection<Guid> sourceUserIds, Guid targetUserId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoOpAuditLogRepository : IAuditLogRepository
    {
        public Task WriteAsync(string actionType, string outcome, Guid? actorUserId, string actorType, Guid? targetUserId, string? beforeValue, string? afterValue, string? sourceComponent, string? denialReason = null, string? ipAddress = null, string? additionalDataJson = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
