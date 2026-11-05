using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Domain.Entities;
using Volaris101.Web.Controllers;

namespace Volaris101.Tests;

public class ReportingControllerTests
{
    [Fact]
    public async Task PeopleDirectoryDiagnostic_IncludesManagerUserPrincipalName()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var peopleDirectory = new NoOpPeopleDirectoryService
        {
            Employees =
            [
                new PeopleDirectoryEmployeeDto(
                    "Taylor Contractor",
                    "taylor@example.com",
                    "Aion",
                    "Aion",
                    123,
                    30505,
                    "Contractor - ourVolaris Disallowed")
            ]
        };
        var controller = CreateController(
            new TestCurrentUserContext("GlobalAdmin"),
            cache,
            new RecordingReportingService(),
            new RecordingQuizRepository([], []),
            peopleDirectory: peopleDirectory);

        var result = await controller.PeopleDirectoryDiagnostic("Aion", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.SerializeToDocument(json.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).RootElement;
        var sample = payload.GetProperty("sample");
        Assert.Equal(
            "Contractor - ourVolaris Disallowed",
            sample[0].GetProperty("managerUserPrincipalName").GetString());
    }

    [Fact]
    public async Task SaveState_ReturnsTokenAndCachesSnapshot()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateController(
            new TestCurrentUserContext("Learner"),
            cache,
            new RecordingReportingService(),
            new RecordingQuizRepository([], []));

        var filter = new ReportingFilterDto(
            SearchTerm: "acme",
            Domain: null,
            ModuleOrder: null,
            LastActiveDays: null,
            Companies: [30300, 30505],
            Portfolio: null,
            FromDate: null,
            ToDate: null);

