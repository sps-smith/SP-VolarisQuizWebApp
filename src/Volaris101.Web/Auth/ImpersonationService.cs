using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;

namespace Volaris101.Web.Auth;

public record ImpersonationTransitionResult(ClaimsPrincipal Principal, string RedirectUrl, string TargetFullName);

public interface IImpersonationService
{
    Task<ImpersonationTransitionResult> StartAsync(Guid targetUserId, string? returnUrl, CancellationToken ct = default);
    Task<ImpersonationTransitionResult> StopAsync(CancellationToken ct = default);
}

public class ImpersonationService : IImpersonationService
{
    private readonly IHttpContextAccessor _http;
    private readonly IUserRepository _userRepo;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogRepository _auditLog;
    private readonly ILogger<ImpersonationService> _log;

    public ImpersonationService(
        IHttpContextAccessor http,
        IUserRepository userRepo,
        ICurrentUserContext currentUser,
        IAuditLogRepository auditLog,
        ILogger<ImpersonationService> log)
    {
        _http = http;
        _userRepo = userRepo;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _log = log;
    }

    public async Task<ImpersonationTransitionResult> StartAsync(Guid targetUserId, string? returnUrl, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("You must be signed in to impersonate a user.");
        if (!_currentUser.IsGlobalAdmin)
            throw new UnauthorizedAccessException("Only Global Admin users can impersonate other users.");
        if (_currentUser.IsImpersonating)
            throw new InvalidOperationException("Stop the current impersonation session before starting another one.");
        if (targetUserId == Guid.Empty)
            throw new ArgumentException("A target user is required.", nameof(targetUserId));
        if (targetUserId == _currentUser.UserId)
            throw new ArgumentException("You cannot impersonate your own account.", nameof(targetUserId));

        var target = await _userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException($"User {targetUserId} was not found.");
        if (!target.IsActive)
            throw new InvalidOperationException("You can only impersonate an active user.");

        var principal = _http.HttpContext?.User ?? throw new InvalidOperationException("No active HTTP context was found.");
        var identity = new ClaimsIdentity(principal.Identity?.AuthenticationType ?? CookieAuthenticationDefaults.AuthenticationScheme);

        foreach (var claim in principal.Claims)
        {
            if (ShouldSkipClaim(claim.Type))
                continue;

            identity.AddClaim(claim);
        }

        AddClaim(identity, ClaimTypes.NameIdentifier, target.Login);
        AddClaim(identity, "sub", target.Login);
        AddClaim(identity, ClaimTypes.Email, target.Email);
        AddClaim(identity, "email", target.Email);
        AddClaim(identity, "userId", target.UserId.ToString());
        AddClaim(identity, "role", target.RoleCode);
        AddClaim(identity, "preferredLanguage", target.PreferredLanguage);
        AddClaim(identity, "displayName", target.FullName);
        AddClaim(identity, "initials", target.Initials);

        AddClaim(identity, ImpersonationClaimTypes.OriginalUserId, _currentUser.UserId.ToString());
        AddClaim(identity, ImpersonationClaimTypes.OriginalLogin, _currentUser.Login);
        AddClaim(identity, ImpersonationClaimTypes.OriginalEmail, _currentUser.Email);
        AddClaim(identity, ImpersonationClaimTypes.OriginalFullName, _currentUser.FullName);
        AddClaim(identity, ImpersonationClaimTypes.OriginalRoleCode, _currentUser.RoleCode);
        AddClaim(identity, ImpersonationClaimTypes.OriginalPreferredLanguage, _currentUser.PreferredLanguage);
        AddClaim(identity, ImpersonationClaimTypes.OriginalDisplayName, _currentUser.FullName);
        AddClaim(identity, ImpersonationClaimTypes.OriginalInitials, GetInitials(_currentUser.FullName));
        AddClaim(identity, ImpersonationClaimTypes.ReturnUrl, NormalizeLocalUrl(returnUrl) ?? "/");

        await WriteAuditAsync("auth.impersonation_started", target.UserId, target.FullName, ct);
        _log.LogInformation(
            "Global admin {ActorUserId} started impersonating {TargetUserId} ({TargetFullName}).",
            _currentUser.UserId,
            target.UserId,
            target.FullName);

        return new ImpersonationTransitionResult(
            new ClaimsPrincipal(identity),
            "/learner/dashboard",
            target.FullName);
    }

    public async Task<ImpersonationTransitionResult> StopAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("You must be signed in to stop impersonation.");
        if (!_currentUser.IsImpersonating)
            throw new InvalidOperationException("You are not currently impersonating another user.");

        var principal = _http.HttpContext?.User ?? throw new InvalidOperationException("No active HTTP context was found.");
        var identity = new ClaimsIdentity(principal.Identity?.AuthenticationType ?? CookieAuthenticationDefaults.AuthenticationScheme);

