using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Volaris101.Application.Interfaces;

namespace Volaris101.Web.Controllers;

[Authorize]
[Route("language")]
public class LanguageController : Controller
{
    private readonly IUserRepository _userRepo;
    private readonly ICurrentUserContext _user;

    private static readonly HashSet<string> SupportedLanguages = new() { "en", "fr", "es", "pt", "de" };

    public LanguageController(IUserRepository userRepo, ICurrentUserContext user)
    {
        _userRepo = userRepo;
        _user = user;
    }

    // POST /language/set
    [HttpPost("set")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set([FromForm] string lang, [FromForm] string? returnUrl, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(lang) || !SupportedLanguages.Contains(lang))
            lang = "en";

        // Persist to DB
        var user = await _userRepo.GetByIdAsync(_user.UserId, ct);
        if (user != null)
        {
            user.PreferredLanguage = lang;
            await _userRepo.UpdateAsync(user, ct);
        }

        // Re-issue the auth cookie with updated preferredLanguage claim
        // so CurrentUserContext picks it up immediately without a re-login
        var identity = new ClaimsIdentity(
            User.Claims
                .Where(c => c.Type != "preferredLanguage")
                .Append(new Claim("preferredLanguage", lang)),
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

        // Redirect back to where the user was
        var redirect = returnUrl;
        if (string.IsNullOrEmpty(redirect) || !Url.IsLocalUrl(redirect))
            redirect = "/";

        return Redirect(redirect);
    }
}
