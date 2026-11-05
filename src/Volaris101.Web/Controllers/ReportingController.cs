using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Infrastructure.Repositories;
using System.Text.Json;

namespace Volaris101.Web.Controllers;

[Authorize(Policy = "OrgAdminOrAbove")]
[Route("reporting")]
public class ReportingController : Controller
{
    private readonly IReportingService _reporting;
    private readonly IPeopleDirectoryService _peopleDirectory;
    private readonly IReportingAccessRepository _accessRepo;
    private readonly IQuizRepository _quizRepo;
    private readonly ICurrentUserContext _user;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _environment;

    public ReportingController(
        IReportingService reporting,
        IPeopleDirectoryService peopleDirectory,
        IReportingAccessRepository accessRepo,
        IQuizRepository quizRepo,
        ICurrentUserContext user,
        IMemoryCache cache,
        IWebHostEnvironment environment)
    {
        _reporting = reporting;
        _peopleDirectory = peopleDirectory;
        _accessRepo = accessRepo;
        _quizRepo = quizRepo;
        _user = user;
        _cache = cache;
        _environment = environment;
    }

    // GET /reporting  — Overview tab (default)
    [HttpGet(""), HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] ReportingFilterDto filter, [FromQuery] string? stateToken, CancellationToken ct)
    {
        var effectiveFilter = ResolveReportingFilter(filter, stateToken);
        var includeDrafts = _user.IsGlobalAdmin;
        var companies = await GetCachedAsync(
            BuildCacheKey("reporting-companies", _user.RoleCode, includeDrafts),
            () => _reporting.GetAvailableCompaniesAsync(ct),
            ct);
        var companyHierarchy = await GetCachedAsync(
            BuildCacheKey("reporting-company-hierarchy", _user.RoleCode, includeDrafts),
            () => BuildCompanyHierarchyContextAsync(companies, ct),
            ct);
        var quizzes   = includeDrafts
            ? await _quizRepo.GetAllActiveAsync(_user.PreferredLanguage, ct)
            : await _quizRepo.GetAllPublishedAsync(_user.PreferredLanguage, ct);
        var domains   = await GetCachedAsync(
            BuildCacheKey("reporting-domains", _user.RoleCode, includeDrafts),
            () => _reporting.GetDistinctDomainsAsync(ct),
            ct);
        var data = await GetCachedAsync(
            BuildCacheKey("overview-summary", new { Filter = effectiveFilter, Take = (int?)null }, includeDrafts),
            () => _reporting.GetOverviewAsync(effectiveFilter, includeDrafts, null, ct),
            ct);
        var overviewDisplayCount = data.TotalLearners;
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(data);
        return View("Reporting", new ReportingPageModel(data, null, effectiveFilter, effectiveFilter, "overview", null, quizzes, domains, companies, OverviewDisplayCount: overviewDisplayCount, OrganizationChildrenJson: companyHierarchy.ChildrenByParentJson, PortfolioOrganizationIdsJson: companyHierarchy.PortfolioOrganizationIdsJson, ShowHowToButton: !_user.IsLearner));
    }

    // GET /reporting/users
    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] ReportingFilterDto filter, [FromQuery] string? stateToken, CancellationToken ct)
    {
        var effectiveFilter = ResolveReportingFilter(filter, stateToken);
        var includeDrafts = _user.IsGlobalAdmin;
        var companies = await GetCachedAsync(
            BuildCacheKey("reporting-companies", _user.RoleCode, includeDrafts),
            () => _reporting.GetAvailableCompaniesAsync(ct),
            ct);
        var companyHierarchy = await GetCachedAsync(
            BuildCacheKey("reporting-company-hierarchy", _user.RoleCode, includeDrafts),
            () => BuildCompanyHierarchyContextAsync(companies, ct),
            ct);
        var initialState = GetInitialLoadState(effectiveFilter);
        var totalUsers = await GetCachedAsync(
            BuildCacheKey("users-count", initialState.EffectiveFilter, includeDrafts),
            () => _reporting.GetUserActivityCountAsync(initialState.EffectiveFilter, includeDrafts, ct),
            ct);
        var data      = await _reporting.GetUserActivityAsync(initialState.EffectiveFilter, includeDrafts, initialState.Take ?? 100, ct);
        var quizzes   = includeDrafts
            ? await _quizRepo.GetAllActiveAsync(_user.PreferredLanguage, ct)
            : await _quizRepo.GetAllPublishedAsync(_user.PreferredLanguage, ct);
        var domains   = await GetCachedAsync(
            BuildCacheKey("reporting-domains", _user.RoleCode, includeDrafts),
            () => _reporting.GetDistinctDomainsAsync(ct),
            ct);
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(data);
        return View("Reporting", new ReportingPageModel(null, data, effectiveFilter, effectiveFilter, "users", null, quizzes, domains, companies, UserActivityTotalCount: totalUsers, OrganizationChildrenJson: companyHierarchy.ChildrenByParentJson, PortfolioOrganizationIdsJson: companyHierarchy.PortfolioOrganizationIdsJson, ShowHowToButton: !_user.IsLearner));
    }

    // GET /reporting/export  — Report preview tab
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ReportingFilterDto filter, [FromQuery] string? stateToken, CancellationToken ct)
    {
        var effectiveFilter = ResolveReportingFilter(filter, stateToken);
        var includeDrafts = _user.IsGlobalAdmin;
        var companies = await GetCachedAsync(
            BuildCacheKey("reporting-companies", _user.RoleCode, includeDrafts),
            () => _reporting.GetAvailableCompaniesAsync(ct),
            ct);
        var companyHierarchy = await GetCachedAsync(
            BuildCacheKey("reporting-company-hierarchy", _user.RoleCode, includeDrafts),
            () => BuildCompanyHierarchyContextAsync(companies, ct),
            ct);
        var hasSelectedFilters = HasAnyUserSelectedFilter(effectiveFilter) || effectiveFilter.FromDate.HasValue || effectiveFilter.ToDate.HasValue;
        IReadOnlyList<ReportingExportRowDto> data = [];
        var totalRows = 0;

        if (hasSelectedFilters)
        {
            var initialState = GetInitialLoadState(effectiveFilter);
            totalRows = await GetCachedAsync(
                BuildCacheKey("export-count", initialState.EffectiveFilter, includeDrafts),
                () => _reporting.GetExportDataCountAsync(initialState.EffectiveFilter, includeDrafts, ct),
                ct);
            data = await _reporting.GetExportDataAsync(initialState.EffectiveFilter, includeDrafts, initialState.Take ?? 100, ct);
        }

        var quizzes   = includeDrafts
            ? await _quizRepo.GetAllActiveAsync(_user.PreferredLanguage, ct)
            : await _quizRepo.GetAllPublishedAsync(_user.PreferredLanguage, ct);
        var domains   = await GetCachedAsync(
            BuildCacheKey("reporting-domains", _user.RoleCode, includeDrafts),
            () => _reporting.GetDistinctDomainsAsync(ct),
            ct);
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(data);
        return View("Reporting", new ReportingPageModel(null, null, effectiveFilter, effectiveFilter, "export", data, quizzes, domains, companies, ExportPreviewTotalCount: totalRows, OrganizationChildrenJson: companyHierarchy.ChildrenByParentJson, PortfolioOrganizationIdsJson: companyHierarchy.PortfolioOrganizationIdsJson, ShowHowToButton: !_user.IsLearner));
    }

    // GET /reporting/not-started
    [HttpGet("not-started")]
    public IActionResult NotStarted([FromQuery] ReportingFilterDto filter, [FromQuery] string? stateToken)
    {
        var query = Request.QueryString.Value;
        var target = string.IsNullOrWhiteSpace(query) ? "/reporting" : $"/reporting{query}";
        return Redirect(target);
    }

    // GET /reporting/dashboard
    [HttpGet("dashboard")]
    [Authorize(Policy = "GlobalAdmin")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var data = await _reporting.GetLanguageDashboardAsync(ct);
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(data);

        var emptyFilter = new ReportingFilterDto(null, null, null, null, null, null, null, null, null);
        return View("Reporting", new ReportingPageModel(
            null,
            null,
            emptyFilter,
            emptyFilter,
            "dashboard",
            Dashboard: data,
            PortfolioOrganizationIdsJson: null,
            ShowHowToButton: !_user.IsLearner));
    }

    // GET /reporting/diagnostics/people-directory?vbu=Aion
    [HttpGet("diagnostics/people-directory")]
    [Authorize(Policy = "GlobalAdmin")]
    public async Task<IActionResult> PeopleDirectoryDiagnostic([FromQuery] string? vbu, CancellationToken ct)
    {
        var company = vbu?.Trim();
        if (string.IsNullOrWhiteSpace(company))
            return BadRequest(new { error = "Query string parameter 'vbu' is required." });

        try
        {
            var employees = await _peopleDirectory.GetEmployeesByCompaniesAsync(new[] { company }, ct);
            var sample = employees
                .Take(10)
                .Select(employee => new
                {
                    employee.EmployeeName,
                    employee.Email,
                    employee.Company,
                    employee.Vbu,
                    employee.EmployeeUserId,
                    employee.OrganizationId,
                    employee.ManagerUserPrincipalName
                })
                .ToArray();

            return Json(new
            {
                requestedVbu = company,
                count = employees.Count,
                sample
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                requestedVbu = company,
                error = ex.GetBaseException().Message
            });
        }
    }

    // POST /reporting/state
    [HttpPost("state")]
    public IActionResult SaveState([FromBody] ReportingFilterDto filter)
    {
        var token = Guid.NewGuid().ToString("N");
        var cachedFilter = filter with { RefreshToken = token };
        _cache.Set(BuildReportingStateCacheKey(token), cachedFilter, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = GetCacheExpiration(),
            SlidingExpiration = TimeSpan.FromHours(6)
        });

        return Json(new { token });
    }

    // GET /reporting/diagnostics/portfolio?companies=30300&companies=30505
    [HttpGet("diagnostics/portfolio")]
    [Authorize(Policy = "GlobalAdmin")]
    public async Task<IActionResult> PortfolioDiagnostic([FromQuery] ReportingFilterDto filter, CancellationToken ct)
    {
        try
        {
            var data = await _reporting.GetPortfolioReportDiagnosticAsync(filter, ct);
            return Json(data);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /reporting/how-to
    [HttpGet("how-to")]
    public IActionResult HowTo()
    {
        var webRoot = _environment.WebRootPath ?? string.Empty;
        var path = Path.Combine(webRoot, "docs", "reporting-how-to.pdf");
        if (!System.IO.File.Exists(path))
            return NotFound();

        return PhysicalFile(path, "application/pdf");
    }

    private ReportingInitialState GetInitialLoadState(ReportingFilterDto filter)
    {
        if (HasAnyUserSelectedFilter(filter) || filter.FromDate.HasValue || filter.ToDate.HasValue)
            return new ReportingInitialState(filter, filter, null);

        if (!_user.IsGlobalAdmin)
        {
            return new ReportingInitialState(filter, filter, 100);
        }

        var to = DateTimeOffset.UtcNow.Date;
        var from = to.AddMonths(-2);
        var effective = filter with
        {
            FromDate = from,
            ToDate = to
        };
        return new ReportingInitialState(filter, effective, null);
    }

    private ReportingSummaryState GetOverviewSummaryState(ReportingFilterDto filter, ReportingInitialState initialState)
    {
        if (HasAnyUserSelectedFilter(filter) || filter.FromDate.HasValue || filter.ToDate.HasValue)
            return new ReportingSummaryState(filter, null);

        if (_user.IsGlobalAdmin)
            return new ReportingSummaryState(new ReportingFilterDto(null, null, null, null, null, null, null, null, null), null);

        return new ReportingSummaryState(initialState.EffectiveFilter, initialState.Take);
    }

    private static bool HasAnyOverviewSelectedFilter(ReportingFilterDto filter)
    {
        var hasStatus = filter.Status is { Length: > 0 } && filter.Status.Any(s => !string.IsNullOrWhiteSpace(s));
        return HasAnyUserSelectedFilter(filter) || filter.FromDate.HasValue || filter.ToDate.HasValue || hasStatus;
    }

    private ReportingFilterDto ResolveReportingFilter(ReportingFilterDto filter, string? stateToken)
    {
        var resolved = string.IsNullOrWhiteSpace(stateToken)
            ? filter
            : _cache.TryGetValue(BuildReportingStateCacheKey(stateToken), out ReportingFilterDto? cached) && cached is not null
                ? cached
                : filter;

        return SanitizeReportingFilter(resolved);
    }

    private async Task<ReportingCompanyHierarchyContext> BuildCompanyHierarchyContextAsync(
        IReadOnlyList<CompanyGroupDto> availableCompanies,
        CancellationToken ct)
    {
        var visibleOrganizationIds = availableCompanies
            .SelectMany(group => group.Companies)
            .Select(company => company.OrganizationId)
            .Where(id => id > 0)
            .ToHashSet();

        var vbuRows = await _accessRepo.GetVbuMasterListAsync(ct);
        var childrenByParent = ReportingCompanySourceBuilder.BuildVisibleChildrenMap(vbuRows, visibleOrganizationIds);

        var portfolioOrganizationIds = availableCompanies
            .SelectMany(group => group.Companies)
            .Where(company => company.IsPortfolio)
            .Select(company => company.OrganizationId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        return new ReportingCompanyHierarchyContext(
            childrenByParent,
            JsonSerializer.Serialize(childrenByParent),
            JsonSerializer.Serialize(portfolioOrganizationIds));
    }

    private static ReportingFilterDto SanitizeReportingFilter(ReportingFilterDto filter)
    {
        var companies = filter.Companies?
            .Where(id => id > 0 && !ReportingCompanySourceBuilder.IsExcludedOrganizationId(id))
            .Distinct()
            .ToArray();

        return filter with
        {
            Companies = companies is { Length: > 0 } ? companies : null
        };
    }

    private string BuildCacheKey(string name, object? filter, bool includeDrafts)
    {
        var filterKey = filter is null ? string.Empty : JsonSerializer.Serialize(filter);
        return $"reporting:{name}:{_user.UserId:N}:{_user.RoleCode}:{includeDrafts}:{filterKey}";
    }

    private static string BuildReportingStateCacheKey(string token) => $"reporting-state:{token}";

    private async Task<T> GetCachedAsync<T>(string key, Func<Task<T>> factory, CancellationToken ct)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var value = await factory();
        _cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = GetCacheExpiration(),
            SlidingExpiration = TimeSpan.FromHours(6)
        });

        return value;
    }

    private static DateTimeOffset GetCacheExpiration()
    {
        return DateTimeOffset.UtcNow.Date.AddDays(1);
    }

    private static bool HasAnyUserSelectedFilter(ReportingFilterDto filter)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(filter.SearchTerm);
        var hasDomain = !string.IsNullOrWhiteSpace(filter.Domain);
        var hasDomains = filter.Domains is { Length: > 0 } && filter.Domains.Any(d => !string.IsNullOrWhiteSpace(d));
        var hasModule = filter.ModuleOrder.HasValue;
        var hasCompanies = filter.Companies is { Length: > 0 } && filter.Companies.Any(c => c > 0);
        var hasPortfolio = !string.IsNullOrWhiteSpace(filter.Portfolio);
        var hasLastActive = filter.LastActiveDays.HasValue;
        var hasStatus = filter.Status is { Length: > 0 } && filter.Status.Any(s => !string.IsNullOrWhiteSpace(s));
        var hasIncludeLeavers = filter.IncludeLeavers;

        return hasSearch || hasDomain || hasDomains || hasModule || hasCompanies || hasPortfolio || hasLastActive || hasStatus || hasIncludeLeavers;
    }

    private sealed record ReportingInitialState(
        ReportingFilterDto AppliedFilter,
        ReportingFilterDto EffectiveFilter,
        int? Take);

    private sealed record ReportingSummaryState(
        ReportingFilterDto Filter,
        int? Take);

    private sealed record ReportingCompanyHierarchyContext(
        IReadOnlyDictionary<int, IReadOnlyList<int>> ChildrenByParent,
        string ChildrenByParentJson,
        string PortfolioOrganizationIdsJson);

    /*
    // POST /reporting/export/csv
    [HttpPost("export/csv")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportCsv([FromBody] ReportingFilterDto filter, CancellationToken ct)
    {
        var data = await _reporting.GetExportDataAsync(filter, _user.IsGlobalAdmin, null, ct);

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8);
        using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);

        csv.WriteHeader<ReportingExportRowDto>();
        await csv.NextRecordAsync();
        foreach (var row in data)
        {
            csv.WriteRecord(row);
            await csv.NextRecordAsync();
        }
        await writer.FlushAsync();

        var filename = $"Volaris101_Report_{DateTimeOffset.UtcNow:yyyyMMdd}.csv";
        return File(ms.ToArray(), "text/csv", filename);
    }
    */

    // POST /reporting/export/monthly
    [HttpPost("export/monthly")]
    [Authorize(Policy = "GlobalAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportMonthly([FromBody] ReportingFilterDto filter, CancellationToken ct)
    {
        if (!filter.FromDate.HasValue || !filter.ToDate.HasValue || filter.FromDate > filter.ToDate)
            return BadRequest("A valid date range is required.");

        var data = await _reporting.GetMonthlyCompletionReportAsync(filter, ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Monthly Report");

        var headers = new[] { "Name", "EmployeeID", "Portfolio" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var r in data)
        {
            ws.Cell(row, 1).Value = r.Name;
            ws.Cell(row, 2).Value = r.EmployeeId;
            ws.Cell(row, 3).Value = r.Portfolio;
            row++;
        }

        ws.Range(1, 1, Math.Max(1, row - 1), 3).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var from = filter.FromDate.Value;
        var to = filter.ToDate.Value;
        var reportLabel = from.Year == to.Year && from.Month == to.Month
            ? from.ToString("yyyy-MM")
            : $"{from:yyyy-MM-dd}_to_{to:yyyy-MM-dd}";
        var filename = $"Volaris101 Monthly_{reportLabel} Report.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    // POST /reporting/export/portfolio
    [HttpPost("export/portfolio")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportPortfolio([FromBody] ReportingFilterDto filter, CancellationToken ct)
    {
        PortfolioReportDto report;
        try
        {
            report = await _reporting.GetPortfolioReportAsync(filter, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        using var wb = new XLWorkbook();
        var analysis = wb.Worksheets.Add("Analysis");
        var analysisHeaders = new[]
        {
            "Organization", "Total People", "#Online Complete",
            "#Online in progress", "%Online Complete", "%Online in progress"
        };

        for (var i = 0; i < analysisHeaders.Length; i++)
        {
            analysis.Cell(1, i + 1).Value = analysisHeaders[i];
            analysis.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var analysisRow = 2;
        foreach (var org in report.Organizations)
        {
            analysis.Cell(analysisRow, 1).Value = org.OrganizationName;
            analysis.Cell(analysisRow, 2).Value = org.TotalPeople;
            analysis.Cell(analysisRow, 3).Value = org.OnlineCompleteCount;
            analysis.Cell(analysisRow, 4).Value = org.OnlineInProgressCount;
            analysis.Cell(analysisRow, 5).Value = org.TotalPeople == 0 ? 0d : (double)org.OnlineCompleteCount / org.TotalPeople;
            analysis.Cell(analysisRow, 6).Value = org.TotalPeople == 0 ? 0d : (double)org.OnlineInProgressCount / org.TotalPeople;
            analysisRow++;
        }

        if (report.Organizations.Count > 0)
        {
            var totalPeople = report.Organizations.Sum(o => o.TotalPeople);
            var totalComplete = report.Organizations.Sum(o => o.OnlineCompleteCount);
            var totalInProgress = report.Organizations.Sum(o => o.OnlineInProgressCount);

            analysis.Cell(analysisRow, 1).Value = "Total";
            analysis.Cell(analysisRow, 2).Value = totalPeople;
            analysis.Cell(analysisRow, 3).Value = totalComplete;
            analysis.Cell(analysisRow, 4).Value = totalInProgress;
            analysis.Cell(analysisRow, 5).Value = totalPeople == 0 ? 0d : (double)totalComplete / totalPeople;
            analysis.Cell(analysisRow, 6).Value = totalPeople == 0 ? 0d : (double)totalInProgress / totalPeople;
            analysis.Range(analysisRow, 1, analysisRow, analysisHeaders.Length).Style.Font.Bold = true;
            analysisRow++;
        }

        if (analysisRow > 2)
            analysis.Range(2, 5, analysisRow - 1, 6).Style.NumberFormat.Format = "0.0%";

        analysis.Range(1, 1, Math.Max(1, analysisRow - 1), analysisHeaders.Length).SetAutoFilter();
        analysis.SheetView.FreezeRows(1);
        analysis.Columns().AdjustToContents();

        foreach (var org in report.Organizations)
        {
            var sheet = wb.Worksheets.Add(GetUniqueWorksheetName(wb, org.OrganizationName));
            var headers = new[] { "Employee", "VBU", "Online", "Last participated" };

            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
                sheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            var row = 2;
            foreach (var item in org.Rows)
            {
                sheet.Cell(row, 1).Value = item.Employee;
                sheet.Cell(row, 2).Value = item.Vbu;
                sheet.Cell(row, 3).Value = item.Online;
                sheet.Cell(row, 4).Value = item.LastParticipated?.ToString("yyyy-MM-dd") ?? string.Empty;
                row++;
            }

            sheet.Range(1, 1, Math.Max(1, row - 1), headers.Length).SetAutoFilter();
            sheet.SheetView.FreezeRows(1);
            sheet.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var safePortfolio = string.Concat(report.PortfolioName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
        if (string.IsNullOrWhiteSpace(safePortfolio))
            safePortfolio = "Portfolio";
        var filename = $"Volaris101_{safePortfolio}_Certification_Report_{DateTimeOffset.UtcNow:yyyyMMdd}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    // POST /reporting/export/xlsx
    [HttpPost("export/xlsx")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportXlsx([FromBody] ReportingFilterDto filter, CancellationToken ct)
    {
        var data = await _reporting.GetExportDataAsync(filter, _user.IsGlobalAdmin, null, ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Volaris 101 Report");

        // Header row
        var headers = new[] { "Full Name", "Email", "Employee ID", "Organization", "Portfolio",
            "Status", "Fully Completed", "Completed At", "Last Active" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        int row = 2;
        foreach (var r in data)
        {
            ws.Cell(row, 1).Value = r.FullName;
            ws.Cell(row, 2).Value = r.Email;
            ws.Cell(row, 3).Value = r.EmployeeId;
            ws.Cell(row, 4).Value = r.Company;
            ws.Cell(row, 5).Value = r.Portfolio;
            ws.Cell(row, 6).Value = GetDetailActivityStatus(r);
            ws.Cell(row, 7).Value = r.IsFullyCompleted ? "Yes" : "No";
            ws.Cell(row, 8).Value = r.CompletedAt?.ToString("yyyy-MM-dd") ?? "";
            ws.Cell(row, 9).Value = r.LastActiveAt?.ToString("yyyy-MM-dd") ?? "";
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var filename = $"Volaris101_Detail_Activity_Report_{DateTimeOffset.UtcNow:yyyyMMdd}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    private static string GetDetailActivityStatus(ReportingExportRowDto row) =>
        row.IsFullyCompleted
            ? "Completed"
            : row.LastActiveAt.HasValue
                ? "In Progress"
                : "Not Started";

    // POST /reporting/export/not-started
    [HttpPost("export/not-started")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportNotStarted([FromBody] ReportingFilterDto filter, CancellationToken ct)
    {
        var data = await _reporting.GetNotStartedUsersAsync(filter, ct);
        if (data.RequiresCompanySelection)
            return BadRequest("Select at least one organization to export not started users.");

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Not Started Users");

        var headers = new[] { "Employee Name", "Email", "Manager Name", "Manager Email", "Organization", "Portfolio" };
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var item in data.Rows)
        {
            ws.Cell(row, 1).Value = item.EmployeeName;
            ws.Cell(row, 2).Value = item.Email;
            ws.Cell(row, 3).Value = item.ManagerName;
            ws.Cell(row, 4).Value = item.ManagerEmail;
            ws.Cell(row, 5).Value = item.Company;
            ws.Cell(row, 6).Value = item.Portfolio;
            row++;
        }

        ws.Range(1, 1, Math.Max(1, row - 1), headers.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var filename = $"Volaris101_Not_Started_{DateTimeOffset.UtcNow:yyyyMMdd}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    private static string GetUniqueWorksheetName(XLWorkbook workbook, string requestedName)
    {
        var invalidChars = new HashSet<char>(new[] { ':', '\\', '/', '?', '*', '[', ']' });
        var baseName = new string(requestedName
            .Where(ch => !invalidChars.Contains(ch))
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "Sheet";

        if (baseName.Length > 31)
            baseName = baseName[..31];

        var candidate = baseName;
        var suffix = 2;
        while (workbook.Worksheets.Any(ws => string.Equals(ws.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            var suffixText = $" {suffix}";
            var trimmed = baseName.Length > 31 - suffixText.Length
                ? baseName[..(31 - suffixText.Length)]
                : baseName;
            candidate = trimmed + suffixText;
            suffix++;
        }

        return candidate;
    }
}

public record ReportingPageModel(
    ReportingOverviewDto? Overview,
    IReadOnlyList<UserActivityRowDto>? UserActivity,
    ReportingFilterDto Filter,
    ReportingFilterDto AppliedFilter,
    string ActiveTab,
    IReadOnlyList<ReportingExportRowDto>? ExportPreview = null,
    IReadOnlyList<Volaris101.Domain.Entities.Quiz>? Quizzes = null,
    IReadOnlyList<string>? Domains = null,
    IReadOnlyList<Volaris101.Application.Dtos.CompanyGroupDto>? AvailableCompanies = null,
    int OverviewDisplayCount = 0,
    int UserActivityTotalCount = 0,
    int ExportPreviewTotalCount = 0,
    ReportingDashboardDto? Dashboard = null,
    NotStartedUsersResultDto? NotStartedUsers = null,
    string? OrganizationChildrenJson = null,
    string? PortfolioOrganizationIdsJson = null,
    bool ShowHowToButton = false
);
