using Microsoft.Extensions.Caching.Memory;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Application.Services;
using Volaris101.Domain.Entities;

namespace Volaris101.Tests;

public class UserAdminServiceTests
{
    [Fact]
    public async Task UpdateAccessAsync_RejectsInvalidRole()
    {
        var service = CreateService(new User(), new InMemoryUserRepository(new User()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAccessAsync(Guid.NewGuid(), new UpdateUserAccessRequestDto("NotARole", null, null), Guid.NewGuid()));
    }

    [Fact]
    public async Task SetActiveStatusAsync_UpdatesUserStatus()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Login = "jasmine@example.com",
            IdpSubjectIdentifier = "sub",
            FirstName = "Jasmine",
            LastName = "Test",
            Email = "jasmine@example.com",
            EmployeeId = "123",
            RoleCode = "Learner",
            IsActive = true,
            AccountCreatedAt = DateTimeOffset.UtcNow
        };
        var repo = new InMemoryUserRepository(user);
        var service = CreateService(user, repo);

        var result = await service.SetActiveStatusAsync(userId, false, Guid.NewGuid());

        Assert.False(repo.Users[userId].IsActive);
        Assert.False(result.IsActive);
    }

    private static UserAdminService CreateService(User currentUser, InMemoryUserRepository userRepo)
    {
        return new UserAdminService(
            userRepo,
            new NoOpMergeRepository(),
            new NoOpReportingAccessRepository(),
            new NoOpAuditLogRepository(),
            new MemoryCache(new MemoryCacheOptions()));
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public Dictionary<Guid, User> Users { get; } = new();

        public InMemoryUserRepository(params User[] users)
        {
            foreach (var user in users)
                Users[user.UserId] = user;
        }

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.TryGetValue(userId, out var user) ? user : null);

        public Task<User?> GetByLoginAsync(string login, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmployeeIdAsync(string employeeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetAllActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> SearchActiveByPrefixAsync(string? searchTerm, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountActiveAsync(string? searchTerm = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetActiveAsync(string? searchTerm = null, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetFilteredReportingUsersAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, int? take = null, CancellationToken ct = default, Guid? includeUserId = null) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetFilteredReportingUserIdsAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountFilteredReportingUsersAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetDistinctReportingDomainsAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompanyGroupDto>> GetAvailableReportingCompaniesAsync(IReadOnlyCollection<int>? scopeOrganizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User> CreateAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User> UpdateAsync(User user, CancellationToken ct = default)
        {
            Users[user.UserId] = user;
            return Task.FromResult(user);
        }
    }

    private sealed class NoOpMergeRepository : IUserMergeRepository
    {
        public Task<MergeUsersStatisticsDto> GetMergeStatisticsAsync(IReadOnlyCollection<Guid> sourceUserIds, Guid targetUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MergeUsersResultDto> ExecuteMergeAsync(IReadOnlyCollection<Guid> sourceUserIds, Guid targetUserId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoOpReportingAccessRepository : IReportingAccessRepository
    {
        public Task<IReadOnlyList<OrgAdminAssignment>> GetOrgAssignmentsAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OrgAdminAssignment>>([]);
        public Task<IReadOnlyList<PortfolioAdminAssignment>> GetPortfolioAssignmentsAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PortfolioAdminAssignment>>([]);
        public Task SetOrgAssignmentsAsync(Guid userId, IReadOnlyList<int> organizationIds, Guid actorUserId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetPortfolioAssignmentsAsync(Guid userId, IReadOnlyList<int> portfolioIds, Guid actorUserId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Organization>> SearchOrganizationsAsync(string? searchTerm, int take = 25, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Organization>>([]);
        public Task<IReadOnlyList<Portfolio>> SearchPortfoliosAsync(string? searchTerm, int take = 25, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Portfolio>>([]);
        public Task<Portfolio?> GetPortfolioByNameAsync(string portfolioName, CancellationToken ct = default) => Task.FromResult<Portfolio?>(null);
        public Task<IReadOnlyList<Organization>> GetOrganizationsByIdsAsync(IReadOnlyCollection<int> organizationIds, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Organization>>([]);
        public Task<IReadOnlyList<Portfolio>> GetPortfoliosByIdsAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Portfolio>>([]);
        public Task<IReadOnlyList<int>> GetOrganizationIdsForPortfoliosAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([]);
        public Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<VbuMasterListRecord>>([]);
    }

    private sealed class NoOpAuditLogRepository : IAuditLogRepository
    {
        public Task WriteAsync(string actionType, string outcome, Guid? actorUserId, string actorType, Guid? targetUserId, string? beforeValue, string? afterValue, string? sourceComponent, string? denialReason = null, string? ipAddress = null, string? additionalDataJson = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}
