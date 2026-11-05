using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;

namespace Volaris101.Web.Controllers;

[Authorize]
public class LearnerController : Controller
{
    private readonly ILearnerService _learner;
    private readonly ICurrentUserContext _user;
    private readonly ILogger<LearnerController> _log;
    private static readonly HashSet<string> SupportedQuizLanguages = new() { "en", "fr", "es", "pt", "de" };

    public LearnerController(ILearnerService learner, ICurrentUserContext user, ILogger<LearnerController> log)
    {
        _learner = learner;
        _user = user;
        _log = log;
    }

    // GET /  or  GET /learner/dashboard
    [HttpGet(""), HttpGet("learner/dashboard")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        if (_user.UserId == Guid.Empty)
        {
            _log.LogWarning("Dashboard request is missing a valid userId claim. Signing out and redirecting to login.");
            await HttpContext.SignOutAsync();
            return Redirect("/auth/login");
        }

        try
        {
            var dashboard = await _learner.GetDashboardAsync(_user.UserId, _user.PreferredLanguage, ct);
            return View(dashboard);
        }
        catch (KeyNotFoundException)
        {
            _log.LogWarning("Dashboard request failed because user {UserId} no longer exists. Signing out and redirecting to login.", _user.UserId);
            await HttpContext.SignOutAsync();
            return Redirect("/auth/login");
        }
        catch (Exception ex)
        {
            var root = ex;
            while (root.InnerException != null)
                root = root.InnerException;

            _log.LogError(ex,
                "Failed to load dashboard for user {UserId}. Root cause: {RootMessage}",
                _user.UserId,
                root.Message);

            throw;
        }
    }

    // GET /learner/quiz/start/{quizId}
    [HttpGet("learner/quiz/start/{quizId:guid}")]
    public Task<IActionResult> StartQuizGet(Guid quizId, [FromQuery] string? lang, CancellationToken ct) =>
        RenderQuizAsync(quizId, lang, ct);

    // POST /learner/quiz/start/{quizId}
    [HttpPost("learner/quiz/start/{quizId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> StartQuiz(Guid quizId, CancellationToken ct) =>
        RenderQuizAsync(quizId, _user.PreferredLanguage, ct);

    // GET /learner/quiz/{attemptId}  — reload after page refresh
    [HttpGet("learner/quiz/open/{quizId:guid}")]
    public Task<IActionResult> OpenQuiz(Guid quizId, [FromQuery] string? lang, CancellationToken ct) =>
        RenderQuizAsync(quizId, lang, ct);

    private async Task<IActionResult> RenderQuizAsync(Guid quizId, string? lang, CancellationToken ct)
    {
        var quizLanguage = NormalizeQuizLanguage(lang ?? _user.PreferredLanguage);

        try
        {
            var result = await _learner.StartQuizAsync(_user.UserId, quizId, quizLanguage, ct);
            TempData["AttemptId"] = result.AttemptId.ToString();
            return View("Quiz", result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            var root = ex;
            while (root.InnerException != null)
                root = root.InnerException;

            _log.LogError(ex,
                "Failed to start quiz {QuizId} for user {UserId}. Root cause: {RootMessage}",
                quizId,
                _user.UserId,
                root.Message);

            return StatusCode(StatusCodes.Status500InternalServerError, "Unable to start quiz.");
        }
    }

    [HttpGet("learner/quiz/{attemptId:guid}")]
    public IActionResult Quiz(Guid attemptId)
    {
        // Quiz state is client-side; on refresh we redirect back to dashboard
        return RedirectToAction(nameof(Dashboard));
    }

    // POST /learner/quiz/submit
    [HttpPost("learner/quiz/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitQuiz([FromBody] SubmitQuizRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _learner.SubmitQuizAsync(_user.UserId, request, _user.PreferredLanguage, ct);
            return Json(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /learner/results  — results rendered by the dashboard SPA
    [HttpGet("learner/results")]
    public IActionResult Results() => RedirectToAction(nameof(Dashboard));

    private static string NormalizeQuizLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
            return "en";

        var normalized = lang.Trim().ToLowerInvariant();
        return SupportedQuizLanguages.Contains(normalized) ? normalized : "en";
    }
}
