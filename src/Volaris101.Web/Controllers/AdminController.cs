using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;

namespace Volaris101.Web.Controllers;

[Authorize(Policy = "GlobalAdmin")]
[Route("admin")]
public class AdminController : Controller
{
    private static readonly HashSet<string> SupportedCmsLanguages = new() { "en", "de", "es", "fr", "pt" };

    private readonly ICmsService _cms;
    private readonly IUserAdminService _userAdmin;
    private readonly IAccessGroupService _accessGroups;
    private readonly ICurrentUserContext _user;
    private readonly ILogger<AdminController> _log;

    public AdminController(ICmsService cms, IUserAdminService userAdmin, IAccessGroupService accessGroups, ICurrentUserContext user, ILogger<AdminController> log)
    {
        _cms = cms;
        _userAdmin = userAdmin;
        _accessGroups = accessGroups;
        _user = user;
        _log = log;
    }

    // ─── CMS — By Quiz ─────────────────────────────────────────────────────────

    // GET /admin  or  GET /admin/cms
    [HttpGet(""), HttpGet("cms")]
    public async Task<IActionResult> Cms(CancellationToken ct)
    {
        var quizzes = await _cms.GetQuizListAsync(_user.PreferredLanguage, ct);
        return View("Cms", new CmsPageModel(quizzes, null, null, "byquiz"));
    }

    // GET /admin/cms/quiz/{quizId}
    [HttpGet("cms/quiz/{quizId:guid}")]
    public async Task<IActionResult> QuizDetail(Guid quizId, [FromQuery] string? lang, CancellationToken ct)
    {
        var selectedLanguage = NormalizeCmsLanguage(lang ?? _user.PreferredLanguage);
        var quiz = await _cms.GetQuizDetailAsync(quizId, selectedLanguage, ct);
        if (quiz == null) return NotFound();
        var allQuizzes = await _cms.GetQuizListAsync(selectedLanguage, ct);
        return View("Cms", new CmsPageModel(allQuizzes, quiz, null, "byquiz"));
    }

