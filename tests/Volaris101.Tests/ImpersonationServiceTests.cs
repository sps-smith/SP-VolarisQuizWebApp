using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;
using Volaris101.Web.Auth;
using Microsoft.Extensions.Logging;

namespace Volaris101.Tests;

public class ImpersonationServiceTests
{
    [Fact]
    public async Task StartAsync_ReplacesEffectiveClaims_AndKeepsOriginalAdminClaims()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var accessor = CreateAccessor(CreatePrincipal(
            userId: adminId,
            login: "admin.login",
            email: "admin@example.com",
            fullName: "Admin Person",
            role: "GlobalAdmin",
            preferredLanguage: "en"));
        var currentUser = new CurrentUserContext(accessor);
        var repo = new FakeUserRepository(new User
        {
            UserId = targetId,
            Login = "learner.login",
            Email = "learner@example.com",
            FirstName = "Learner",
            LastName = "Person",
            RoleCode = "Learner",
            PreferredLanguage = "fr",
            IsActive = true
        });
        var service = CreateService(accessor, currentUser, repo);

        var result = await service.StartAsync(targetId, "/admin/users?search=learner");

        Assert.Equal("Learner Person", result.TargetFullName);
        Assert.Equal("/learner/dashboard", result.RedirectUrl);

        var principal = result.Principal;
        Assert.Equal(targetId.ToString(), principal.FindFirstValue("userId"));
        Assert.Equal("learner.login", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("learner.login", principal.FindFirstValue("sub"));
        Assert.Equal("learner@example.com", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("learner@example.com", principal.FindFirstValue("email"));
        Assert.Equal("Learner Person", principal.FindFirstValue("displayName"));
        Assert.Equal("LP", principal.FindFirstValue("initials"));
        Assert.Equal("Learner", principal.FindFirstValue("role"));
        Assert.Equal("fr", principal.FindFirstValue("preferredLanguage"));
        Assert.Equal(adminId.ToString(), principal.FindFirstValue(ImpersonationClaimTypes.OriginalUserId));
        Assert.Equal("admin.login", principal.FindFirstValue(ImpersonationClaimTypes.OriginalLogin));
        Assert.Equal("admin@example.com", principal.FindFirstValue(ImpersonationClaimTypes.OriginalEmail));
        Assert.Equal("Admin Person", principal.FindFirstValue(ImpersonationClaimTypes.OriginalFullName));
        Assert.Equal("GlobalAdmin", principal.FindFirstValue(ImpersonationClaimTypes.OriginalRoleCode));
        Assert.Equal("en", principal.FindFirstValue(ImpersonationClaimTypes.OriginalPreferredLanguage));
        Assert.Equal("Admin Person", principal.FindFirstValue(ImpersonationClaimTypes.OriginalDisplayName));
        Assert.Equal("AP", principal.FindFirstValue(ImpersonationClaimTypes.OriginalInitials));
        Assert.Equal("/admin/users?search=learner", principal.FindFirstValue(ImpersonationClaimTypes.ReturnUrl));
    }

    [Fact]
    public async Task StopAsync_RestoresOriginalClaims_AndReturnUrl()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var accessor = CreateAccessor(CreatePrincipal(
            userId: targetId,
            login: "learner.login",
            email: "learner@example.com",
            fullName: "Learner Person",
            role: "Learner",
            preferredLanguage: "fr",
            extras:
            [
                new Claim(ImpersonationClaimTypes.OriginalUserId, adminId.ToString()),
                new Claim(ImpersonationClaimTypes.OriginalLogin, "admin.login"),
                new Claim(ImpersonationClaimTypes.OriginalEmail, "admin@example.com"),
                new Claim(ImpersonationClaimTypes.OriginalFullName, "Admin Person"),
                new Claim(ImpersonationClaimTypes.OriginalRoleCode, "GlobalAdmin"),
                new Claim(ImpersonationClaimTypes.OriginalPreferredLanguage, "en"),
                new Claim(ImpersonationClaimTypes.OriginalDisplayName, "Admin Person"),
                new Claim(ImpersonationClaimTypes.OriginalInitials, "AP"),
                new Claim(ImpersonationClaimTypes.ReturnUrl, "/admin/users")
            ]));
        var currentUser = new CurrentUserContext(accessor);
        var repo = new FakeUserRepository();
        var service = CreateService(accessor, currentUser, repo);

        var result = await service.StopAsync();

        Assert.Equal("/admin/users", result.RedirectUrl);
        var principal = result.Principal;
        Assert.Equal(adminId.ToString(), principal.FindFirstValue("userId"));
        Assert.Equal("admin.login", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("admin.login", principal.FindFirstValue("sub"));
        Assert.Equal("admin@example.com", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("admin@example.com", principal.FindFirstValue("email"));
        Assert.Equal("Admin Person", principal.FindFirstValue("displayName"));
        Assert.Equal("AP", principal.FindFirstValue("initials"));
        Assert.Equal("GlobalAdmin", principal.FindFirstValue("role"));
        Assert.Equal("en", principal.FindFirstValue("preferredLanguage"));
        Assert.Null(principal.FindFirstValue(ImpersonationClaimTypes.OriginalUserId));
        Assert.Null(principal.FindFirstValue(ImpersonationClaimTypes.ReturnUrl));
    }

    [Fact]
    public void CurrentUserContext_RecognizesImpersonationClaims()
    {
        var accessor = CreateAccessor(CreatePrincipal(
            userId: Guid.NewGuid(),
            login: "learner.login",
            email: "learner@example.com",
            fullName: "Learner Person",
            role: "Learner",
            preferredLanguage: "fr",
            extras:
            [
                new Claim(ImpersonationClaimTypes.OriginalUserId, Guid.NewGuid().ToString()),
                new Claim(ImpersonationClaimTypes.OriginalLogin, "admin.login"),
                new Claim(ImpersonationClaimTypes.OriginalEmail, "admin@example.com"),
                new Claim(ImpersonationClaimTypes.OriginalFullName, "Admin Person"),
                new Claim(ImpersonationClaimTypes.OriginalRoleCode, "GlobalAdmin"),
                new Claim(ImpersonationClaimTypes.OriginalPreferredLanguage, "en")
            ]));

        var ctx = new CurrentUserContext(accessor);

        Assert.True(ctx.IsImpersonating);
        Assert.NotEqual(Guid.Empty, ctx.ImpersonatorUserId);
        Assert.Equal("admin.login", ctx.ImpersonatorLogin);
        Assert.Equal("admin@example.com", ctx.ImpersonatorEmail);
        Assert.Equal("Admin Person", ctx.ImpersonatorFullName);
        Assert.Equal("GlobalAdmin", ctx.ImpersonatorRoleCode);
        Assert.Equal("en", ctx.ImpersonatorPreferredLanguage);
    }

    private static ImpersonationService CreateService(
        IHttpContextAccessor accessor,
        ICurrentUserContext currentUser,
        IUserRepository userRepo)
    {
        var httpContext = accessor.HttpContext ?? throw new InvalidOperationException("HTTP context missing.");
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton(userRepo)
            .AddSingleton<IAuditLogRepository, NoOpAuditLogRepository>()
            .BuildServiceProvider();
        httpContext.RequestServices = services;

        return new ImpersonationService(
            accessor,
            userRepo,
            currentUser,
            new NoOpAuditLogRepository(),
            services.GetRequiredService<ILogger<ImpersonationService>>());
    }

    private static IHttpContextAccessor CreateAccessor(ClaimsPrincipal principal)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return accessor;
    }

    private static ClaimsPrincipal CreatePrincipal(
        Guid userId,
        string login,
        string email,
        string fullName,
        string role,
        string preferredLanguage,
        IReadOnlyList<Claim>? extras = null)
    {
        var claims = new List<Claim>
        {
            new("userId", userId.ToString()),
            new(ClaimTypes.NameIdentifier, login),
            new("sub", login),
            new(ClaimTypes.Email, email),
            new("email", email),
            new("displayName", fullName),
            new("initials", string.Concat(fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])))),
            new("role", role),
            new("preferredLanguage", preferredLanguage)
        };

        if (extras is not null)
            claims.AddRange(extras);

        var identity = new ClaimsIdentity(claims, "Cookies");
        return new ClaimsPrincipal(identity);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, User> _users;

        public FakeUserRepository(params User[] users)
        {
            _users = users.ToDictionary(user => user.UserId);
        }

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_users.TryGetValue(userId, out var user) ? user : null);

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
        public Task<User> UpdateAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoOpAuditLogRepository : IAuditLogRepository
    {
        public Task WriteAsync(string actionType, string outcome, Guid? actorUserId, string actorType, Guid? targetUserId, string? beforeValue, string? afterValue, string? sourceComponent, string? denialReason = null, string? ipAddress = null, string? additionalDataJson = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
