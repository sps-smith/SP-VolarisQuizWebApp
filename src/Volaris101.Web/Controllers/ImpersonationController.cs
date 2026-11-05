using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volaris101.Application.Interfaces;
using Volaris101.Web.Auth;

namespace Volaris101.Web.Controllers;

[Route("impersonation")]
public class ImpersonationController : Controller
{
    private readonly IImpersonationService _impersonation;
    private readonly ICurrentUserContext _user;

    public ImpersonationController(IImpersonationService impersonation, ICurrentUserContext user)
    {
        _impersonation = impersonation;
        _user = user;
    }

    [HttpPost("start/{userId:guid}")]
    [Authorize(Policy = "GlobalAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(Guid userId, [FromForm] string? returnUrl, CancellationToken ct)
    {
        var result = await _impersonation.StartAsync(userId, returnUrl, ct);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            result.Principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return Json(new
        {
            redirectUrl = result.RedirectUrl,
            userName = result.TargetFullName
        });
    }

    [HttpPost("stop")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        if (!_user.IsImpersonating)
            return BadRequest(new { error = "You are not currently impersonating another user." });

        var result = await _impersonation.StopAsync(ct);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            result.Principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return Json(new
        {
            redirectUrl = result.RedirectUrl
        });
    }
}
