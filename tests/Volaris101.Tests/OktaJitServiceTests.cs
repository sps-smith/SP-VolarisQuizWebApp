using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;
using Volaris101.Web.Auth;

namespace Volaris101.Tests;

public class OktaJitServiceTests
{
    [Fact]
    public async Task HandleSignInAsync_MapsConfiguredPortfolio_WhenCreatingUser()
    {
        var repo = new RecordingUserRepository();
        var service = CreateService(repo);
        var principal = CreatePrincipal(portfolio: "Rob Turner Portfolio");

        var result = await service.HandleSignInAsync(principal);

        Assert.Null(result);
        Assert.Single(repo.CreatedUsers);
        Assert.Equal("Robert Turner Portfolio", repo.CreatedUsers[0].Portfolio);
        Assert.Equal(77, repo.CreatedUsers[0].PortfolioId);
    }

    [Fact]
    public async Task HandleSignInAsync_MapsConfiguredPortfolio_WhenUpdatingUser()
    {
        var existing = CreateUser();
        var repo = new RecordingUserRepository(existing);
        var service = CreateService(repo);
        var principal = CreatePrincipal(portfolio: "Rob Turner Portfolio");

        var result = await service.HandleSignInAsync(principal);

        Assert.Null(result);
        Assert.Single(repo.UpdatedUsers);
        Assert.Equal("Robert Turner Portfolio", repo.UpdatedUsers[0].Portfolio);
        Assert.Equal(77, repo.UpdatedUsers[0].PortfolioId);
    }

    [Fact]
    public async Task HandleSignInAsync_PreservesUnmappedPortfolioValue()
    {
        var repo = new RecordingUserRepository();
        var service = CreateService(repo);
        var principal = CreatePrincipal(portfolio: "Unmapped Portfolio");

        var result = await service.HandleSignInAsync(principal);

        Assert.Null(result);
        Assert.Single(repo.CreatedUsers);
        Assert.Equal("Unmapped Portfolio", repo.CreatedUsers[0].Portfolio);
        Assert.Null(repo.CreatedUsers[0].PortfolioId);
    }

    [Fact]
    public async Task HandleSignInAsync_LeavesExistingPortfolioId_WhenPortfolioNameIsNotInDatabase()
    {
        var existing = CreateUser();
        existing.PortfolioId = 42;
        var repo = new RecordingUserRepository(existing);
        var service = CreateService(repo, []);

        var result = await service.HandleSignInAsync(CreatePrincipal("Unknown Portfolio"));

        Assert.Null(result);
        Assert.Equal(42, repo.UpdatedUsers.Single().PortfolioId);
    }

    private static OktaJitService CreateService(RecordingUserRepository repo, IReadOnlyList<Portfolio>? portfolios = null)
    {
        var options = Options.Create(new OktaJitOptions
        {
            PortfolioMappings =
            {
                ["Rob Turner Portfolio"] = "Robert Turner Portfolio"
            }
        });

        return new OktaJitService(
            repo,
            new FakeReportingAccessRepository(portfolios ?? [new Portfolio { PortfolioId = 77, PortfolioName = "Robert Turner Portfolio" }]),
            new NoOpAuditLogRepository(),
            NullLogger<OktaJitService>.Instance,
            options);
    }

    private static ClaimsPrincipal CreatePrincipal(string portfolio)
    {
        var claims = new[]
        {
            new Claim("sub", "okta-user-1"),
            new Claim("email", "jasmine@example.com"),
            new Claim("given_name", "Jasmine"),
            new Claim("family_name", "Test"),
            new Claim("EmployeeId", "12345"),
            new Claim("ValidEmail", "true"),
            new Claim("VBUName", "Aion"),
            new Claim("Portfolio", portfolio),
            new Claim("VBUID", "30300")
        };

        var identity = new ClaimsIdentity(claims, "oidc");
        return new ClaimsPrincipal(identity);
    }

    private static User CreateUser() =>
        new()
        {
            UserId = Guid.NewGuid(),
            Login = "okta-user-1",
            Email = "jasmine@example.com",
            FirstName = "Jasmine",
            LastName = "Test",
            EmployeeId = "12345",
            Company = "Aion",
            Portfolio = "Wrong Portfolio",
            OrganizationId = 30300,
            PortfolioId = 77,
            RoleCode = "Learner",
            IsActive = true,
            AccountCreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastLoginAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastAttributeRefreshAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

    private sealed class RecordingUserRepository : IUserRepository
    {
        private readonly User? _existing;

        public RecordingUserRepository(User? existing = null)
        {
            _existing = existing;
        }

        public List<User> CreatedUsers { get; } = [];
        public List<User> UpdatedUsers { get; } = [];

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByLoginAsync(string login, CancellationToken ct = default) => Task.FromResult(_existing);
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(_existing);
        public Task<User?> GetByEmployeeIdAsync(string employeeId, CancellationToken ct = default) => Task.FromResult(_existing);
        public Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task<IReadOnlyList<User>> GetAllActiveAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task<IReadOnlyList<User>> SearchActiveByPrefixAsync(string? searchTerm, int? take = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task<int> CountActiveAsync(string? searchTerm = null, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<User>> GetActiveAsync(string? searchTerm = null, int? take = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task<IReadOnlyList<User>> GetFilteredReportingUsersAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, int? take = null, CancellationToken ct = default, Guid? includeUserId = null) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetFilteredReportingUserIdsAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountFilteredReportingUsersAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, ReportingFilterDto filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetDistinctReportingDomainsAsync(bool isGlobalAdmin, IReadOnlyCollection<int>? scopeOrganizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompanyGroupDto>> GetAvailableReportingCompaniesAsync(IReadOnlyCollection<int>? scopeOrganizationIds, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<User> CreateAsync(User user, CancellationToken ct = default)
        {
            CreatedUsers.Add(user);
            return Task.FromResult(user);
        }

        public Task<User> UpdateAsync(User user, CancellationToken ct = default)
        {
            UpdatedUsers.Add(user);
            return Task.FromResult(user);
        }
    }

    private sealed class NoOpAuditLogRepository : IAuditLogRepository
    {
        public Task WriteAsync(string actionType, string outcome, Guid? actorUserId, string actorType, Guid? targetUserId, string? beforeValue, string? afterValue, string? sourceComponent, string? denialReason = null, string? ipAddress = null, string? additionalDataJson = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeReportingAccessRepository : IReportingAccessRepository
    {
        private readonly IReadOnlyList<Portfolio> _portfolios;

        public FakeReportingAccessRepository(IReadOnlyList<Portfolio> portfolios) => _portfolios = portfolios;

        public Task<Portfolio?> GetPortfolioByNameAsync(string portfolioName, CancellationToken ct = default) =>
            Task.FromResult<Portfolio?>(_portfolios.FirstOrDefault(p => string.Equals(p.PortfolioName, portfolioName.Trim(), StringComparison.OrdinalIgnoreCase)));
        public Task<IReadOnlyList<OrgAdminAssignment>> GetOrgAssignmentsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PortfolioAdminAssignment>> GetPortfolioAssignmentsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetOrgAssignmentsAsync(Guid userId, IReadOnlyList<int> organizationIds, Guid actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetPortfolioAssignmentsAsync(Guid userId, IReadOnlyList<int> portfolioIds, Guid actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> SearchOrganizationsAsync(string? searchTerm, int take = 25, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> SearchPortfoliosAsync(string? searchTerm, int take = 25, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> GetOrganizationsByIdsAsync(IReadOnlyCollection<int> organizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> GetPortfoliosByIdsAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> GetOrganizationIdsForPortfoliosAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
