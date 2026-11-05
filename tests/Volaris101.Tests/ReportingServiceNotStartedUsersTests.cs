using Microsoft.Extensions.Caching.Memory;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Application.Services;
using Volaris101.Domain.Entities;

namespace Volaris101.Tests;

public class ReportingServiceNotStartedUsersTests
{
    [Theory]
    [InlineData("ReportingAdmin")]
    [InlineData("OrgAdmin")]
    public async Task ScopedAdmins_CanSeeAllUsersAlongsideScopedLearners(string roleCode)
    {
        var reportingAdminId = Guid.NewGuid();
        var reportingAdmin = CreateUser(reportingAdminId, "admin@example.com", "999", "Aion", "Portfolio A", 30505);
        reportingAdmin.RoleCode = roleCode;
        var learner = CreateUser(Guid.NewGuid(), "learner@example.com", "111", "Aion", "Portfolio A", 30505);
        var orgAdmin = CreateUser(Guid.NewGuid(), "org-admin@example.com", "222", "Aion", "Portfolio A", 30505);
        orgAdmin.RoleCode = "OrgAdmin";
        var currentUser = new TestCurrentUserContext("ReportingAdmin", reportingAdminId);
        var service = CreateService(
            currentUser: currentUser,
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers = [reportingAdmin, learner, orgAdmin]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Reporting Admin", "admin@example.com", "Aion", "Aion", 999, 30505),
                    new PeopleDirectoryEmployeeDto("Learner", "learner@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Org Admin", "org-admin@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            scopeService: new FakeReportingScopeService([30505]),
            quizRepo: new FakeQuizRepository([]));

        var rows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null));