        foreach (var claim in principal.Claims)
        {
            if (IsImpersonationClaim(claim.Type))
                continue;

            identity.AddClaim(claim);
        }

        Guid? originalUserId = GetGuidClaim(principal, ImpersonationClaimTypes.OriginalUserId);
        originalUserId ??= _currentUser.ImpersonatorUserId == Guid.Empty ? null : _currentUser.ImpersonatorUserId;
        var originalLogin = GetClaimValue(principal, ImpersonationClaimTypes.OriginalLogin) ?? _currentUser.ImpersonatorLogin;
        var originalEmail = GetClaimValue(principal, ImpersonationClaimTypes.OriginalEmail) ?? _currentUser.ImpersonatorEmail;
        var originalFullName = GetClaimValue(principal, ImpersonationClaimTypes.OriginalFullName) ?? _currentUser.ImpersonatorFullName;
        var originalRoleCode = GetClaimValue(principal, ImpersonationClaimTypes.OriginalRoleCode) ?? _currentUser.ImpersonatorRoleCode;
        var originalPreferredLanguage = GetClaimValue(principal, ImpersonationClaimTypes.OriginalPreferredLanguage) ?? _currentUser.ImpersonatorPreferredLanguage;
        var originalDisplayName = GetClaimValue(principal, ImpersonationClaimTypes.OriginalDisplayName) ?? _currentUser.ImpersonatorFullName;
        var originalInitials = GetClaimValue(principal, ImpersonationClaimTypes.OriginalInitials) ?? GetInitials(originalFullName);
        var redirectUrl = NormalizeLocalUrl(GetClaimValue(principal, ImpersonationClaimTypes.ReturnUrl)) ?? "/admin/users";

        if (originalUserId is { } restoredUserId && restoredUserId != Guid.Empty)
            AddClaim(identity, "userId", restoredUserId.ToString());
        AddClaim(identity, ClaimTypes.NameIdentifier, originalLogin);
        AddClaim(identity, "sub", originalLogin);
        AddClaim(identity, ClaimTypes.Email, originalEmail);
        AddClaim(identity, "email", originalEmail);
        AddClaim(identity, "role", originalRoleCode);
        AddClaim(identity, "preferredLanguage", originalPreferredLanguage);
        AddClaim(identity, "displayName", originalDisplayName);
        AddClaim(identity, "initials", originalInitials);

        await WriteAuditAsync("auth.impersonation_stopped", originalUserId, originalFullName, ct);
        _log.LogInformation(
            "Global admin {ActorUserId} stopped impersonating and restored {OriginalUserId} ({OriginalFullName}).",
            _currentUser.ImpersonatorUserId,
            originalUserId,
            originalFullName);

        return new ImpersonationTransitionResult(new ClaimsPrincipal(identity), redirectUrl, originalFullName);
    }

    private async Task WriteAuditAsync(string actionType, Guid? targetUserId, string targetName, CancellationToken ct)
    {
        try
        {
            var actorType = _currentUser.IsImpersonating ? "impersonator" : "user";
            var additionalData = $"{{\"targetUserId\":\"{targetUserId}\",\"targetName\":\"{EscapeJson(targetName)}\"}}";
            await _auditLog.WriteAsync(
                actionType,
                "success",
                _currentUser.UserId,
                actorType,
                targetUserId,
                null,
                null,
                "admin.impersonation",
                additionalDataJson: additionalData,
                ct: ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to write impersonation audit log.");
        }
    }

    private static bool ShouldSkipClaim(string claimType) =>
        IsImpersonationClaim(claimType) ||
        string.Equals(claimType, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "sub", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, ClaimTypes.Email, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "email", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "userId", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "role", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "preferredLanguage", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "displayName", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claimType, "initials", StringComparison.OrdinalIgnoreCase);

    private static bool IsImpersonationClaim(string claimType) =>
        claimType.StartsWith("impersonation", StringComparison.OrdinalIgnoreCase);

    private static void AddClaim(ClaimsIdentity identity, string type, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        RemoveClaims(identity, type);
        identity.AddClaim(new Claim(type, value));
    }

    private static void RemoveClaims(ClaimsIdentity identity, string claimType)
    {
        foreach (var claim in identity.FindAll(claimType).ToList())
            identity.RemoveClaim(claim);
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, string claimType) =>
        principal.Claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;

    private static Guid? GetGuidClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = GetClaimValue(principal, claimType);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string GetInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
    }

    private static string? NormalizeLocalUrl(string? url) =>
        IsLocalUrl(url) ? url : null;

    private static string EscapeJson(string? value) =>
        (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