        var result = controller.SaveState(filter);

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(json.Value))!;
        var token = payload["token"];

        Assert.True(cache.TryGetValue($"reporting-state:{token}", out ReportingFilterDto? cachedFilter));
        Assert.NotNull(cachedFilter);
        Assert.Equal("acme", cachedFilter!.SearchTerm);
        Assert.Equal(token, cachedFilter.RefreshToken);
    }

    [Fact]
    public async Task SaveState_PreservesDateRangeInCachedSnapshot()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateController(
            new TestCurrentUserContext("Learner"),
            cache,
            new RecordingReportingService(),
            new RecordingQuizRepository([], []));
        var from = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero);

        var result = controller.SaveState(new ReportingFilterDto(
            SearchTerm: null,
            Domain: null,
            ModuleOrder: null,
            LastActiveDays: null,
            Companies: null,
            Portfolio: null,
            FromDate: from,
            ToDate: to));

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(json.Value))!;
        var token = payload["token"];

        Assert.True(cache.TryGetValue($"reporting-state:{token}", out ReportingFilterDto? cachedFilter));
        Assert.NotNull(cachedFilter);
        Assert.Equal(from, cachedFilter!.FromDate);
        Assert.Equal(to, cachedFilter.ToDate);
    }

    [Fact]
    public async Task Overview_WithStateToken_UsesCachedFilterAndPublishedQuizzesForNonGlobalAdmin()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            OverviewResult = new ReportingOverviewDto(
                12,
                4,
                33,
                2,
                [],
                [])
        };
        var quizRepo = new RecordingQuizRepository(
            activeQuizzes: [CreateQuiz(1, true), CreateQuiz(2, false)],
            publishedQuizzes: [CreateQuiz(1, true)]);
        var controller = CreateController(new TestCurrentUserContext("OrgAdmin"), cache, reporting, quizRepo);
        var saved = controller.SaveState(new ReportingFilterDto("north", null, null, null, null, null, null, null));
        var token = JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(((JsonResult)saved).Value))!["token"];

        var result = await controller.Overview(
            new ReportingFilterDto(null, null, null, null, null, null, null, null),
            token,
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportingPageModel>(view.Model);

        Assert.Equal("overview", model.ActiveTab);
        Assert.Equal("north", reporting.OverviewCalls.Single().Filter.SearchTerm);
        Assert.Null(reporting.OverviewCalls.Single().Take);
        Assert.True(quizRepo.GetAllPublishedCalled);
        Assert.False(quizRepo.GetAllActiveCalled);
        Assert.Single(model.Quizzes!);
        Assert.True(model.ShowHowToButton);
    }

    [Fact]
    public async Task Overview_NoFilters_ShowsScopedSummaryUsingOverviewService()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            OverviewResult = new ReportingOverviewDto(
                12,
                4,
                33,
                2,
                [],
                []),
            Companies =
            [
                new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
            ]
        };
        var quizRepo = new RecordingQuizRepository(
            activeQuizzes: [CreateQuiz(1, true)],
            publishedQuizzes: [CreateQuiz(1, true)]);
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, quizRepo);

        var result = await controller.Overview(
            new ReportingFilterDto(null, null, null, null, null, null, null, null, null, null),
            null,
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Single(reporting.OverviewCalls);
        var model = Assert.IsType<ReportingPageModel>(view.Model);

        Assert.NotNull(model.Overview);
        Assert.Equal(12, model.OverviewDisplayCount);
        Assert.Equal(0, reporting.OverviewCalls.Single().Filter.Companies?.Length ?? 0);
        Assert.Equal(12, model.Overview!.TotalLearners);
    }

    [Fact]
    public async Task ExportMonthly_ReturnsBadRequest_WhenDateRangeIsMissing()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService();
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, new RecordingQuizRepository([], []));

        var result = await controller.ExportMonthly(
            new ReportingFilterDto(null, null, null, null, null, null, null, null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("A valid date range is required.", badRequest.Value);
        Assert.Empty(reporting.MonthlyCalls);
    }

    [Fact]
    public async Task Export_NoFilters_ShowsEmptyPreview()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            ExportCount = 1,
            ExportRows =
            [
                new ReportingExportRowDto("Test User", "test@example.com", "123", "Aion", "Portfolio A", 1, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            ]
        };
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, new RecordingQuizRepository([], []));

        var result = await controller.Export(new ReportingFilterDto(null, null, null, null, null, null, null, null, null), null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportingPageModel>(view.Model);

        Assert.Equal("export", model.ActiveTab);
        Assert.Empty(model.ExportPreview ?? []);
        Assert.Equal(0, model.ExportPreviewTotalCount);
    }

    [Fact]
    public async Task ExportXlsx_UsesStatusColumn_ForDetailActivityReport()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            ExportRows =
            [
                new ReportingExportRowDto("Completed User", "completed@example.com", "111", "Aion", "Portfolio A", 10, true, new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero)),
                new ReportingExportRowDto("In Progress User", "progress@example.com", "222", "Aion", "Portfolio A", 4, false, null, new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero)),
                new ReportingExportRowDto("Not Started User", "new@example.com", "333", "Aion", "Portfolio A", 0, false, null, null)
            ]
        };
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, new RecordingQuizRepository([], []));

        var result = await controller.ExportXlsx(new ReportingFilterDto(null, null, null, null, null, null, null, null, null), CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        using var ms = new MemoryStream(file.FileContents);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);

        Assert.Equal("Status", ws.Cell(1, 6).GetString());
        Assert.Equal("Completed", ws.Cell(2, 6).GetString());
        Assert.Equal("In Progress", ws.Cell(3, 6).GetString());
        Assert.Equal("Not Started", ws.Cell(4, 6).GetString());
    }

    [Fact]
    public async Task ExportMonthly_ForwardsValidDateRangeToReportingService()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            MonthlyResult =
            [
                new MonthlyCompletionReportRowDto("Taylor Active", "111", "Portfolio A")
            ]
        };
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, new RecordingQuizRepository([], []));
        var from = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero);

        var result = await controller.ExportMonthly(
            new ReportingFilterDto(null, null, null, null, null, null, from, to),
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Equal("Volaris101 Monthly_2026-07 Report.xlsx", file.FileDownloadName);
        var call = Assert.Single(reporting.MonthlyCalls);
        Assert.Equal(from, call.FromDate);
        Assert.Equal(to, call.ToDate);
    }

    [Fact]
    public async Task Overview_PreservesSelectedOrganizations()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            OverviewResult = new ReportingOverviewDto(
                12,
                4,
                33,
                2,
                [],
                []),
            Companies =
            [
                new CompanyGroupDto("Portfolio A", [
                    new CompanyOptionDto(100, "Root"),
                    new CompanyOptionDto(101, "Child"),
                    new CompanyOptionDto(102, "Grandchild")
                ])
            ]
        };
        var accessRepo = new RecordingReportingAccessRepository
        {
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 100, ReportToId = 0, OrganizationName = "Root" },
                new VbuMasterListRecord { OrganizationId = 101, ReportToId = 100, OrganizationName = "Child" },
                new VbuMasterListRecord { OrganizationId = 102, ReportToId = 101, OrganizationName = "Grandchild" }
            ]
        };
        var controller = CreateController(new TestCurrentUserContext("Learner"), cache, reporting, new RecordingQuizRepository([], []), accessRepo);

        var result = await controller.Overview(
            new ReportingFilterDto(null, null, null, null, [100], null, null, null, null, null),
            null,
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportingPageModel>(view.Model);

        var filteredCompanies = model.Filter.Companies;
        var reportedCompanies = reporting.OverviewCalls.Single().Filter.Companies;
        Assert.NotNull(filteredCompanies);
        Assert.NotNull(reportedCompanies);
        Assert.Equal([100], filteredCompanies);
        Assert.Equal([100], reportedCompanies);
        Assert.False(model.ShowHowToButton);
    }

    [Fact]
    public async Task Overview_StripsExcludedOrganizations_AndBuildsChildExpansionMap()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            OverviewResult = new ReportingOverviewDto(
                12,
                4,
                33,
                2,
                [],
                []),
            Companies =
            [
                new CompanyGroupDto("Omegro", [
                    new CompanyOptionDto(30000, "Omegro", true),
                    new CompanyOptionDto(30001, "Andy Nelson Portfolio"),
                    new CompanyOptionDto(30002, "Roddy MacLennan Group"),
                    new CompanyOptionDto(30562, "tlmNexus")
                ])
            ]
        };
        var accessRepo = new RecordingReportingAccessRepository
        {
            VbuRows =
            [
                new VbuMasterListRecord { OrganizationId = 30000, ReportToId = 0, OrganizationName = "Omegro" },
                new VbuMasterListRecord { OrganizationId = 30001, ReportToId = 30000, OrganizationName = "Andy Nelson Portfolio" },
                new VbuMasterListRecord { OrganizationId = 30002, ReportToId = 30001, OrganizationName = "Roddy MacLennan Group" },
                new VbuMasterListRecord { OrganizationId = 30417, ReportToId = 30002, OrganizationName = "Hidden Parent" },
                new VbuMasterListRecord { OrganizationId = 30562, ReportToId = 30417, OrganizationName = "tlmNexus" }
            ]
        };
        var controller = CreateController(new TestCurrentUserContext("Learner"), cache, reporting, new RecordingQuizRepository([], []), accessRepo);

        var result = await controller.Overview(
            new ReportingFilterDto(
                SearchTerm: null,
                Domain: null,
                ModuleOrder: null,
                LastActiveDays: null,
                Companies: [30001, 30417],
                Portfolio: null,
                FromDate: null,
                ToDate: null),
            null,
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportingPageModel>(view.Model);
        var childrenMap = JsonSerializer.Deserialize<Dictionary<string, int[]>>(model.OrganizationChildrenJson!)!;
        var portfolioIds = JsonSerializer.Deserialize<int[]>(model.PortfolioOrganizationIdsJson!)!;
        var filteredCompanies = model.Filter.Companies;
        var reportedCompanies = reporting.OverviewCalls.Single().Filter.Companies;

        Assert.NotNull(filteredCompanies);
        Assert.NotNull(reportedCompanies);
        Assert.Equal([30000], portfolioIds);
        Assert.Equal([30001], filteredCompanies);
        Assert.Equal([30001], reportedCompanies);
        Assert.Equal([30001], childrenMap["30000"]);
        Assert.Equal([30002], childrenMap["30001"]);
        Assert.Equal([30562], childrenMap["30002"]);
        Assert.DoesNotContain("30417", childrenMap.Keys);
    }

    [Fact]
    public async Task HowTo_ReturnsPdfFromWebRoot()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var env = CreateWebHostEnvironment();
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, new RecordingReportingService(), new RecordingQuizRepository([], []), environment: env);

        var result = controller.HowTo();

        var file = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.EndsWith(Path.Combine("docs", "reporting-how-to.pdf"), file.FileName);
    }

    [Fact]
    public async Task Users_WithStateToken_UsesCachedFilterAndActiveQuizzesForGlobalAdmin()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            UserActivityCount = 9,
            UserActivity = []
        };
        var quizRepo = new RecordingQuizRepository(
            activeQuizzes: [CreateQuiz(1, true), CreateQuiz(2, false)],
            publishedQuizzes: [CreateQuiz(1, true)]);
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, quizRepo);
        var saved = controller.SaveState(new ReportingFilterDto("south", null, null, null, null, null, null, null));
        var token = JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(((JsonResult)saved).Value))!["token"];

        var result = await controller.Users(
            new ReportingFilterDto(null, null, null, null, null, null, null, null),
            token,
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportingPageModel>(view.Model);

        Assert.Equal("users", model.ActiveTab);
        Assert.Equal(9, model.UserActivityTotalCount);
        Assert.Equal("south", reporting.UserActivityCountCalls.Single().SearchTerm);
        Assert.Equal("south", reporting.UserActivityCalls.Single().Filter.SearchTerm);
        Assert.Equal(100, reporting.UserActivityCalls.Single().Take);
        Assert.True(quizRepo.GetAllActiveCalled);
        Assert.False(quizRepo.GetAllPublishedCalled);
    }

    [Fact]
    public async Task NotStarted_RedirectsToOverview()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService();
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, new RecordingQuizRepository([], []));

        var result = controller.NotStarted(
            new ReportingFilterDto("tay", null, null, null, [30505], null, null, null, null, null),
            null);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/reporting", redirect.Url);
    }

    [Fact]
    public async Task NotStarted_WithJsonAccept_RedirectsToOverview()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService();
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, new RecordingQuizRepository([], []));
        controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

        var result = controller.NotStarted(
            new ReportingFilterDto(null, null, null, null, [30505], null, null, null, null, null),
            null);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/reporting", redirect.Url);
    }

    [Fact]
    public async Task ExportNotStarted_ReturnsBadRequest_WhenCompanySelectionIsRequired()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            NotStartedUsersResult = new NotStartedUsersResultDto(true, [])
        };
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, new RecordingQuizRepository([], []));

        var result = await controller.ExportNotStarted(
            new ReportingFilterDto(null, null, null, null, null, null, null, null, null, null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Select at least one organization to export not started users.", badRequest.Value);
    }

    [Fact]
    public async Task ExportNotStarted_ReturnsWorkbook_WithExpectedHeadersAndRows()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var reporting = new RecordingReportingService
        {
            NotStartedUsersResult = new NotStartedUsersResultDto(
                false,
                [
                    new NotStartedUserRowDto("Taylor Active", "taylor@example.com", string.Empty, string.Empty, "Aion", "Portfolio A"),
                    new NotStartedUserRowDto("Jordan Ready", "jordan@example.com", string.Empty, string.Empty, "Aion", "Portfolio A")
                ])
        };
        var controller = CreateController(new TestCurrentUserContext("GlobalAdmin"), cache, reporting, new RecordingQuizRepository([], []));

        var result = await controller.ExportNotStarted(
            new ReportingFilterDto(null, null, null, null, [30505], null, null, null, null, null),
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Contains("Volaris101_Not_Started_", file.FileDownloadName);

        using var workbook = new XLWorkbook(new MemoryStream(file.FileContents));
        var worksheet = workbook.Worksheet("Not Started Users");
        Assert.Equal("Employee Name", worksheet.Cell(1, 1).GetString());
        Assert.Equal("Email", worksheet.Cell(1, 2).GetString());
        Assert.Equal("Taylor Active", worksheet.Cell(2, 1).GetString());
        Assert.Equal("taylor@example.com", worksheet.Cell(2, 2).GetString());
        Assert.Equal("Jordan Ready", worksheet.Cell(3, 1).GetString());
        Assert.Equal("Portfolio A", worksheet.Cell(3, 6).GetString());
    }

    private static ReportingController CreateController(
        TestCurrentUserContext currentUser,
        IMemoryCache cache,
        RecordingReportingService reporting,
        RecordingQuizRepository quizRepo,
        RecordingReportingAccessRepository? accessRepo = null,
        IWebHostEnvironment? environment = null,
        IPeopleDirectoryService? peopleDirectory = null)
    {
        var controller = new ReportingController(
            reporting,
            peopleDirectory ?? new NoOpPeopleDirectoryService(),
            accessRepo ?? new RecordingReportingAccessRepository(),
            quizRepo,
            currentUser,
            cache,
            environment ?? CreateWebHostEnvironment())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static IWebHostEnvironment CreateWebHostEnvironment()
    {
        var repoRoot = FindRepoRoot();
        var webRoot = Path.Combine(repoRoot, "src", "Volaris101.Web", "wwwroot");
        return new TestWebHostEnvironment(repoRoot, webRoot);
    }

    private static string FindRepoRoot()
    {
        const string fallbackRepoRoot = @"D:\PhilDev\ReactProjects\SP-VolarisQuizWebApp";
        if (Directory.Exists(Path.Combine(fallbackRepoRoot, "src", "Volaris101.Web", "wwwroot")))
            return fallbackRepoRoot;

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Volaris101.Web", "wwwroot");
            if (Directory.Exists(candidate))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root for reporting controller tests.");
    }

    private static Quiz CreateQuiz(int displayOrder, bool isPublished) =>
        new()
        {
            QuizId = Guid.NewGuid(),
            DisplayOrder = displayOrder,
            IsActive = true,
            IsPublished = isPublished,
            PassingScore = 50m,
            Translations =
            [
                new QuizTranslation
                {
                    QuizId = Guid.NewGuid(),
                    LanguageCode = "en",
                    Title = $"Module {displayOrder}"
                }
            ]
        };

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(string roleCode)
        {
            RoleCode = roleCode;
        }

        public Guid UserId { get; } = Guid.NewGuid();
        public string Login { get; } = "jasmine@example.com";
        public string Email { get; } = "jasmine@example.com";
        public string FullName { get; } = "Jasmine Test";
        public string RoleCode { get; }
        public string PreferredLanguage { get; } = "en";
        public bool IsAuthenticated { get; } = true;
        public bool IsImpersonating => false;
        public Guid ImpersonatorUserId => Guid.Empty;
        public string ImpersonatorLogin => string.Empty;
        public string ImpersonatorEmail => string.Empty;
        public string ImpersonatorFullName => string.Empty;
        public string ImpersonatorRoleCode => string.Empty;
        public string ImpersonatorPreferredLanguage => string.Empty;
        public bool IsLearner => RoleCode == "Learner";
        public bool IsOrgAdmin => RoleCode is "OrgAdmin" or "PortfolioAdmin" or "GlobalAdmin";
        public bool IsPortfolioAdmin => RoleCode is "PortfolioAdmin" or "GlobalAdmin";
        public bool IsGlobalAdmin => RoleCode == "GlobalAdmin";
    }

    private sealed class NoOpPeopleDirectoryService : IPeopleDirectoryService
    {
        public IReadOnlyList<PeopleDirectoryEmployeeDto> Employees { get; init; } = [];

        public Task<IReadOnlyList<PeopleDirectoryEmployeeDto>> GetEmployeesByCompaniesAsync(IReadOnlyList<string> companyNames, CancellationToken ct = default) =>
            Task.FromResult(Employees);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath, string webRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = webRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootFileProvider = new PhysicalFileProvider(webRootPath);
            ApplicationName = "Volaris101.Web";
            EnvironmentName = "Development";
        }

        public string ApplicationName { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; } = string.Empty;
    }

    private sealed class RecordingReportingAccessRepository : IReportingAccessRepository
    {
        public IReadOnlyList<VbuMasterListRecord> VbuRows { get; set; } = [];

        public Task<IReadOnlyList<OrgAdminAssignment>> GetOrgAssignmentsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PortfolioAdminAssignment>> GetPortfolioAssignmentsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetOrgAssignmentsAsync(Guid userId, IReadOnlyList<int> organizationIds, Guid actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetPortfolioAssignmentsAsync(Guid userId, IReadOnlyList<int> portfolioIds, Guid actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> SearchOrganizationsAsync(string? searchTerm, int take = 25, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> SearchPortfoliosAsync(string? searchTerm, int take = 25, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Portfolio?> GetPortfolioByNameAsync(string portfolioName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> GetOrganizationsByIdsAsync(IReadOnlyCollection<int> organizationIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> GetPortfoliosByIdsAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> GetOrganizationIdsForPortfoliosAsync(IReadOnlyCollection<int> portfolioIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VbuMasterListRecord>> GetVbuMasterListAsync(CancellationToken ct = default) => Task.FromResult(VbuRows);
    }

    private sealed class RecordingQuizRepository : IQuizRepository
    {
        private readonly IReadOnlyList<Quiz> _activeQuizzes;
        private readonly IReadOnlyList<Quiz> _publishedQuizzes;

        public RecordingQuizRepository(IReadOnlyList<Quiz> activeQuizzes, IReadOnlyList<Quiz> publishedQuizzes)
        {
            _activeQuizzes = activeQuizzes;
            _publishedQuizzes = publishedQuizzes;
        }

        public bool GetAllActiveCalled { get; private set; }
        public bool GetAllPublishedCalled { get; private set; }

        public Task<IReadOnlyList<Quiz>> GetAllActiveAsync(string lang = "en", CancellationToken ct = default)
        {
            GetAllActiveCalled = true;
            return Task.FromResult(_activeQuizzes);
        }

        public Task<IReadOnlyList<Quiz>> GetAllPublishedAsync(string lang = "en", CancellationToken ct = default)
        {
            GetAllPublishedCalled = true;
            return Task.FromResult(_publishedQuizzes);
        }

        public Task<Quiz?> GetByIdAsync(Guid quizId, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Quiz?> GetByIdForLearnerAsync(Guid quizId, bool includeDrafts, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Quiz> CreateAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Quiz> UpdateAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PublishAsync(Quiz quiz, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Question?> GetQuestionByIdAsync(Guid questionId, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Question> CreateQuestionAsync(Question question, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Question> UpdateQuestionAsync(Question question, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteQuestionAsync(Guid questionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Question>> SearchQuestionsAsync(string? searchTerm, Guid? quizId, string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RecordingReportingService : IReportingService
    {
        public ReportingOverviewDto OverviewResult { get; set; } = new(0, 0, 0, 0, [], []);
        public int UserActivityCount { get; set; }
        public IReadOnlyList<UserActivityRowDto> UserActivity { get; set; } = [];
        public int ExportCount { get; set; }
        public IReadOnlyList<ReportingExportRowDto> ExportRows { get; set; } = [];
        public IReadOnlyList<string> Domains { get; set; } = [];
        public IReadOnlyList<CompanyGroupDto> Companies { get; set; } = [];
        public ReportingDashboardDto Dashboard { get; set; } = new(null, []);
        public NotStartedUsersResultDto NotStartedUsersResult { get; set; } = new(false, []);
        public IReadOnlyList<MonthlyCompletionReportRowDto> MonthlyResult { get; set; } = [];

        public List<(ReportingFilterDto Filter, bool IncludeDrafts, int? Take)> OverviewCalls { get; } = [];
        public List<(ReportingFilterDto Filter, bool IncludeDrafts, int? Take)> UserActivityCalls { get; } = [];
        public List<ReportingFilterDto> UserActivityCountCalls { get; } = [];
        public List<ReportingFilterDto> NotStartedUsersCalls { get; } = [];
        public List<ReportingFilterDto> MonthlyCalls { get; } = [];

        public Task<ReportingOverviewDto> GetOverviewAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default)
        {
            OverviewCalls.Add((filter, includeDrafts, take));
            return Task.FromResult(OverviewResult);
        }

        public Task<ReportingDashboardDto> GetLanguageDashboardAsync(CancellationToken ct = default) => Task.FromResult(Dashboard);

        public Task<int> GetUserActivityCountAsync(ReportingFilterDto filter, bool includeDrafts = false, CancellationToken ct = default)
        {
            UserActivityCountCalls.Add(filter);
            return Task.FromResult(UserActivityCount);
        }

        public Task<IReadOnlyList<UserActivityRowDto>> GetUserActivityAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default)
        {
            UserActivityCalls.Add((filter, includeDrafts, take));
            return Task.FromResult(UserActivity);
        }

        public Task<int> GetExportDataCountAsync(ReportingFilterDto filter, bool includeDrafts = false, CancellationToken ct = default) => Task.FromResult(ExportCount);
        public Task<IReadOnlyList<ReportingExportRowDto>> GetExportDataAsync(ReportingFilterDto filter, bool includeDrafts = false, int? take = null, CancellationToken ct = default) => Task.FromResult(ExportRows);
        public Task<IReadOnlyList<MonthlyCompletionReportRowDto>> GetMonthlyCompletionReportAsync(ReportingFilterDto filter, CancellationToken ct = default)
        {
            MonthlyCalls.Add(filter);
            return Task.FromResult(MonthlyResult);
        }
        public Task<PortfolioReportDto> GetPortfolioReportAsync(ReportingFilterDto filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PortfolioReportDiagnosticDto> GetPortfolioReportDiagnosticAsync(ReportingFilterDto filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NotStartedUsersResultDto> GetNotStartedUsersAsync(ReportingFilterDto filter, CancellationToken ct = default)
        {
            NotStartedUsersCalls.Add(filter);
            return Task.FromResult(NotStartedUsersResult);
        }
        public Task<IReadOnlyList<string>> GetDistinctDomainsAsync(CancellationToken ct = default) => Task.FromResult(Domains);
        public Task<IReadOnlyList<CompanyGroupDto>> GetAvailableCompaniesAsync(CancellationToken ct = default) => Task.FromResult(Companies);
    }
}