        Assert.Contains(rows, row => row.UserId == reportingAdminId);
        Assert.Contains(rows, row => row.UserId == learner.UserId);
        Assert.Contains(rows, row => row.UserId == orgAdmin.UserId);
    }

    [Fact]
    public async Task GetUserActivityAsync_ExcludesLeaversByDefault_AndIncludesThemWhenRequested()
    {
        var noIdUserId = Guid.NewGuid();
        var idMissingUserId = Guid.NewGuid();
        var emailDifferentUserId = Guid.NewGuid();
        var wrongOrganizationUserId = Guid.NewGuid();
        var validUserId = Guid.NewGuid();
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(noIdUserId, "no-id@example.com", "", "Aion", "Portfolio A", 30505),
                    CreateUser(idMissingUserId, "id-missing@example.com", "456", "Aion", "Portfolio A", 30505),
                    CreateUser(emailDifferentUserId, "email-different@example.com", "789", "Aion", "Portfolio A", 30505),
                    CreateUser(wrongOrganizationUserId, "wrong-org@example.com", "456", "Aion", "Portfolio A", 30505),
                    CreateUser(validUserId, "valid@example.com", "123", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("ID Missing User", "id-missing@example.com", "Aion", "Aion", 999, 30505),
                    new PeopleDirectoryEmployeeDto("Email Different User", "other@example.com", "Aion", "Aion", 789, 30505),
                    new PeopleDirectoryEmployeeDto("Wrong Organization User", "wrong-org@example.com", "Other", "Other", 456, 30506),
                    new PeopleDirectoryEmployeeDto("Valid User", "valid@example.com", "Aion", "Aion", 123, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([]));

        var defaultRows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, IncludeLeavers: false));
        var includedRows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, IncludeLeavers: true));

        Assert.DoesNotContain(defaultRows, row => row.UserId == noIdUserId);
        Assert.DoesNotContain(defaultRows, row => row.UserId == idMissingUserId);
        Assert.DoesNotContain(defaultRows, row => row.UserId == wrongOrganizationUserId);
        Assert.Contains(defaultRows, row => row.UserId == emailDifferentUserId);
        Assert.Contains(defaultRows, row => row.UserId == validUserId);
        Assert.Contains(includedRows, row => row.UserId == noIdUserId);
        Assert.Contains(includedRows, row => row.UserId == idMissingUserId);
        Assert.Contains(includedRows, row => row.UserId == emailDifferentUserId);
        Assert.Contains(includedRows, row => row.UserId == wrongOrganizationUserId);
        Assert.Contains(includedRows, row => row.UserId == validUserId);
    }

    [Fact]
    public async Task Reporting_ExcludesDisallowedEmployees_FromApplicationAndDirectoryOnlyRows()
    {
        var appDisallowedId = Guid.NewGuid();
        var appAllowedId = Guid.NewGuid();
        var peopleDirectory = new FakePeopleDirectoryService
        {
            Employees =
            [
                new PeopleDirectoryEmployeeDto("Disallowed App User", "app-disallowed@example.com", "Aion", "Aion", 111, 30505, " Contractor - ourVolaris Disallowed "),
                new PeopleDirectoryEmployeeDto("Disallowed Directory User", "directory-disallowed@example.com", "Aion", "Aion", 222, 30505, "Contractor - OURVOLARIS DISALLOWED"),
                new PeopleDirectoryEmployeeDto("Allowed App User", "app-allowed@example.com", "Aion", "Aion", 333, 30505, "Employee"),
                new PeopleDirectoryEmployeeDto("Allowed Directory User", "directory-allowed@example.com", "Aion", "Aion", 444, 30505, "Employee")
            ]
        };
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(appDisallowedId, "app-disallowed@example.com", "111", "Aion", "Portfolio A", 30505),
                    CreateUser(appAllowedId, "app-allowed@example.com", "333", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: peopleDirectory,
            quizRepo: new FakeQuizRepository([]));

        var filter = new ReportingFilterDto(null, null, null, null, [30505], null, null, null, null, null);
        var overview = await service.GetOverviewAsync(filter);
        var exportRows = await service.GetExportDataAsync(filter);

        Assert.Equal(2, overview.TotalLearners);
        Assert.DoesNotContain(exportRows, row => row.EmployeeId is "111" or "222");
        Assert.Contains(exportRows, row => row.EmployeeId == "333");
        Assert.Contains(exportRows, row => row.EmployeeId == "444");
    }

    [Fact]
    public async Task Reporting_ExcludesDisallowedEmployees_FromUserActivity()
    {
        var disallowedUserId = Guid.NewGuid();
        var allowedUserId = Guid.NewGuid();
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(disallowedUserId, "disallowed@example.com", "111", "Aion", "Portfolio A", 30505),
                    CreateUser(allowedUserId, "allowed@example.com", "333", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Disallowed", "disallowed@example.com", "Aion", "Aion", 111, 30505, "Contractor - ourVolaris Disallowed"),
                    new PeopleDirectoryEmployeeDto("Allowed", "allowed@example.com", "Aion", "Aion", 333, 30505, "Employee")
                ]
            },
            quizRepo: new FakeQuizRepository([]));

        var rows = await service.GetUserActivityAsync(
            new ReportingFilterDto(null, null, null, null, [30505], null, null, null, null, null));

        var row = Assert.Single(rows);
        Assert.Equal("allowed@example.com", row.Email);
    }

    [Fact]
    public async Task GetNotStartedUsersAsync_ReturnsSelectionPrompt_ForGlobalAdminWithoutCompanies()
    {
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ]
            });

        var result = await service.GetNotStartedUsersAsync(new ReportingFilterDto(
            null, null, null, null, null, null, null, null, null, null));

        Assert.True(result.RequiresCompanySelection);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task GetNotStartedUsersAsync_IncludesActiveDirectoryEmployee_WhenNoStartedRecordExists()
    {
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Taylor Active", "taylor@example.com", "Aion", "Aion", 101, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([CreatePublishedQuiz(1)]));

        var result = await service.GetNotStartedUsersAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null, null));

        Assert.False(result.RequiresCompanySelection);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Taylor Active", row.EmployeeName);
        Assert.Equal("taylor@example.com", row.Email);
        Assert.Equal("Aion", row.Company);
        Assert.Equal("Portfolio A", row.Portfolio);
    }

    [Fact]
    public async Task GetNotStartedUsersAsync_DoesNotDropDifferentOrganizationMatch_WhenEmailMatches()
    {
        var startedUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A",
                    [
                        new CompanyOptionDto(30505, "Aion"),
                        new CompanyOptionDto(30506, "Aion Sub")
                    ])
                ],
                FilteredUsers =
                [
                    CreateUser(startedUserId, "same@example.com", "123", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Same Employee", "same@example.com", "Aion", "Aion", 123, 30505),
                    new PeopleDirectoryEmployeeDto("Same Employee", "same@example.com", "Aion Sub", "Aion Sub", 456, 30506)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(startedUserId, publishedQuiz.QuizId)
                ]
            });

        var result = await service.GetNotStartedUsersAsync(new ReportingFilterDto(
            null, null, null, null, [30505, 30506], null, null, null, null, null));

        Assert.False(result.RequiresCompanySelection);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Aion Sub", row.Company);
        Assert.Equal("same@example.com", row.Email);
    }

    [Fact]
    public async Task GetNotStartedUsersAsync_DefaultsToScopedCompanies_ForOrgAdminWithoutSelection()
    {
        var userRepo = new FakeUserRepository
        {
            AvailableCompanies =
            [
                new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
            ]
        };
        var service = CreateService(
            currentUser: new TestCurrentUserContext("OrgAdmin"),
            userRepo: userRepo,
            scopeService: new FakeReportingScopeService([30505]),
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Scoped Employee", "scoped@example.com", "Aion", "Aion", 202, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([CreatePublishedQuiz(1)]));

        var result = await service.GetNotStartedUsersAsync(new ReportingFilterDto(
            null, null, null, null, null, null, null, null, null, null));

        Assert.False(result.RequiresCompanySelection);
        Assert.Single(result.Rows);
        Assert.Equal(new[] { 30505 }, userRepo.LastAvailableCompaniesScope!.OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task GetOverviewAsync_CountsPeopleDirectoryOnlyUsers_AsNotStarted()
    {
        var appUserId = Guid.NewGuid();
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(appUserId, "active@example.com", "111", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Active User", "active@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([CreatePublishedQuiz(1)]));

        var result = await service.GetOverviewAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null, null));

        Assert.Equal(2, result.TotalLearners);
        Assert.Equal(2, result.NotStarted);
        Assert.Equal(0, result.FullyCompleted);
        Assert.Equal(0, result.AvgCompletionPercent);
        Assert.Equal(0, result.InProgress);
        Assert.Equal(0, result.CompletedPercent);
        Assert.Equal(0, result.InProgressPercent);
        Assert.Equal(100, result.NotStartedPercent);
        var bar = Assert.Single(result.CompletionBars);
        Assert.Equal(0, bar.PassedCount);
        Assert.Equal(2, bar.TotalLearners);
    }

    [Fact]
    public async Task GetOverviewAsync_WithDomainFilter_KeepsPeopleDirectoryOnlyUsers_AsNotStarted()
    {
        var appUserId = Guid.NewGuid();
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(appUserId, "active@example.com", "111", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Active User", "active@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([CreatePublishedQuiz(1)]));

        var result = await service.GetOverviewAsync(new ReportingFilterDto(
            null, "example.com", null, null, [30505], null, null, null, null, null));

        Assert.Equal(2, result.TotalLearners);
        Assert.Equal(2, result.NotStarted);
        Assert.Equal(0, result.FullyCompleted);
        Assert.Equal(0, result.InProgress);
        Assert.Equal(0, result.CompletedPercent);
        Assert.Equal(0, result.InProgressPercent);
        Assert.Equal(100, result.NotStartedPercent);
    }

    [Fact]
    public async Task GetOverviewAsync_FiltersBySelectedStatuses()
    {
        var completedUserId = Guid.NewGuid();
        var inProgressUserId = Guid.NewGuid();
        var notStartedUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(completedUserId, "completed@example.com", "111", "Aion", "Portfolio A", 30505),
                    CreateUser(inProgressUserId, "progress@example.com", "222", "Aion", "Portfolio A", 30505),
                    CreateUser(notStartedUserId, "nostart@example.com", "333", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Completed User", "completed@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Progress User", "progress@example.com", "Aion", "Aion", 222, 30505),
                    new PeopleDirectoryEmployeeDto("Not Started User", "nostart@example.com", "Aion", "Aion", 333, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 444, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(completedUserId, publishedQuiz.QuizId),
                    CreateInProgressAttempt(inProgressUserId, publishedQuiz.QuizId)
                ]
            });

        var result = await service.GetOverviewAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null, null, ["Completed", "In Progress"]));

        Assert.Equal(2, result.TotalLearners);
        Assert.Equal(1, result.FullyCompleted);
        Assert.Equal(1, result.InProgress);
        Assert.Equal(0, result.NotStarted);
        Assert.Equal(50, result.CompletedPercent);
        Assert.Equal(50, result.InProgressPercent);
        Assert.Equal(0, result.NotStartedPercent);
        var bar = Assert.Single(result.CompletionBars);
        Assert.Equal(1, bar.PassedCount);
        Assert.Equal(2, bar.TotalLearners);
    }

    [Fact]
    public async Task GetOverviewAsync_WithModuleFilter_ZeroesNotStarted()
    {
        var appUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(appUserId, "active@example.com", "111", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Active User", "active@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]));

        var result = await service.GetOverviewAsync(new ReportingFilterDto(
            null, null, 1, null, [30505], null, null, null, null, null));

        Assert.Equal(0, result.TotalLearners);
        Assert.Equal(0, result.NotStarted);
        Assert.Equal(0, result.NotStartedPercent);
    }

    [Fact]
    public async Task GetOverviewAsync_WithDateRange_ZeroesNotStarted()
    {
        var appUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(appUserId, "active@example.com", "111", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Active User", "active@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]));

        var result = await service.GetOverviewAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero),
            null,
            null));

        Assert.Equal(0, result.TotalLearners);
        Assert.Equal(0, result.NotStarted);
        Assert.Equal(0, result.NotStartedPercent);
    }

    [Fact]
    public async Task GetUserActivityAsync_IncludesPeopleDirectoryOnlyUsers_AsNotStarted()
    {
        var activeUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(activeUserId, "active@example.com", "111", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Active User", "active@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(activeUserId, publishedQuiz.QuizId)
                ]
            });

        var filter = new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null, null);

        var rows = await service.GetUserActivityAsync(filter);
        var count = await service.GetUserActivityCountAsync(filter);

        Assert.Equal(2, count);
        var directoryRow = Assert.Single(rows, r => r.Email == "directory-only@example.com");
        Assert.Equal("Not Started", directoryRow.ActivityStatus);
        Assert.Equal(0, directoryRow.ModulesPassed);
        Assert.False(directoryRow.IsFullyCompleted);
        Assert.Null(directoryRow.LastActiveAt);
        Assert.Equal("Aion", directoryRow.Company);
        Assert.Equal("Portfolio A", directoryRow.Portfolio);

        var activeRow = Assert.Single(rows, r => r.Email == "active@example.com");
        Assert.Equal("Completed", activeRow.ActivityStatus);
        Assert.Equal(1, activeRow.ModulesPassed);
    }

    [Fact]
    public async Task GetUserActivityAsync_FiltersBySelectedStatuses()
    {
        var completedUserId = Guid.NewGuid();
        var inProgressUserId = Guid.NewGuid();
        var notStartedUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(completedUserId, "completed@example.com", "111", "Aion", "Portfolio A", 30505),
                    CreateUser(inProgressUserId, "progress@example.com", "222", "Aion", "Portfolio A", 30505),
                    CreateUser(notStartedUserId, "nostart@example.com", "333", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Completed User", "completed@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Progress User", "progress@example.com", "Aion", "Aion", 222, 30505),
                    new PeopleDirectoryEmployeeDto("Not Started User", "nostart@example.com", "Aion", "Aion", 333, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 444, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(completedUserId, publishedQuiz.QuizId),
                    CreateInProgressAttempt(inProgressUserId, publishedQuiz.QuizId)
                ]
            });

        var rows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null, null, ["In Progress", "Not Started"]));

        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, row => row.ActivityStatus == "In Progress");
        Assert.Contains(rows, row => row.ActivityStatus == "Not Started");
        Assert.Contains(rows, row => row.ActivityStatus == "In Progress" && row.Email == "progress@example.com");
        Assert.Contains(rows, row => row.ActivityStatus == "Not Started" && row.Email == "nostart@example.com");
        Assert.Contains(rows, row => row.ActivityStatus == "Not Started" && row.Email == "directory-only@example.com");
    }

    [Fact]
    public async Task GetUserActivityAsync_FiltersDirectoryOnlyRows_ByDomain()
    {
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Taylor Match", "taylor@example.com", "Aion", "Aion", 101, 30505),
                    new PeopleDirectoryEmployeeDto("Taylor Other", "taylor@other.com", "Aion", "Aion", 102, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([]));

        var rows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, "example.com", null, null, [30505], null, null, null, null, null));

        Assert.Single(rows);
        Assert.Equal("taylor@example.com", rows[0].Email);
        Assert.Equal("Not Started", rows[0].ActivityStatus);
    }

    [Fact]
    public async Task GetUserActivityAsync_FiltersDirectoryOnlyRows_ByPortfolio()
    {
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")]),
                    new CompanyGroupDto("Portfolio B", [new CompanyOptionDto(40505, "Beta")])
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("A Person", "a@example.com", "Aion", "Aion", 101, 30505),
                    new PeopleDirectoryEmployeeDto("B Person", "b@example.com", "Beta", "Beta", 102, 40505)
                ]
            },
            quizRepo: new FakeQuizRepository([]));

        var rows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, null, null, null, "Portfolio A", null, null, null, null));

        Assert.Single(rows);
        Assert.Equal("A Person", rows[0].FullName);
        Assert.Equal("Portfolio A", rows[0].Portfolio);
    }

    [Fact]
    public async Task GetUserActivityAsync_WithModuleFilter_ExcludesNotStartedRows()
    {
        var completedUserId = Guid.NewGuid();
        var notStartedUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(completedUserId, "completed@example.com", "111", "Aion", "Portfolio A", 30505),
                    CreateUser(notStartedUserId, "nostart@example.com", "222", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Completed User", "completed@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Not Started User", "nostart@example.com", "Aion", "Aion", 222, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 333, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(completedUserId, publishedQuiz.QuizId)
                ]
            });

        var rows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, 1, null, [30505], null, null, null, null, null));

        Assert.Single(rows);
        Assert.Equal("Completed", rows[0].ActivityStatus);
        Assert.DoesNotContain(rows, row => row.ActivityStatus == "Not Started");
    }

    [Fact]
    public async Task GetUserActivityAsync_WithDateRange_ExcludesNotStartedRows()
    {
        var completedUserId = Guid.NewGuid();
        var notStartedUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(completedUserId, "completed@example.com", "111", "Aion", "Portfolio A", 30505),
                    CreateUser(notStartedUserId, "nostart@example.com", "222", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Completed User", "completed@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Not Started User", "nostart@example.com", "Aion", "Aion", 222, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 333, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(completedUserId, publishedQuiz.QuizId)
                ]
            });

        var rows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero),
            null,
            null));

        Assert.Single(rows);
        Assert.Equal("Completed", rows[0].ActivityStatus);
        Assert.DoesNotContain(rows, row => row.ActivityStatus == "Not Started");
    }

    [Fact]
    public async Task GetUserActivityAsync_WithLastActiveFilter_ExcludesNotStartedRows()
    {
        var completedUserId = Guid.NewGuid();
        var notStartedUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(completedUserId, "completed@example.com", "111", "Aion", "Portfolio A", 30505),
                    CreateUser(notStartedUserId, "nostart@example.com", "222", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Completed User", "completed@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Not Started User", "nostart@example.com", "Aion", "Aion", 222, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 333, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(completedUserId, publishedQuiz.QuizId)
                ]
            });

        var rows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, null, 30, [30505], null, null, null, null, null));

        Assert.Single(rows);
        Assert.Equal("Completed", rows[0].ActivityStatus);
        Assert.DoesNotContain(rows, row => row.ActivityStatus == "Not Started");
    }

    [Fact]
    public async Task GetUserActivityAsync_SortsRowsByNameAscending()
    {
        var completedUserId = Guid.NewGuid();
        var inProgressUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var alphaUser = CreateUser(completedUserId, "alpha@example.com", "111", "Aion", "Portfolio A", 30505);
        alphaUser.FirstName = "Alpha";
        alphaUser.LastName = "Person";
        var zuluUser = CreateUser(inProgressUserId, "zeta@example.com", "222", "Aion", "Portfolio A", 30505);
        zuluUser.FirstName = "Zulu";
        zuluUser.LastName = "Person";

        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    zuluUser,
                    alphaUser
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Zulu Person", "zeta@example.com", "Aion", "Aion", 222, 30505),
                    new PeopleDirectoryEmployeeDto("Alpha Person", "alpha@example.com", "Aion", "Aion", 111, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(completedUserId, publishedQuiz.QuizId),
                    CreateInProgressAttempt(inProgressUserId, publishedQuiz.QuizId)
                ]
            });

        var rows = await service.GetUserActivityAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null, null));

        Assert.Equal(["Alpha Person", "Zulu Person"], rows.Select(r => r.FullName).ToArray());
    }

    [Fact]
    public async Task GetExportDataAsync_PopulatesEmployeeId_ForDirectoryOnlyLearners()
    {
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([]));

        var rows = await service.GetExportDataAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null, null));

        var row = Assert.Single(rows);
        Assert.Equal("222", row.EmployeeId);
        Assert.Equal("Not Started", row.IsFullyCompleted ? "Completed" : row.LastActiveAt.HasValue ? "In Progress" : "Not Started");
    }

    [Fact]
    public async Task GetExportDataAsync_WithLastActiveFilter_KeepsDirectoryOnlyLearners()
    {
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(Guid.NewGuid(), "active@example.com", "111", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Active User", "active@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([CreatePublishedQuiz(1)]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(Guid.NewGuid(), Guid.NewGuid())
                ]
            });

        var rows = await service.GetExportDataAsync(new ReportingFilterDto(
            null, null, null, 30, [30505], null, null, null, null, null));

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.EmployeeId == "222" && row.IsFullyCompleted == false);
    }

    [Fact]
    public async Task GetExportDataAsync_WithModuleFilter_ExcludesDirectoryOnlyLearners()
    {
        var completedUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")]),
                ],
                FilteredUsers =
                [
                    CreateUser(completedUserId, "completed@example.com", "111", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Completed User", "completed@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Directory Only", "directory-only@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(completedUserId, publishedQuiz.QuizId)
                ]
            });

        var rows = await service.GetExportDataAsync(new ReportingFilterDto(
            null, null, 1, null, [30505], null, null, null, null, null));

        Assert.Single(rows);
        Assert.Equal("111", rows[0].EmployeeId);
        Assert.Equal("Completed", rows[0].IsFullyCompleted ? "Completed" : "Not Started");
    }

    [Fact]
    public async Task GetExportDataAsync_WithLearnerSearch_IncludesDirectoryOnlyLearners()
    {
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Taylor Active", "taylor@example.com", "Aion", "Aion", 222, 30505),
                    new PeopleDirectoryEmployeeDto("Jordan Other", "jordan@example.com", "Aion", "Aion", 333, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([]));

        var rows = await service.GetExportDataAsync(new ReportingFilterDto(
            "taylor", null, null, null, [30505], null, null, null, null, null));

        var row = Assert.Single(rows);
        Assert.Equal("Taylor Active", row.FullName);
        Assert.Equal("222", row.EmployeeId);
        Assert.Equal("Not Started", row.IsFullyCompleted ? "Completed" : row.LastActiveAt.HasValue ? "In Progress" : "Not Started");
    }

    [Fact]
    public async Task GetExportDataAsync_WithStatusFilter_IncludesDirectoryOnlyLearners()
    {
        var completedUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(completedUserId, "completed@example.com", "111", "Aion", "Portfolio A", 30505)
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Completed User", "completed@example.com", "Aion", "Aion", 111, 30505),
                    new PeopleDirectoryEmployeeDto("Taylor Active", "taylor@example.com", "Aion", "Aion", 222, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    CreatePassedAttempt(completedUserId, publishedQuiz.QuizId)
                ]
            });

        var rows = await service.GetExportDataAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null, null, null, null, null, ["Not Started"]));

        var row = Assert.Single(rows);
        Assert.Equal("Taylor Active", row.FullName);
        Assert.Equal("222", row.EmployeeId);
        Assert.Null(row.LastActiveAt);
    }

    [Fact]
    public async Task GetExportDataAsync_WithDomainFilter_IncludesDirectoryOnlyLearners()
    {
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Taylor Active", "taylor@example.com", "Aion", "Aion", 222, 30505),
                    new PeopleDirectoryEmployeeDto("Jordan Other", "jordan@other.com", "Aion", "Aion", 333, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([]));

        var rows = await service.GetExportDataAsync(new ReportingFilterDto(
            null, "example.com", null, null, [30505], null, null, null, null, null));

        var row = Assert.Single(rows);
        Assert.Equal("Taylor Active", row.FullName);
        Assert.Equal("222", row.EmployeeId);
    }

    [Fact]
    public async Task GetExportDataAsync_WithDateRange_IncludesOverlappingActivity()
    {
        var overlappingUserId = Guid.NewGuid();
        var publishedQuiz = CreatePublishedQuiz(1);
        var service = CreateService(
            currentUser: new TestCurrentUserContext("GlobalAdmin"),
            userRepo: new FakeUserRepository
            {
                AvailableCompanies =
                [
                    new CompanyGroupDto("Portfolio A", [new CompanyOptionDto(30505, "Aion")])
                ],
                FilteredUsers =
                [
                    CreateUser(overlappingUserId, "overlap@example.com", "111", "Aion", "Portfolio A", 30505),
                ]
            },
            peopleDirectory: new FakePeopleDirectoryService
            {
                Employees =
                [
                    new PeopleDirectoryEmployeeDto("Overlapping Activity", "overlap@example.com", "Aion", "Aion", 111, 30505)
                ]
            },
            quizRepo: new FakeQuizRepository([publishedQuiz]),
            attemptRepo: new FakeAttemptRepository
            {
                Attempts =
                [
                    new QuizAttempt
                    {
                        AttemptId = Guid.NewGuid(),
                        UserId = overlappingUserId,
                        QuizId = publishedQuiz.QuizId,
                        AttemptNumber = 1,
                        Status = "Passed",
                        ScorePercent = 100m,
                        TotalCount = 1,
                        StartedAt = new DateTimeOffset(2026, 7, 5, 9, 0, 0, TimeSpan.Zero),
                        CompletedAt = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero)
                    },
                    new QuizAttempt
                    {
                        AttemptId = Guid.NewGuid(),
                        UserId = overlappingUserId,
                        QuizId = publishedQuiz.QuizId,
                        AttemptNumber = 2,
                        Status = "Started",
                        ScorePercent = null,
                        TotalCount = 1,
                        StartedAt = new DateTimeOffset(2026, 7, 9, 9, 0, 0, TimeSpan.Zero),
                        CompletedAt = null
                    },
                    new QuizAttempt
                    {
                        AttemptId = Guid.NewGuid(),
                        UserId = overlappingUserId,
                        QuizId = publishedQuiz.QuizId,
                        AttemptNumber = 3,
                        Status = "Passed",
                        ScorePercent = 100m,
                        TotalCount = 1,
                        StartedAt = new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero),
                        CompletedAt = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero)
                    }
                ]
            });

        var rows = await service.GetExportDataAsync(new ReportingFilterDto(
            null, null, null, null, [30505], null,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero),
            null,
            null));

        Assert.Single(rows);
        Assert.Equal("Test User", rows[0].FullName);
        Assert.Equal("111", rows[0].EmployeeId);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero), rows[0].LastActiveAt);
    }

    private static ReportingService CreateService(
        TestCurrentUserContext? currentUser = null,
        FakeUserRepository? userRepo = null,
        FakeQuizRepository? quizRepo = null,
        FakeAttemptRepository? attemptRepo = null,
        FakeCompletionRepository? completionRepo = null,
        FakeReportingScopeService? scopeService = null,
        FakePeopleDirectoryService? peopleDirectory = null)
    {
        return new ReportingService(
            new FakeReportingDashboardRepository(),
            userRepo ?? new FakeUserRepository(),
            quizRepo ?? new FakeQuizRepository([]),
            attemptRepo ?? new FakeAttemptRepository(),
            completionRepo ?? new FakeCompletionRepository(),
            scopeService ?? new FakeReportingScopeService(null),
            currentUser ?? new TestCurrentUserContext("GlobalAdmin"),
            peopleDirectory ?? new FakePeopleDirectoryService(),
            new MemoryCache(new MemoryCacheOptions()));
    }

    private static Quiz CreatePublishedQuiz(int displayOrder) =>
        new()
        {
            QuizId = Guid.NewGuid(),
            DisplayOrder = displayOrder,
            IsPublished = true,
            IsActive = true,
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

    private static QuizAttempt CreatePassedAttempt(Guid userId, Guid quizId) =>
        new()
        {
            AttemptId = Guid.NewGuid(),
            UserId = userId,
            QuizId = quizId,
            AttemptNumber = 1,
            Status = "Passed",
            ScorePercent = 100m,
            TotalCount = 1,
            StartedAt = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero),
            CompletedAt = new DateTimeOffset(2026, 7, 1, 9, 10, 0, TimeSpan.Zero)
        };

    private static QuizAttempt CreateInProgressAttempt(Guid userId, Guid quizId) =>
        new()
        {
            AttemptId = Guid.NewGuid(),
            UserId = userId,
            QuizId = quizId,
            AttemptNumber = 1,
            Status = "Started",
            ScorePercent = null,
            TotalCount = 1,
            StartedAt = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero),
            CompletedAt = null
        };

    private static User CreateUser(Guid userId, string email, string employeeId, string company, string portfolio, int organizationId) =>
        new()
        {
            UserId = userId,
            Login = email,
            IdpSubjectIdentifier = $"sub-{userId}",
            FirstName = "Test",
            LastName = "User",
            Email = email,
            EmployeeId = employeeId,
            Company = company,
            Portfolio = portfolio,
            OrganizationId = organizationId,
            RoleCode = "Learner",
            IsActive = true,
            AccountCreatedAt = DateTimeOffset.UtcNow
        };

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(string roleCode, Guid? userId = null)
        {
            RoleCode = roleCode;
            UserId = userId ?? Guid.NewGuid();
        }

        public Guid UserId { get; }
        public string Login { get; } = "tester@example.com";
        public string Email { get; } = "tester@example.com";
        public string FullName { get; } = "Tester";
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

    private sealed class FakeReportingDashboardRepository : IReportingDashboardRepository
    {
        public Task<ReportingDashboardDto> GetLanguageDashboardAsync(CancellationToken ct = default) =>
            Task.FromResult(new ReportingDashboardDto(null, []));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public IReadOnlyList<User> FilteredUsers { get; set; } = [];
        public IReadOnlyList<CompanyGroupDto> AvailableCompanies { get; set; } = [];
        public IReadOnlyCollection<int>? LastAvailableCompaniesScope { get; private set; }

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByLoginAsync(string login, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmployeeIdAsync(string employeeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetAllActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> SearchActiveByPrefixAsync(string? searchTerm, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountActiveAsync(string? searchTerm = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetActiveAsync(string? searchTerm = null, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<User>> GetFilteredReportingUsersAsync(
            bool isGlobalAdmin,
            IReadOnlyCollection<int>? scopeOrganizationIds,
            ReportingFilterDto filter,
            int? take = null,
            CancellationToken ct = default,
            Guid? includeUserId = null)
        {
            var query = FilteredUsers.AsEnumerable();

            if (!isGlobalAdmin)
                query = query.Where(u => u.RoleCode == "Learner" || (includeUserId.HasValue && u.UserId == includeUserId.Value));

            if (scopeOrganizationIds is not null)
                query = query.Where(u => u.OrganizationId.HasValue && scopeOrganizationIds.Contains(u.OrganizationId.Value));

            if (filter.Companies is { Length: > 0 })
                query = query.Where(u => u.OrganizationId.HasValue && filter.Companies.Contains(u.OrganizationId.Value));

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                query = query.Where(u =>
                    u.FullName.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (take.HasValue)
                query = query.Take(take.Value);

            return Task.FromResult<IReadOnlyList<User>>(query.ToList());
        }

        public Task<IReadOnlyList<Guid>> GetFilteredReportingUserIdsAsync(
            bool isGlobalAdmin,
            IReadOnlyCollection<int>? scopeOrganizationIds,
            ReportingFilterDto filter,
            int? take = null,
            CancellationToken ct = default)
        {
            var query = FilteredUsers.AsEnumerable();

            if (scopeOrganizationIds is not null)
                query = query.Where(u => u.OrganizationId.HasValue && scopeOrganizationIds.Contains(u.OrganizationId.Value));

            if (filter.Companies is { Length: > 0 })
                query = query.Where(u => u.OrganizationId.HasValue && filter.Companies.Contains(u.OrganizationId.Value));

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                query = query.Where(u =>
                    u.FullName.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (take.HasValue)
                query = query.Take(take.Value);

            return Task.FromResult<IReadOnlyList<Guid>>(query.Select(u => u.UserId).ToList());
        }

        public Task<int> CountFilteredReportingUsersAsync(
            bool isGlobalAdmin,
            IReadOnlyCollection<int>? scopeOrganizationIds,
            ReportingFilterDto filter,
            CancellationToken ct = default)
        {
            var query = FilteredUsers.AsEnumerable();

            if (scopeOrganizationIds is not null)
                query = query.Where(u => u.OrganizationId.HasValue && scopeOrganizationIds.Contains(u.OrganizationId.Value));

            if (filter.Companies is { Length: > 0 })
                query = query.Where(u => u.OrganizationId.HasValue && filter.Companies.Contains(u.OrganizationId.Value));

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                query = query.Where(u =>
                    u.FullName.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(query.Count());
        }

        public Task<IReadOnlyList<string>> GetDistinctReportingDomainsAsync(
            bool isGlobalAdmin,
            IReadOnlyCollection<int>? scopeOrganizationIds,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CompanyGroupDto>> GetAvailableReportingCompaniesAsync(
            IReadOnlyCollection<int>? scopeOrganizationIds,
            CancellationToken ct = default)
        {
            LastAvailableCompaniesScope = scopeOrganizationIds;

            if (scopeOrganizationIds is null)
                return Task.FromResult(AvailableCompanies);

            var filtered = AvailableCompanies
                .Select(group => new CompanyGroupDto(
                    group.PortfolioName,
                    group.Companies.Where(c => scopeOrganizationIds.Contains(c.OrganizationId)).ToList()))
                .Where(group => group.Companies.Count > 0)
                .ToList();

            return Task.FromResult<IReadOnlyList<CompanyGroupDto>>(filtered);
        }

        public Task<User> CreateAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User> UpdateAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeQuizRepository : IQuizRepository
    {
        private readonly IReadOnlyList<Quiz> _publishedQuizzes;

        public FakeQuizRepository(IReadOnlyList<Quiz> publishedQuizzes)
        {
            _publishedQuizzes = publishedQuizzes;
        }

        public Task<IReadOnlyList<Quiz>> GetAllActiveAsync(string lang = "en", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Quiz>> GetAllPublishedAsync(string lang = "en", CancellationToken ct = default) => Task.FromResult(_publishedQuizzes);
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

    private sealed class FakeAttemptRepository : IAttemptRepository
    {
        public IReadOnlyList<QuizAttempt> Attempts { get; set; } = [];

        public Task<QuizAttempt?> GetAttemptByIdAsync(Guid attemptId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> GetAttemptCountAsync(Guid userId, Guid quizId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QuizAttempt>> GetUserAttemptsForQuizAsync(Guid userId, Guid quizId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QuizAttempt>> GetAllAttemptsForUserAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QuizAttempt> CreateAttemptAsync(QuizAttempt attempt, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QuizAttempt> UpdateAttemptAsync(QuizAttempt attempt, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QuizAttempt>> GetAttemptsForReportingAsync(bool includeTestData = false, CancellationToken ct = default) => Task.FromResult(Attempts);
        public Task<IReadOnlyList<(Guid UserId, Guid QuizId, bool HasPassed, int TotalAttempts, decimal? BestScore, DateTimeOffset? LastAttemptAt)>> GetPassStatusSummaryAsync(bool includeTestData = false, IReadOnlyCollection<Guid>? userIds = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeCompletionRepository : ICompletionRepository
    {
        public Task<CompletionRecord?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CompletionRecord> CreateAsync(CompletionRecord record, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CompletionRecord> UpdateAsync(CompletionRecord record, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompletionRecord>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CompletionRecord>>([]);
    }

    private sealed class FakeReportingScopeService : IReportingScopeService
    {
        private readonly HashSet<int>? _scope;

        public FakeReportingScopeService(IEnumerable<int>? scope)
        {
            _scope = scope is null ? null : scope.ToHashSet();
        }

        public Task<HashSet<int>?> GetEffectiveOrganizationScopeAsync(Guid userId, string roleCode, CancellationToken ct = default) =>
            Task.FromResult(_scope is null ? null : new HashSet<int>(_scope));
    }

    private sealed class FakePeopleDirectoryService : IPeopleDirectoryService
    {
        public IReadOnlyList<PeopleDirectoryEmployeeDto> Employees { get; set; } = [];

        public Task<IReadOnlyList<PeopleDirectoryEmployeeDto>> GetEmployeesByCompaniesAsync(IReadOnlyList<string> companyNames, CancellationToken ct = default)
        {
            var filtered = Employees
                .Where(e =>
                    companyNames.Contains(e.Company, StringComparer.OrdinalIgnoreCase) ||
                    companyNames.Contains(e.Vbu, StringComparer.OrdinalIgnoreCase))
                .ToList();

            return Task.FromResult<IReadOnlyList<PeopleDirectoryEmployeeDto>>(filtered);
        }

    }
}
