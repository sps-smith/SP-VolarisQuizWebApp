using System.Security.Claims;
using Microsoft.Extensions.Options;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;

namespace Volaris101.Web.Auth;

public interface IOktaJitService
{
    Task<string?> HandleSignInAsync(ClaimsPrincipal principal);
}

public class OktaJitService : IOktaJitService
{
    private readonly IUserRepository _userRepo;
    private readonly IReportingAccessRepository _reportingAccessRepo;
    private readonly IAuditLogRepository _auditLog;
    private readonly ILogger<OktaJitService> _log;
    private readonly OktaJitOptions _options;

    public OktaJitService(
        IUserRepository userRepo,
        IReportingAccessRepository reportingAccessRepo,
        IAuditLogRepository auditLog,
        ILogger<OktaJitService> log,
        IOptions<OktaJitOptions> options)
    {
        _userRepo = userRepo;
        _reportingAccessRepo = reportingAccessRepo;
        _auditLog = auditLog;
        _log = log;
        _options = options.Value;
    }

    public async Task<string?> HandleSignInAsync(ClaimsPrincipal principal)
    {
        try
        {
            // Log all claims for diagnostics
            foreach (var claim in principal.Claims)
                _log.LogInformation("Okta claim: {Type} = {Value}", claim.Type, claim.Value);

            var login     = FindClaimValue(principal, "sub", ClaimTypes.NameIdentifier)
                         ?? string.Empty;
            var email     = FindClaimValue(principal, "email", ClaimTypes.Email, "preferred_username")
                         ?? string.Empty;

            // Parse first/last from given_name/family_name, fall back to splitting 'name'
            var firstName = FindClaimValue(principal, "given_name", ClaimTypes.GivenName)
                         ?? string.Empty;
            var lastName  = FindClaimValue(principal, "family_name", ClaimTypes.Surname)
                         ?? string.Empty;
            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
            {
                var fullName = FindClaimValue(principal, "name") ?? string.Empty;
                var parts    = fullName.Split(' ', 2);
                firstName    = parts.Length > 0 ? parts[0] : string.Empty;
                lastName     = parts.Length > 1 ? parts[1] : string.Empty;
            }

            var employeeId = FindClaimValue(principal, "EmployeeId", "employeeId", "EmployeeID", "employeeID")
                          ?? string.Empty;
            var validEmailClaim = FindClaimValue(principal, "ValidEmail", "validEmail", "valid_email")
                              ?? string.Empty;
            var company    = FindClaimValue(principal, "VBUName", "company");
            var rawPortfolio = FindClaimValue(principal, "Portfolio", "portfolio");
            var portfolio  = MapPortfolio(rawPortfolio);
            var vbuIdStr   = FindClaimValue(principal, "VBUID") ?? string.Empty;
            int? organizationId = int.TryParse(vbuIdStr, out var parsedVbuId) ? parsedVbuId : null;
            var hasEmployeeId = HasEmployeeId(employeeId);
            var existingByEmail = !string.IsNullOrWhiteSpace(email)
                ? await _userRepo.GetByEmailAsync(email)
                : null;
            var hasValidEmailAccess = HasValidEmailAccess(validEmailClaim, email) || existingByEmail is not null;

            // Look up PortfolioID from the VBU master list API using the VBUID
            int? portfolioId = await GetPortfolioIdAsync(portfolio, CancellationToken.None);

            _log.LogInformation("JIT sign-in: login={Login} email={Email} orgId={OrgId} portfolioId={PortfolioId} rawPortfolio={RawPortfolio} mappedPortfolio={MappedPortfolio}",
                login, email, organizationId, portfolioId, rawPortfolio, portfolio);

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(email))
            {
                _log.LogWarning("JIT denied: missing sub or email claim");
                return "Missing required claims (sub/email).";
            }

            if (!hasEmployeeId && !hasValidEmailAccess)
            {
                _log.LogWarning(
                    "JIT denied: access validation failed for login={Login} email={Email}. EmployeeId='{EmployeeId}', ValidEmail='{ValidEmail}', ExistingEmailMatch={ExistingEmailMatch}",
                    login,
                    email,
                    employeeId,
                    validEmailClaim,
                    existingByEmail is not null);
                return "Access denied. A valid EmployeeId or ValidEmail flag is required.";
            }

            _log.LogInformation(
                "JIT access validation passed for login={Login} using {AccessMode}",
                login,
                hasEmployeeId ? "EmployeeId" : existingByEmail is not null ? "ExistingEmail" : "ValidEmail");

            var existingByLogin = await _userRepo.GetByLoginAsync(login);
            var existingByEmployeeId = hasEmployeeId
                ? await _userRepo.GetByEmployeeIdAsync(employeeId)
                : null;
            var activeLoginOrEmailMatches = new[] { existingByLogin, existingByEmail }
                .Where(match => match is not null && match.IsActive)
                .GroupBy(match => match!.UserId)
                .Select(group => group.First()!)
                .ToList();
            var inactiveLoginConflictForEmployeeMatch =
                hasEmployeeId &&
                existingByEmployeeId is not null &&
                existingByLogin is not null &&
                existingByLogin.UserId != existingByEmployeeId.UserId &&
                !existingByLogin.IsActive;
            var inactiveEmailConflictForEmployeeMatch =
                hasEmployeeId &&
                existingByEmployeeId is not null &&
                existingByEmail is not null &&
                existingByEmail.UserId != existingByEmployeeId.UserId &&
                !existingByEmail.IsActive;
            var inactiveEmployeeConflictForActiveAlternate =
                hasEmployeeId &&
                existingByEmployeeId is not null &&
                !existingByEmployeeId.IsActive &&
                activeLoginOrEmailMatches.Count == 1 &&
                activeLoginOrEmailMatches[0].UserId != existingByEmployeeId.UserId;
            var preferredActiveMatch = hasEmployeeId
                ? ResolvePreferredActiveMatch(existingByLogin, existingByEmployeeId, existingByEmail)
                : null;

            User? existing;
            if (!hasEmployeeId && existingByEmail is not null)
            {
                existing = existingByEmail;

                if (existingByLogin is not null && existingByLogin.UserId != existingByEmail.UserId)
                {
                    _log.LogWarning(
                        "JIT found mismatched login and email records for login={Login}. Preferring email-matched user {EmailUserId} over login-matched user {LoginUserId} because EmployeeId is missing.",
                        login,
                        existingByEmail.UserId,
                        existingByLogin.UserId);
                }
            }
            else
            {
                if (preferredActiveMatch is not null)
                {
                    existing = preferredActiveMatch;
                    _log.LogWarning(
                        "JIT resolved multiple active matches for login={Login} by preferring user {PreferredUserId} with role {RoleCode}.",
                        login,
                        existing.UserId,
                        existing.RoleCode);
                }
                else if (inactiveEmployeeConflictForActiveAlternate)
                {
                    existing = activeLoginOrEmailMatches[0];
                    _log.LogWarning(
                        "JIT found an inactive employee-matched record {InactiveEmployeeUserId} for login={Login}. Preferring the active alternate user {ActiveUserId}.",
                        existingByEmployeeId!.UserId,
                        login,
                        existing.UserId);
                }
                else if (inactiveLoginConflictForEmployeeMatch || inactiveEmailConflictForEmployeeMatch)
                {
                    existing = existingByEmployeeId;
                    _log.LogWarning(
                        "JIT found inactive duplicate records for login={Login} email={Email}. Preferring employee-matched user {EmployeeUserId} over inactive conflicting records.",
                        login,
                        email,
                        existingByEmployeeId!.UserId);
                }
                else
                {
                existing = existingByLogin
                    ?? existingByEmployeeId
                    ?? existingByEmail;
                }
            }

            if (existing is not null && !existing.IsActive)
            {
                var activeMatches = new[] { existingByLogin, existingByEmployeeId, existingByEmail }
                    .Where(match => match is not null && match.IsActive)
                    .GroupBy(match => match!.UserId)
                    .Select(group => group.First()!)
                    .ToList();

                if (activeMatches.Count == 1)
                {
                    _log.LogWarning(
                        "JIT resolved an inactive user {InactiveUserId} for login={Login}, but found one active alternate match {ActiveUserId}. Preferring the active record.",
                        existing.UserId,
                        login,
                        activeMatches[0].UserId);
                    existing = activeMatches[0];
                }
            }

            var conflictLoginMatch = inactiveLoginConflictForEmployeeMatch ? null : existingByLogin;
            var conflictEmailMatch = inactiveEmailConflictForEmployeeMatch ? null : existingByEmail;
            var conflictEmployeeMatch = inactiveEmployeeConflictForActiveAlternate ? null : existingByEmployeeId;

            if (hasEmployeeId && preferredActiveMatch is null && HasConflictingMatch(conflictLoginMatch, conflictEmployeeMatch, conflictEmailMatch))
            {
                _log.LogWarning(
                    "JIT denied: conflicting user matches for login={Login}, email={Email}, employeeId={EmployeeId}",
                    login,
                    email,
                    employeeId);
                return "Your sign-in details match multiple user records. Contact your administrator.";
            }

            _log.LogInformation(
                "JIT user match for login={Login}: byLogin={ByLogin} byEmployeeId={ByEmployeeId} byEmail={ByEmail} resolvedUserId={ResolvedUserId}",
                login,
                existingByLogin?.UserId,
                existingByEmployeeId?.UserId,
                existingByEmail?.UserId,
                existing?.UserId);

            var hasLoginConflictForEmailMatch =
                !hasEmployeeId &&
                existing is not null &&
                existingByEmail is not null &&
                existing.UserId == existingByEmail.UserId &&
                existingByLogin is not null &&
                existingByLogin.UserId != existingByEmail.UserId;

            User user;

            if (existing == null)
            {
                if (hasEmployeeId && existingByEmail != null)
                {
                    _log.LogWarning(
                        "JIT denied: email {Email} already exists for another user while employeeId {EmployeeId} did not match an existing record",
                        email,
                        employeeId);
                    return "Email address already associated with another account.";
                }

                // Create new user
                user = new User
                {
                    UserId               = Guid.NewGuid(),
                    Login                = login.ToLowerInvariant(),
                    IdpSource            = "Okta",
                    IdpSubjectIdentifier = login,
                    FirstName            = firstName,
                    LastName             = lastName,
                    Email                = email.ToLowerInvariant(),
                    EmployeeId           = employeeId,
                    Company              = company,
                    Portfolio            = portfolio,
                    OrganizationId       = organizationId,
                    PortfolioId          = portfolioId,
                    RoleCode             = "Learner",
                    AccountCreatedAt     = DateTimeOffset.UtcNow,
                    LastLoginAt          = DateTimeOffset.UtcNow,
                    LastAttributeRefreshAt = DateTimeOffset.UtcNow,
                    IsActive             = true
                };

                await _userRepo.CreateAsync(user);
                _log.LogInformation("JIT created user {UserId} ({Email})", user.UserId, email);

                try { await _auditLog.WriteAsync("auth.jit_created", "success", null, "system",
                    user.UserId, null, null, "auth.oidc"); }
                catch (Exception ex) { _log.LogWarning(ex, "Audit log write failed (non-fatal)"); }
            }
            else
            {
                if (!existing.IsActive)
                {
                    _log.LogWarning("Sign-in denied for deactivated user {UserId}", existing.UserId);
                    return "Your account has been deactivated. Contact your administrator.";
                }

                // Refresh attributes from Okta on every sign-in
                if (!hasLoginConflictForEmailMatch)
                {
                    if (!inactiveLoginConflictForEmployeeMatch)
                    {
                        existing.Login                = login.ToLowerInvariant();
                        existing.IdpSource            = "Okta";
                        existing.IdpSubjectIdentifier = login;
                    }
                    else
                    {
                        _log.LogWarning(
                            "Skipping Login/IdpSubjectIdentifier update for user {UserId} because login {Login} is still attached to an inactive duplicate record {ConflictingUserId}.",
                            existing.UserId,
                            login,
                            existingByLogin!.UserId);
                    }
                }
                else
                {
                    _log.LogWarning(
                        "Skipping Login/IdpSubjectIdentifier update for user {UserId} because login {Login} is already attached to a different record {ConflictingUserId}.",
                        existing.UserId,
                        login,
                        existingByLogin!.UserId);
                }
                existing.FirstName              = firstName;
                existing.LastName               = lastName;
                if (!inactiveEmailConflictForEmployeeMatch)
                {
                    existing.Email = email.ToLowerInvariant();
                }
                else
                {
                    _log.LogWarning(
                        "Skipping email update for user {UserId} because email {Email} is still attached to an inactive duplicate record {ConflictingUserId}.",
                        existing.UserId,
                        email,
                        existingByEmail!.UserId);
                }
                if (hasEmployeeId)
                    existing.EmployeeId         = employeeId;
                existing.Company                = company;
                existing.Portfolio              = portfolio;
                existing.OrganizationId         = organizationId;
                // Only update PortfolioId if the API returned a valid value
                if (portfolioId.HasValue && portfolioId.Value > 0)
                    existing.PortfolioId         = portfolioId;
                existing.LastLoginAt            = DateTimeOffset.UtcNow;
                existing.LastAttributeRefreshAt = DateTimeOffset.UtcNow;

                await _userRepo.UpdateAsync(existing);
                user = existing;
                _log.LogInformation("JIT updated user {UserId} ({Email}) portfolioId={PortfolioId} rawPortfolio={RawPortfolio} mappedPortfolio={MappedPortfolio}",
                    user.UserId, email, portfolioId, rawPortfolio, portfolio);

                try { await _auditLog.WriteAsync("auth.sign_in", "success", user.UserId, "user",
                    user.UserId, null, null, "auth.oidc"); }
                catch (Exception ex) { _log.LogWarning(ex, "Audit log write failed (non-fatal)"); }
            }

            AppendRoleClaim(principal, user);
            return null;
        }
        catch (Exception ex)
        {
            // Unwrap to get the real DB error
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            _log.LogError(ex, "JIT sign-in failed: {Message} | Inner: {Inner}", ex.Message, inner.Message);
            return $"Sign-in processing failed: {ex.Message} | Root cause: {inner.Message}";
        }
    }

    /// <summary>
    /// Calls the VBU master list API to retrieve the PortfolioID for a given OrganizationID.
    /// Returns 0 if the call fails or the org is not found.
    /// </summary>
    private async Task<int?> GetPortfolioIdAsync(string? portfolioName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(portfolioName))
            return null;

        var portfolio = await _reportingAccessRepo.GetPortfolioByNameAsync(portfolioName, ct);
        if (portfolio is null)
        {
            _log.LogWarning("No database portfolio found for Okta portfolio name {PortfolioName}", portfolioName);
            return null;
        }

        _log.LogInformation("Resolved portfolio name {PortfolioName} to PortfolioID={PortfolioId}",
            portfolioName, portfolio.PortfolioId);
        return portfolio.PortfolioId > 0 ? portfolio.PortfolioId : null;
    }

    private static void AppendRoleClaim(ClaimsPrincipal principal, User user)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim("userId",            user.UserId.ToString()));
        identity.AddClaim(new Claim("role",              user.RoleCode));
        identity.AddClaim(new Claim("preferredLanguage", user.PreferredLanguage));
        identity.AddClaim(new Claim("initials",          user.Initials));
        identity.AddClaim(new Claim("displayName",       user.FullName));
    }

    private string? MapPortfolio(string? rawPortfolio)
    {
        if (string.IsNullOrWhiteSpace(rawPortfolio))
            return rawPortfolio;

        var normalizedRaw = rawPortfolio.Trim();
        foreach (var mapping in _options.PortfolioMappings)
        {
            if (string.Equals(mapping.Key?.Trim(), normalizedRaw, StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(mapping.Value) ? rawPortfolio : mapping.Value.Trim();
        }

        return rawPortfolio;
    }

    private static bool HasConflictingMatch(params User?[] matches)
    {
        var distinctUserIds = matches
            .Where(match => match is not null)
            .Select(match => match!.UserId)
            .Distinct()
            .ToList();

        return distinctUserIds.Count > 1;
    }

    private static User? ResolvePreferredActiveMatch(User? existingByLogin, User? existingByEmployeeId, User? existingByEmail)
    {
        var activeMatches = new[] { existingByLogin, existingByEmployeeId, existingByEmail }
            .Where(match => match is not null && match.IsActive)
            .GroupBy(match => match!.UserId)
            .Select(group => group.First()!)
            .ToList();

        if (activeMatches.Count <= 1)
            return activeMatches.SingleOrDefault();

        var highestPriority = activeMatches
            .OrderByDescending(match => GetRolePriority(match.RoleCode))
            .ToList();

        if (highestPriority.Count >= 2 &&
            GetRolePriority(highestPriority[0].RoleCode) > GetRolePriority(highestPriority[1].RoleCode))
        {
            return highestPriority[0];
        }

        if (existingByLogin is not null &&
            existingByEmail is not null &&
            existingByLogin.UserId == existingByEmail.UserId &&
            existingByLogin.IsActive)
        {
            return existingByLogin;
        }

        if (existingByEmployeeId is not null && existingByEmployeeId.IsActive)
            return existingByEmployeeId;

        return null;
    }

    private static int GetRolePriority(string? roleCode) => roleCode switch
    {
        "GlobalAdmin" => 4,
        "ReportingAdmin" => 3,
        "PortfolioAdmin" => 3,
        "OrgAdmin" => 2,
        _ => 1
    };

    private static bool HasEmployeeId(string? employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return false;

        return !string.Equals(employeeId.Trim(), "0", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthyClaim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "true" => true,
            "1"    => true,
            "yes"  => true,
            "y"    => true,
            _      => false
        };
    }

    private static bool HasValidEmailAccess(string? validEmailClaim, string email)
    {
        if (IsTruthyClaim(validEmailClaim))
            return true;

        if (string.IsNullOrWhiteSpace(validEmailClaim) || string.IsNullOrWhiteSpace(email))
            return false;

        var parts = validEmailClaim
            .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Any(part => string.Equals(part, email, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var claim = principal.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(c.Value));

            if (claim is not null)
                return claim.Value;
        }

        return null;
    }
}

public sealed class OktaJitOptions
{
    public Dictionary<string, string> PortfolioMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserContext(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;
    private bool HasImpersonationClaim => !string.IsNullOrWhiteSpace(Principal?.FindFirstValue(ImpersonationClaimTypes.OriginalUserId));

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue("userId"), out var id) ? id : Guid.Empty;
    public string Login => Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public string FullName => Principal?.FindFirstValue("displayName") ?? string.Empty;
    public string RoleCode => Principal?.FindFirstValue("role") ?? "Learner";
    public string PreferredLanguage => Principal?.FindFirstValue("preferredLanguage") ?? "en";
    public bool IsImpersonating => HasImpersonationClaim;
    public Guid ImpersonatorUserId => Guid.TryParse(Principal?.FindFirstValue(ImpersonationClaimTypes.OriginalUserId), out var id) ? id : Guid.Empty;
    public string ImpersonatorLogin => Principal?.FindFirstValue(ImpersonationClaimTypes.OriginalLogin) ?? string.Empty;
    public string ImpersonatorEmail => Principal?.FindFirstValue(ImpersonationClaimTypes.OriginalEmail) ?? string.Empty;
    public string ImpersonatorFullName => Principal?.FindFirstValue(ImpersonationClaimTypes.OriginalFullName) ?? string.Empty;
    public string ImpersonatorRoleCode => Principal?.FindFirstValue(ImpersonationClaimTypes.OriginalRoleCode) ?? string.Empty;
    public string ImpersonatorPreferredLanguage => Principal?.FindFirstValue(ImpersonationClaimTypes.OriginalPreferredLanguage) ?? string.Empty;
}