    // POST /admin/cms/quiz/add
    [HttpPost("cms/quiz/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuiz([FromBody] AddQuizRequestBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Title))
            return BadRequest(new { error = "Title is required." });

        var quiz = await _cms.AddQuizAsync(body.Title, ct);
        return Json(quiz);
    }

    // PUT /admin/cms/quiz/{quizId}
    [HttpPut("cms/quiz/{quizId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuiz(Guid quizId, [FromBody] UpdateQuizRequestBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Title))
            return BadRequest(new { error = "Title is required." });

        try
        {
            var quiz = await _cms.UpdateQuizAsync(quizId, body.Title, ct);
            return Json(quiz);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT /admin/cms/quiz/{quizId}/publish
    [HttpPut("cms/quiz/{quizId:guid}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishQuiz(Guid quizId, CancellationToken ct)
    {
        try
        {
            var quiz = await _cms.PublishQuizAsync(quizId, _user.UserId, ct);
            return Json(quiz);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /admin/cms/question/add
    [HttpPost("cms/question/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion([FromBody] AddQuestionRequestBody body, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var question = await _cms.AddQuestionAsync(body.QuizId, body.Question, ct);
        return Json(question);
    }

    // GET /admin/cms/question/{questionId}
    [HttpGet("cms/question/{questionId:guid}")]
    public async Task<IActionResult> GetQuestion(Guid questionId, [FromQuery] string? lang, CancellationToken ct)
    {
        try
        {
            var question = await _cms.GetQuestionAsync(questionId, NormalizeCmsLanguage(lang ?? _user.PreferredLanguage), ct);
            return Json(question);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // PUT /admin/cms/question/{questionId}
    [HttpPut("cms/question/{questionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuestion(Guid questionId, [FromBody] CmsQuestionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var question = await _cms.UpdateQuestionAsync(questionId, dto, ct);
        return Json(question);
    }

    // DELETE /admin/cms/question/{questionId}
    [HttpDelete("cms/question/{questionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(Guid questionId, CancellationToken ct)
    {
        await _cms.DeleteQuestionAsync(questionId, ct);
        return Ok();
    }

    // GET /admin/cms/questions  — All questions view (AJAX)
    [HttpGet("cms/questions")]
    public async Task<IActionResult> AllQuestions(
        [FromQuery] string? search,
        [FromQuery] Guid? quizId,
        [FromQuery] string? lang,
        CancellationToken ct)
    {
        var selectedLanguage = NormalizeCmsLanguage(lang ?? _user.PreferredLanguage);
        var questions = await _cms.GetAllQuestionsAsync(search, quizId, selectedLanguage, ct);
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(questions);
        var allQuizzes = await _cms.GetQuizListAsync(selectedLanguage, ct);
        return View("Cms", new CmsPageModel(allQuizzes, null, questions, "allq", null, selectedLanguage));
    }

    // ─── User Management ───────────────────────────────────────────────────────

    // GET /admin/users
    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, CancellationToken ct)
    {
        var totalUsers = await _userAdmin.GetUsersCountAsync(search, ct);
        var users = await _userAdmin.GetUsersAsync(search, 100, ct);
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(users);
        var allQuizzes = await _cms.GetQuizListAsync(_user.PreferredLanguage, ct);
        return View("Cms", new CmsPageModel(allQuizzes, null, null, "roles", users, "en", totalUsers));
    }

    // GET /admin/groups
    [HttpGet("groups")]
    public async Task<IActionResult> Groups([FromQuery] string? search, [FromQuery] string? type, [FromQuery] bool? includeInactive, [FromQuery] string? organizationSearch, CancellationToken ct)
    {
        var groups = await _accessGroups.GetGroupListAsync(search, type, includeInactive ?? true, organizationSearch, ct);
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(groups);

        var allQuizzes = await _cms.GetQuizListAsync(_user.PreferredLanguage, ct);
        return View("Cms", new CmsPageModel(allQuizzes, null, null, "groups", null, "en", 0, groups));
    }

    // GET /admin/groups/detail/{accessGroupId}
    [HttpGet("groups/detail/{accessGroupId:guid}")]
    public async Task<IActionResult> GetGroupDetail(Guid accessGroupId, CancellationToken ct)
    {
        try
        {
            var group = await _accessGroups.GetGroupDetailAsync(accessGroupId, ct);
            return Json(group);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load access group {AccessGroupId}", accessGroupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to load access group: {ex.GetBaseException().Message}" });
        }
    }

    // GET /admin/groups/{accessGroupId}/users
    [HttpGet("groups/{accessGroupId:guid}/users")]
    public async Task<IActionResult> GetGroupUsers(Guid accessGroupId, CancellationToken ct)
    {
        try
        {
            var users = await _accessGroups.GetGroupUsersAsync(accessGroupId, ct);
            return Json(users);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load users for access group {AccessGroupId}", accessGroupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to load group users: {ex.GetBaseException().Message}" });
        }
    }

    // GET /admin/groups/user-candidates
    [HttpGet("groups/user-candidates")]
    public async Task<IActionResult> SearchGroupUserCandidates([FromQuery] string? term, CancellationToken ct)
    {
        try
        {
            var users = await _userAdmin.SearchMergeCandidatesAsync(term, 0, ct);
            return Json(users);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to search group user candidates for term {Term}", term);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to search users: {ex.GetBaseException().Message}" });
        }
    }

    // POST /admin/groups/{accessGroupId}/users/{userId}
    [HttpPost("groups/{accessGroupId:guid}/users/{userId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGroupUser(Guid accessGroupId, Guid userId, CancellationToken ct)
    {
        if (userId == _user.UserId)
            return BadRequest(new { error = "You cannot change your own access." });

        try
        {
            var users = await _accessGroups.AddUserToGroupAsync(accessGroupId, userId, _user.UserId, ct);
            return Json(users);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to add user {UserId} to access group {AccessGroupId}", userId, accessGroupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to add user to group: {ex.GetBaseException().Message}" });
        }
    }

    // DELETE /admin/groups/{accessGroupId}/users/{userId}
    [HttpDelete("groups/{accessGroupId:guid}/users/{userId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGroupUser(Guid accessGroupId, Guid userId, CancellationToken ct)
    {
        if (userId == _user.UserId)
            return BadRequest(new { error = "You cannot change your own access." });

        try
        {
            var users = await _accessGroups.RemoveUserFromGroupAsync(accessGroupId, userId, _user.UserId, ct);
            return Json(users);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to remove user {UserId} from access group {AccessGroupId}", userId, accessGroupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to remove user from group: {ex.GetBaseException().Message}" });
        }
    }

    // PUT /admin/groups/{accessGroupId}/status
    [HttpPut("groups/{accessGroupId:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetGroupStatus(Guid accessGroupId, [FromBody] SetStatusRequestBody body, CancellationToken ct)
    {
        try
        {
            var group = await _accessGroups.SetGroupActiveStatusAsync(accessGroupId, body.IsActive, _user.UserId, ct);
            return Json(group);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to update access group {AccessGroupId}", accessGroupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to update access group: {ex.GetBaseException().Message}" });
        }
    }

    // POST /admin/groups/reseed
    [HttpPost("groups/reseed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReseedGroups(CancellationToken ct)
    {
        try
        {
            var result = await _accessGroups.SeedSystemManagedGroupsAsync(ct);
            return Json(result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to reseed access groups");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to reseed groups: {ex.GetBaseException().Message}" });
        }
    }

    // POST /admin/users/add
    [HttpPost("users/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUser([FromBody] AddUserRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var user = await _userAdmin.AddUserAsync(request, _user.UserId, ct);
            return Json(user);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /admin/users/merge/candidates
    [HttpGet("users/merge/candidates")]
    public async Task<IActionResult> SearchMergeCandidates([FromQuery] string? term, [FromQuery] int? take, CancellationToken ct)
    {
        try
        {
            var results = await _userAdmin.SearchMergeCandidatesAsync(term, take ?? 25, ct);
            return Json(results);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to search merge candidates for term {Term}", term);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to search users: {ex.GetBaseException().Message}" });
        }
    }

    // POST /admin/users/merge/preview
    [HttpPost("users/merge/preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewMerge([FromBody] MergeUsersRequestDto request, CancellationToken ct)
    {
        try
        {
            var preview = await _userAdmin.PreviewMergeUsersAsync(request, _user.UserId, ct);
            return Json(preview);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to preview user merge for target {TargetUserId}", request.TargetUserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to preview merge: {ex.GetBaseException().Message}" });
        }
    }

    // POST /admin/users/merge/execute
    [HttpPost("users/merge/execute")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteMerge([FromBody] MergeUsersRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await _userAdmin.ExecuteMergeUsersAsync(request, _user.UserId, ct);
            return Json(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to execute user merge for target {TargetUserId}", request.TargetUserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to merge users: {ex.GetBaseException().Message}" });
        }
    }

    // PUT /admin/users/{userId}/role
    [HttpPut("users/{userId:guid}/role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRole(Guid userId, [FromBody] UpdateRoleRequestDto request, CancellationToken ct)
    {
        // Prevent self-role-change
        if (userId == _user.UserId)
            return BadRequest(new { error = "You cannot change your own role." });

        try
        {
            var user = await _userAdmin.UpdateRoleAsync(userId, request, _user.UserId, ct);
            return Json(user);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /admin/users/{userId}/access
    [HttpGet("users/{userId:guid}/access")]
    public async Task<IActionResult> GetAccess(Guid userId, CancellationToken ct)
    {
        try
        {
            var access = await _userAdmin.GetAccessAsync(userId, ct);
            return Json(access);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load access for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to load user access: {ex.GetBaseException().Message}" });
        }
    }

    // PUT /admin/users/{userId}/access
    [HttpPut("users/{userId:guid}/access")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccess(Guid userId, [FromBody] UpdateUserAccessRequestDto request, CancellationToken ct)
    {
        // Prevent self-role-change
        if (userId == _user.UserId)
            return BadRequest(new { error = "You cannot change your own access." });

        try
        {
            var access = await _userAdmin.UpdateAccessAsync(userId, request, _user.UserId, ct);
            return Json(access);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to update access for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to update user access: {ex.GetBaseException().Message}" });
        }
    }

    // GET /admin/users/{userId}/groups
    [HttpGet("users/{userId:guid}/groups")]
    public async Task<IActionResult> GetUserGroups(Guid userId, CancellationToken ct)
    {
        try
        {
            var groups = await _accessGroups.GetUserGroupAssignmentsAsync(userId, ct);
            return Json(groups);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load group assignments for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to load group assignments: {ex.GetBaseException().Message}" });
        }
    }

    // PUT /admin/users/{userId}/groups
    [HttpPut("users/{userId:guid}/groups")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserGroups(Guid userId, [FromBody] UpdateUserGroupAssignmentsRequestDto request, CancellationToken ct)
    {
        if (userId == _user.UserId)
            return BadRequest(new { error = "You cannot change your own access." });

        try
        {
            var access = await _userAdmin.GetAccessAsync(userId, ct);
            var requestedGroups = request.AccessGroupIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? [];
            if (access.RoleCode is not ("ReportingAdmin" or "OrgAdmin" or "PortfolioAdmin") && requestedGroups.Count > 0)
                return BadRequest(new { error = "Group access can only be assigned to Reporting Admin users." });

            var groups = await _accessGroups.SetUserGroupAssignmentsAsync(userId, request.AccessGroupIds, _user.UserId, ct);
            return Json(groups);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to update group assignments for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to update group assignments: {ex.GetBaseException().Message}" });
        }
    }

    // GET /admin/lookups/organizations
    [HttpGet("lookups/organizations")]
    public async Task<IActionResult> SearchOrganizations([FromQuery] string? term, CancellationToken ct)
    {
        try
        {
            var results = await _userAdmin.SearchOrganizationsAsync(term, ct);
            return Json(results);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to search organization lookup for term {Term}", term);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to search VBUs: {ex.GetBaseException().Message}" });
        }
    }

    // GET /admin/lookups/organization-companies
    [HttpGet("lookups/organization-companies")]
    public async Task<IActionResult> SearchOrganizationCompanies([FromQuery] string? term, CancellationToken ct)
    {
        try
        {
            var results = await _userAdmin.SearchOrganizationCompaniesAsync(term, ct);
            return Json(results);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to search organization/company lookup for term {Term}", term);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to search companies: {ex.GetBaseException().Message}" });
        }
    }

    // GET /admin/lookups/portfolios
    [HttpGet("lookups/portfolios")]
    public async Task<IActionResult> SearchPortfolios([FromQuery] string? term, CancellationToken ct)
    {
        try
        {
            var results = await _userAdmin.SearchPortfoliosAsync(term, ct);
            return Json(results);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to search portfolio lookup for term {Term}", term);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Failed to search portfolios: {ex.GetBaseException().Message}" });
        }
    }

    // PUT /admin/users/{userId}/status
    [HttpPut("users/{userId:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid userId, [FromBody] SetStatusRequestBody body, CancellationToken ct)
    {
        if (userId == _user.UserId)
            return BadRequest(new { error = "You cannot deactivate your own account." });

        try
        {
            var user = await _userAdmin.SetActiveStatusAsync(userId, body.IsActive, _user.UserId, ct);
            return Json(user);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static string NormalizeCmsLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
            return "en";

        var normalized = lang.Trim().ToLowerInvariant();
        return SupportedCmsLanguages.Contains(normalized) ? normalized : "en";
    }
}

// ─── Auth Controller ──────────────────────────────────────────────────────────

[Route("auth")]
public class AuthController : Controller
{
    private const string PostLogoutRedirectUrl = "https://ourvolaris.sharepoint.com";

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        var props = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = returnUrl ?? "/"
        };
        return Challenge(props, Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect(PostLogoutRedirectUrl);
    }

    [HttpGet("access-denied")]
    [AllowAnonymous]
    public IActionResult AccessDenied([FromQuery] string? reason)
    {
        ViewData["Reason"] = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return View();
    }
}

// ─── Error Controller ─────────────────────────────────────────────────────────

[Route("error")]
[AllowAnonymous]
public class ErrorController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}

// ─── Request bodies ───────────────────────────────────────────────────────────
public record AddQuizRequestBody(string Title);
public record UpdateQuizRequestBody(string Title);
public record AddQuestionRequestBody(Guid QuizId, CmsQuestionDto Question);
public record SetStatusRequestBody(bool IsActive);

// ─── Page models ──────────────────────────────────────────────────────────────
public record CmsPageModel(
    IReadOnlyList<CmsQuizSummaryDto> Quizzes,
    CmsQuizDetailDto? SelectedQuiz,
    IReadOnlyList<CmsQuestionDto>? AllQuestions,
    string ActiveTab,
    IReadOnlyList<AdminUserDto>? Users = null,
    string SelectedLanguage = "en",
    int UserTotalCount = 0,
    IReadOnlyList<AccessGroupListItemDto>? Groups = null
);
