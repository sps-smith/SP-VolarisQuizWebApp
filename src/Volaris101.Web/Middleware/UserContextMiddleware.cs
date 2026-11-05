using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;

namespace Volaris101.Web.Middleware;

/// <summary>
/// Refreshes the current user's active status from the database on each request.
/// Deactivated users are signed out immediately.
/// </summary>
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserContextMiddleware> _log;

    public UserContextMiddleware(RequestDelegate next, ILogger<UserContextMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserContext userCtx, IUserRepository userRepo)
    {
        if (userCtx.IsAuthenticated && userCtx.UserId == Guid.Empty)
        {
            try
            {
                var principal = context.User;
                var login = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub")
                    ?? string.Empty;
                var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? string.Empty;

                User? resolvedUser = null;
                if (!string.IsNullOrWhiteSpace(login))
                    resolvedUser = await userRepo.GetByLoginAsync(login);
                if (resolvedUser is null && !string.IsNullOrWhiteSpace(email))
                    resolvedUser = await userRepo.GetByEmailAsync(email);

                if (resolvedUser is { IsActive: true })
                {
                    _log.LogWarning(
                        "Authenticated request was missing app claims. Rehydrating claims for user {UserId} using login={Login} email={Email}.",
                        resolvedUser.UserId,
                        login,
                        email);

                    var identity = context.User.Identity as ClaimsIdentity;
                    if (identity is not null)
                    {
                        ReplaceClaim(identity, "userId", resolvedUser.UserId.ToString());
                        ReplaceClaim(identity, "role", resolvedUser.RoleCode);
                        ReplaceClaim(identity, "preferredLanguage", resolvedUser.PreferredLanguage);
                        ReplaceClaim(identity, "displayName", resolvedUser.FullName);
                        ReplaceClaim(identity, "initials", resolvedUser.Initials);

                        await context.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(identity));

                        await _next(context);
                        return;
                    }
                }

                _log.LogWarning("Authenticated request is missing the userId claim and could not be rehydrated. Signing out and redirecting to login.");
                await context.SignOutAsync();
                context.Response.Redirect("/auth/login");
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to recover from missing userId claim.");
            }
        }

        if (userCtx.IsAuthenticated && userCtx.UserId != Guid.Empty)
        {
            try
            {
                var user = await userRepo.GetByIdAsync(userCtx.UserId);

                // User not found in DB — cookie is stale (e.g. after DB reset)
                // Sign out and redirect to login so a fresh JIT record is created
                if (user == null)
                {
                    await context.SignOutAsync();
                    context.Response.Redirect("/auth/login");
                    return;
                }

                if (!user.IsActive)
                {
                    await context.SignOutAsync();
                    context.Response.Redirect("/auth/access-denied");
                    return;
                }

                var identity = context.User.Identity as ClaimsIdentity;
                if (identity != null)
                {
                    var changed = ReplaceClaim(identity, "role", user.RoleCode);
                    changed |= ReplaceClaim(identity, "preferredLanguage", user.PreferredLanguage);
                    changed |= ReplaceClaim(identity, "displayName", user.FullName);
                    changed |= ReplaceClaim(identity, "initials", user.Initials);

                    if (changed)
                    {
                        await context.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(identity));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UserContextMiddleware DB check failed for user {UserId} — continuing", userCtx.UserId);
            }
        }

        await _next(context);
    }

    private static bool ReplaceClaim(ClaimsIdentity identity, string type, string value)
    {
        var existing = identity.FindFirst(type);
        if (existing?.Value == value)
            return false;

        if (existing != null)
            identity.RemoveClaim(existing);
        identity.AddClaim(new Claim(type, value));
        return true;
    }
}
