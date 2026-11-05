using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Security.Claims;
using System.Text.Json;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;
using Volaris101.Application.Services;
using Volaris101.Domain.Entities;
using Volaris101.Infrastructure.Data;
using Volaris101.Infrastructure.Repositories;
using Volaris101.Web.Auth;
using Volaris101.Web.Infrastructure;
using Volaris101.Web.Middleware;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Azure Key Vault ──────────────────────────────────────────────────────────
    if (builder.Environment.IsProduction())
    {
        var keyVaultUri = builder.Configuration["KeyVault:Uri"];
        if (!string.IsNullOrEmpty(keyVaultUri))
            builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
    }

    // ── Serilog ──────────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .WriteTo.Console();

        // Write to file — temporarily disabled to isolate startup issue
        // try
        // {
        //     cfg.WriteTo.File(
        //         path: @"C:\home\LogFiles\Application\volaris101-.log",
        //         rollingInterval: RollingInterval.Day,
        //         retainedFileCountLimit: 7,
        //         outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
        // }
        // catch { /* ignore file logging errors */ }

        var aiCs = ctx.Configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrEmpty(aiCs))
            cfg.WriteTo.ApplicationInsights(aiCs, TelemetryConverter.Traces);
    });

    // ── Database ─────────────────────────────────────────────────────────────────
    builder.Services.AddDbContext<Volaris101DbContext>(opts =>
        opts.UseSqlServer(
            builder.Configuration.GetConnectionString("AzureSQL"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

    // ── Authentication ────────────────────────────────────────────────────────────
    var oktaAuthority = builder.Configuration["Okta:Authority"] ?? string.Empty;
    var oktaConfigured = oktaAuthority.StartsWith("https://") &&
                         !oktaAuthority.Contains("<your-okta-domain>");

    if (oktaConfigured)
    {
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath           = "/auth/login";
                options.LogoutPath          = "/auth/logout";
                options.AccessDeniedPath    = "/auth/access-denied";
                options.SlidingExpiration   = true;
                options.ExpireTimeSpan      = TimeSpan.FromHours(8);
                options.Cookie.HttpOnly     = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite     = SameSiteMode.Lax;
            })
            .AddOpenIdConnect(options =>
            {
                builder.Configuration.GetSection("Okta").Bind(options);
                options.ResponseType = "code";
                options.SaveTokens   = false;
                // Avoid the extra userinfo round-trip because Okta is returning
                // "The 'sub' system claim could not be evaluated" for some users.
                // We rely on claims present in the ID token instead.
                options.GetClaimsFromUserInfoEndpoint = false;
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("Volaris.Quiz");

                // Map custom Okta claims (Okta sends both cases so map both)
                options.ClaimActions.MapUniqueJsonKey("EmployeeId", "EmployeeId");
                options.ClaimActions.MapUniqueJsonKey("employeeId", "employeeId");
                options.ClaimActions.MapUniqueJsonKey("company", "company");
                options.ClaimActions.MapUniqueJsonKey("Portfolio", "Portfolio");
                options.ClaimActions.MapUniqueJsonKey("portfolio", "portfolio");
                options.ClaimActions.MapUniqueJsonKey("VBUName", "VBUName");
                options.ClaimActions.MapUniqueJsonKey("VBUID", "VBUID");
                options.ClaimActions.MapUniqueJsonKey("ValidEmail", "ValidEmail");
                options.ClaimActions.MapUniqueJsonKey("validEmail", "validEmail");
                // Standard profile claims
                options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
                options.ClaimActions.MapJsonKey(ClaimTypes.Surname,   "family_name");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email,     "email");

                options.Events = new OpenIdConnectEvents
                {
                    OnTicketReceived = async ctx =>
                    {
                        var jitService = ctx.HttpContext.RequestServices
                            .GetRequiredService<IOktaJitService>();
                        var failure = await jitService.HandleSignInAsync(ctx.Principal!);
                        if (!string.IsNullOrWhiteSpace(failure))
                            ctx.Fail(failure);
                    },
                    OnRemoteFailure = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        var failureMessage = ctx.Failure?.GetBaseException().Message
                            ?? ctx.Failure?.Message
                            ?? "Authentication failed.";
                        logger.LogError(ctx.Failure, "Okta remote failure: {Message}", failureMessage);
                        var encodedFailure = Uri.EscapeDataString(failureMessage);
                        ctx.Response.Redirect($"/auth/access-denied?reason={encodedFailure}");
                        ctx.HandleResponse();
                        return Task.CompletedTask;
                    }
                };
            });
    }
    else
    {
        // Okta not configured — use cookie auth with dev-login in dev,
        // or a holding page in production
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath           = "/auth/dev-login";
                options.LogoutPath          = "/auth/logout";
                options.AccessDeniedPath    = "/auth/access-denied";
                options.ExpireTimeSpan      = TimeSpan.FromHours(8);
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });
    }

    builder.Services.AddAuthorization(opts =>
    {
        opts.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        opts.AddPolicy("LearnerOrAbove",        p => p.RequireAuthenticatedUser());
        opts.AddPolicy("OrgAdminOrAbove",        p => p.RequireClaim("role", "ReportingAdmin", "OrgAdmin", "PortfolioAdmin", "GlobalAdmin"));
        opts.AddPolicy("PortfolioAdminOrAbove",  p => p.RequireClaim("role", "ReportingAdmin", "PortfolioAdmin", "GlobalAdmin"));
        opts.AddPolicy("GlobalAdmin",            p => p.RequireClaim("role", "GlobalAdmin"));
    });

    // ── Repositories ─────────────────────────────────────────────────────────────
    builder.Services.AddScoped<IUserRepository,       UserRepository>();
    builder.Services.AddScoped<IUserMergeRepository,  UserMergeRepository>();
    builder.Services.AddScoped<IReportingAccessRepository, ReportingAccessRepository>();
    builder.Services.AddScoped<IAccessGroupRepository, AccessGroupRepository>();
    builder.Services.AddScoped<IReportingDashboardRepository, ReportingDashboardRepository>();
    builder.Services.AddScoped<IQuizRepository,       QuizRepository>();
    builder.Services.AddScoped<IAttemptRepository,    AttemptRepository>();
    builder.Services.AddScoped<ICompletionRepository, CompletionRepository>();
    builder.Services.AddScoped<IAuditLogRepository,   AuditLogRepository>();

    // ── Application Services ──────────────────────────────────────────────────────
    builder.Services.AddScoped<ILearnerService,     LearnerService>();
    builder.Services.AddScoped<IReportingService,   ReportingService>();
    builder.Services.AddScoped<IReportingScopeService, ReportingScopeService>();
    builder.Services.AddScoped<IAccessGroupService, AccessGroupService>();
    builder.Services.AddScoped<ICmsService,         CmsService>();
    builder.Services.AddScoped<IUserAdminService,   UserAdminService>();
    builder.Services.AddScoped<IImpersonationService, ImpersonationService>();
    builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
    builder.Services.AddScoped<IOktaJitService,     OktaJitService>();
    builder.Services.Configure<OktaJitOptions>(builder.Configuration.GetSection("Okta"));

    builder.Services.AddControllersWithViews()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new EmptyStringToNullGuidConverter());
        });
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new EmptyStringToNullGuidConverter());
    });
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMemoryCache();
    builder.Services.AddOptions<PeopleDirectoryApiOptions>().Configure(options =>
    {
        options.BaseUrl = builder.Configuration["PEOPLE_API_BASE_URL"]
            ?? throw new InvalidOperationException(
                "Missing required configuration setting 'PEOPLE_API_BASE_URL'.");
        options.Scope = builder.Configuration["PEOPLE_API_SCOPE"]
            ?? throw new InvalidOperationException(
                "Missing required configuration setting 'PEOPLE_API_SCOPE'.");

        if (int.TryParse(
                builder.Configuration["PeopleDirectoryApi:TimeoutSeconds"],
                out var timeoutSeconds))
            options.TimeoutSeconds = timeoutSeconds;
    });
    builder.Services.AddSingleton<IAppOnlyTokenProvider, ManagedIdentityTokenProvider>();

    // ── VBU Master List API client ───────────────────────────────────────────────────────────
    builder.Services.AddHttpClient<IPeopleDirectoryService, PeopleDirectoryService>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PeopleDirectoryApiOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30);
    });

    var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrEmpty(aiConnectionString))
        builder.Services.AddApplicationInsightsTelemetry(options => options.ConnectionString = aiConnectionString);

    builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);
    builder.Services.AddHealthChecks().AddCheck<DbContextHealthCheck>("database");

    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseHttpsRedirection();
    app.UseResponseCompression();
    app.UseStaticFiles();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseAuthentication();
    app.UseMiddleware<UserContextMiddleware>();
    app.UseAuthorization();

    app.MapPost("/admin/cms/question/add",
        async (JsonElement body, HttpContext ctx, IAntiforgery antiforgery, ICmsService cms, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
                var (quizId, question) = ParseAddQuestionBody(body);
                if (quizId == Guid.Empty)
                    return Results.BadRequest(new { error = "QuizId is required." });
                if (question == null)
                    return Results.BadRequest(new { error = "Question is required." });
                if (string.IsNullOrWhiteSpace(question.QuestionText))
                    return Results.BadRequest(new { error = "Question text is required." });

                question = question with
                {
                    QuestionId = question.QuestionId == Guid.Empty ? Guid.Empty : question.QuestionId,
                    QuizId = quizId,
                    Options = question.Options ?? Array.Empty<CmsOptionDto>()
                };

                var created = await cms.AddQuestionAsync(quizId, question, ct);
                return Results.Json(created);
            }
            catch (AntiforgeryValidationException ex)
            {
                logger.LogWarning(ex, "Add question rejected because the request verification token was invalid.");
                return Results.BadRequest(new { error = "Your admin session token expired. Refresh the page and try again." });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add question.");
                return Results.Json(new { error = $"Failed to save question: {ex.GetBaseException().Message}" }, statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .RequireAuthorization("GlobalAdmin");

    app.MapPost("/learner/quiz/submit",
        async (SubmitQuizRequestDto request, HttpContext ctx, IAntiforgery antiforgery, ILearnerService learner, ICurrentUserContext user, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
                if (request.AttemptId == Guid.Empty)
                    return Results.BadRequest(new { error = "AttemptId is required." });
                if (request.Answers == null || request.Answers.Count == 0)
                    return Results.BadRequest(new { error = "At least one answer is required." });

                var result = await learner.SubmitQuizAsync(user.UserId, request, user.PreferredLanguage, ct);
                return Results.Json(result);
            }
            catch (AntiforgeryValidationException ex)
            {
                logger.LogWarning(ex, "Quiz submit rejected because the request verification token was invalid.");
                return Results.BadRequest(new { error = "Your quiz session expired. Refresh the page and try again." });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit quiz.");
                return Results.Json(new { error = $"Failed to submit quiz: {ex.GetBaseException().Message}" }, statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .RequireAuthorization();

    app.MapPost("/admin/cms/quiz/{quizId:guid}/question/add",
        async (Guid quizId, CmsQuestionDto question, HttpContext ctx, IAntiforgery antiforgery, ICmsService cms, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
                var created = await cms.AddQuestionAsync(quizId, question, ct);
                return Results.Json(created);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add question through quiz-scoped route.");
                return Results.Json(new { error = $"Failed to save question: {ex.GetBaseException().Message}" }, statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .RequireAuthorization("GlobalAdmin");

    // ── Dev login — Development only ──────────────────────────────────────────────
    if (!oktaConfigured && app.Environment.IsDevelopment())
    {
        app.MapGet("/auth/dev-login", [AllowAnonymous] async (HttpContext ctx, Volaris101DbContext db) =>
        {
            try
            {
                var devId = new Guid("00000000-0000-0000-0000-000000000001");
                if (await db.Users.FindAsync(devId) == null)
                {
                    db.Users.Add(new User
                    {
                        UserId               = devId,
                        Login                = "dev-user-001",
                        IdpSource            = "Dev",
                        IdpSubjectIdentifier = "dev-user-001",
                        FirstName            = "Dev",
                        LastName             = "Admin",
                        Email                = "dev.admin@volaris.com",
                        EmployeeId           = "DEV-001",
                        Company              = "Volaris Group",
                        Portfolio            = "North America",
                        RoleCode             = "GlobalAdmin",
                        AccountCreatedAt     = DateTimeOffset.UtcNow,
                        LastLoginAt          = DateTimeOffset.UtcNow,
                        IsActive             = true
                    });
                    await db.SaveChangesAsync();
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "dev-user-001"),
                    new Claim(ClaimTypes.Email,          "dev.admin@volaris.com"),
                    new Claim("userId",            devId.ToString()),
                    new Claim("role",              "GlobalAdmin"),
                    new Claim("displayName",       "Dev Admin"),
                    new Claim("initials",          "DA"),
                    new Claim("preferredLanguage", "en"),
                };
                var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                ctx.Response.Redirect("/");
            }
            catch (Exception ex)
            {
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.WriteAsync($"Dev login failed: {ex.Message}\n\n{ex}");
            }
        }).AllowAnonymous();
    }

    // ── Holding page — Production without Okta configured ────────────────────────
    if (!oktaConfigured && !app.Environment.IsDevelopment())
    {
        app.MapGet("/auth/dev-login", [AllowAnonymous] async (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(
                "<html><body style='font-family:sans-serif;text-align:center;margin-top:80px'>" +
                "<h2>Volaris 101</h2>" +
                "<p style='color:#888'>Authentication is not yet configured.<br>" +
                "Please set <strong>Okta__Authority</strong>, <strong>Okta__ClientId</strong> and " +
                "<strong>Okta__ClientSecret</strong> in Azure App Service Configuration.</p>" +
                "</body></html>");
        }).AllowAnonymous();
    }

    app.MapControllerRoute("default", "{controller=Learner}/{action=Dashboard}/{id?}");
    app.MapHealthChecks("/health");

    // ── DB startup: create schema if missing ─────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db  = scope.ServiceProvider.GetRequiredService<Volaris101DbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            await CreateSchemaIfMissingAsync(db);
            var accessGroups = scope.ServiceProvider.GetRequiredService<IAccessGroupService>();
            var seeded = await accessGroups.SeedSystemManagedGroupsAsync();
            log.LogInformation(
                "Access group seed complete: portfolioGroupsCreated={PortfolioGroupsCreated}, portfolioSubGroupsCreated={PortfolioSubGroupsCreated}",
                seeded.PortfolioGroupsCreated,
                seeded.PortfolioSubGroupsCreated);
        }
        catch (Exception ex) { log.LogError(ex, "DB startup task failed — check connection string"); }
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
static (Guid QuizId, CmsQuestionDto? Question) ParseAddQuestionBody(JsonElement body)
{
    if (body.ValueKind != JsonValueKind.Object)
        return (Guid.Empty, null);

    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new EmptyStringToNullGuidConverter());
    CmsQuestionDto? question;

    if (body.TryGetProperty("question", out var questionElement) &&
        questionElement.ValueKind != JsonValueKind.Null &&
        questionElement.ValueKind != JsonValueKind.Undefined)
    {
        question = questionElement.Deserialize<CmsQuestionDto>(options);
    }
    else
    {
        question = body.Deserialize<CmsQuestionDto>(options);
    }

    var quizId = TryReadGuidProperty(body, "quizId") ?? question?.QuizId ?? Guid.Empty;
    return (quizId, question);
}

static Guid? TryReadGuidProperty(JsonElement body, string propertyName)
{
    foreach (var property in body.EnumerateObject())
    {
        if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            continue;

        if (property.Value.ValueKind == JsonValueKind.String &&
            Guid.TryParse(property.Value.GetString(), out var stringGuid))
            return stringGuid;

        if (property.Value.ValueKind == JsonValueKind.Undefined ||
            property.Value.ValueKind == JsonValueKind.Null)
            return null;

        try
        {
            return property.Value.GetGuid();
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    return null;
}

// ── Schema creation ───────────────────────────────────────────────────────────
static async Task CreateSchemaIfMissingAsync(Volaris101.Infrastructure.Data.Volaris101DbContext db)
{
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Organizations')
        CREATE TABLE Organizations (
            OrganizationID   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            OrganizationName NVARCHAR(200)    NOT NULL,
            CreatedAt        DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            CONSTRAINT PK_Organizations PRIMARY KEY (OrganizationID),
            CONSTRAINT UQ_Organizations_Name UNIQUE (OrganizationName)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Portfolios')
        CREATE TABLE Portfolios (
            PortfolioID   INT              NOT NULL,
            PortfolioName NVARCHAR(200)    NOT NULL,
            CreatedAt     DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            CONSTRAINT PK_Portfolios PRIMARY KEY (PortfolioID),
            CONSTRAINT UQ_Portfolios_Name UNIQUE (PortfolioName)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
        CREATE TABLE Users (
            UserID                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            Login                   NVARCHAR(256)    NOT NULL,
            IdpSource               NVARCHAR(50)     NOT NULL DEFAULT 'Okta',
            IdpSubjectIdentifier    NVARCHAR(256)    NOT NULL,
            FirstName               NVARCHAR(100)    NOT NULL,
            LastName                NVARCHAR(100)    NOT NULL,
            Email                   NVARCHAR(256)    NOT NULL,
            EmployeeId              NVARCHAR(100)    NOT NULL,
            Company                 NVARCHAR(200)    NULL,
            Portfolio               NVARCHAR(200)    NULL,
            OrganizationID          INT              NULL,
            PortfolioID             INT              NULL,
            PreferredLanguage       CHAR(2)          NOT NULL DEFAULT 'en',
            RoleCode                NVARCHAR(20)     NOT NULL DEFAULT 'Learner',
            AccountCreatedAt        DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            LastLoginAt             DATETIMEOFFSET(3) NULL,
            LastAttributeRefreshAt  DATETIMEOFFSET(3) NULL,
            IsActive                BIT              NOT NULL DEFAULT 1,
            CONSTRAINT PK_Users       PRIMARY KEY (UserID),
            CONSTRAINT UQ_Users_Login UNIQUE (Login),
            CONSTRAINT UQ_Users_Email UNIQUE (Email)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL
           AND COL_LENGTH('dbo.Roles', 'RoleCode') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleCode = N'ReportingAdmin')
        BEGIN
            DECLARE @SourceRoleCode NVARCHAR(20);
            SELECT TOP (1) @SourceRoleCode = RoleCode
            FROM dbo.Roles
            WHERE RoleCode IN (N'PortfolioAdmin', N'OrgAdmin', N'GlobalAdmin', N'Learner')
            ORDER BY CASE RoleCode
                WHEN N'PortfolioAdmin' THEN 1
                WHEN N'OrgAdmin' THEN 2
                WHEN N'GlobalAdmin' THEN 3
                ELSE 4
            END;

            IF @SourceRoleCode IS NOT NULL
            BEGIN
                DECLARE @RoleColumns NVARCHAR(MAX);
                DECLARE @RoleValues NVARCHAR(MAX);
                DECLARE @RoleSql NVARCHAR(MAX);

                SELECT
                    @RoleColumns = STRING_AGG(QUOTENAME(c.name), N', ') WITHIN GROUP (ORDER BY c.column_id),
                    @RoleValues = STRING_AGG(
                        CASE
                            WHEN c.name = N'RoleCode' THEN N'N''ReportingAdmin'''
                            WHEN c.name IN (N'RoleName', N'Name', N'DisplayName', N'Title') THEN N'N''Reporting Admin'''
                            WHEN c.name = N'Description' THEN N'N''Reporting access scoped by assigned access groups.'''
                            WHEN c.name IN (N'IsActive', N'Active') THEN N'CAST(1 AS BIT)'
                            WHEN c.name IN (N'SortOrder', N'DisplayOrder') THEN N'(SELECT ISNULL(MAX(' + QUOTENAME(c.name) + N'), 0) + 1 FROM dbo.Roles)'
                            ELSE N'r.' + QUOTENAME(c.name)
                        END,
                        N', ') WITHIN GROUP (ORDER BY c.column_id)
                FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.Roles')
                  AND c.is_identity = 0
                  AND c.is_computed = 0
                  AND c.system_type_id <> 189;

                SET @RoleSql = N'INSERT INTO dbo.Roles (' + @RoleColumns + N')
                    SELECT ' + @RoleValues + N'
                    FROM dbo.Roles r
                    WHERE r.RoleCode = @SourceRoleCode;';

                EXEC sp_executesql @RoleSql, N'@SourceRoleCode NVARCHAR(20)', @SourceRoleCode;
            END;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Quizzes')
        CREATE TABLE Quizzes (
            QuizID       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            DisplayOrder INT              NOT NULL,
            PassingScore DECIMAL(5,2)     NOT NULL DEFAULT 50.00,
            IsActive     BIT              NOT NULL DEFAULT 1,
            IsPublished  BIT              NOT NULL DEFAULT 0,
            CreatedAt    DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            UpdatedAt    DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            CONSTRAINT PK_Quizzes       PRIMARY KEY (QuizID),
            CONSTRAINT UQ_Quizzes_Order UNIQUE (DisplayOrder)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QuizTranslations')
        CREATE TABLE QuizTranslations (
            QuizID       UNIQUEIDENTIFIER NOT NULL,
            LanguageCode CHAR(2)          NOT NULL,
            Title        NVARCHAR(500)    NOT NULL,
            CONSTRAINT PK_QuizTranslations PRIMARY KEY (QuizID, LanguageCode),
            CONSTRAINT FK_QT_Quiz FOREIGN KEY (QuizID) REFERENCES Quizzes(QuizID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Questions')
        CREATE TABLE Questions (
            QuestionID    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            QuizID        UNIQUEIDENTIFIER NOT NULL,
            DisplayOrder  INT              NOT NULL,
            IsMultiSelect BIT              NOT NULL DEFAULT 0,
            IsActive      BIT              NOT NULL DEFAULT 1,
            CreatedAt     DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            UpdatedAt     DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            CONSTRAINT PK_Questions PRIMARY KEY (QuestionID),
            CONSTRAINT FK_Q_Quiz    FOREIGN KEY (QuizID) REFERENCES Quizzes(QuizID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QuestionTranslations')
        CREATE TABLE QuestionTranslations (
            QuestionID   UNIQUEIDENTIFIER NOT NULL,
            LanguageCode CHAR(2)          NOT NULL,
            QuestionText NVARCHAR(2000)   NOT NULL,
            CONSTRAINT PK_QuestionTranslations PRIMARY KEY (QuestionID, LanguageCode),
            CONSTRAINT FK_QuT_Question FOREIGN KEY (QuestionID) REFERENCES Questions(QuestionID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnswerOptions')
        CREATE TABLE AnswerOptions (
            OptionID     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            QuestionID   UNIQUEIDENTIFIER NOT NULL,
            OptionKey    NCHAR(1)         NOT NULL DEFAULT N'A',
            DisplayOrder INT              NOT NULL,
            IsCorrect    BIT              NOT NULL DEFAULT 0,
            CONSTRAINT PK_AnswerOptions PRIMARY KEY (OptionID),
            CONSTRAINT FK_AO_Question   FOREIGN KEY (QuestionID) REFERENCES Questions(QuestionID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnswerOptionTranslations')
        CREATE TABLE AnswerOptionTranslations (
            OptionID     UNIQUEIDENTIFIER NOT NULL,
            LanguageCode CHAR(2)          NOT NULL,
            OptionText   NVARCHAR(1000)   NOT NULL,
            CONSTRAINT PK_AnswerOptionTranslations PRIMARY KEY (OptionID, LanguageCode),
            CONSTRAINT FK_AOT_Option FOREIGN KEY (OptionID) REFERENCES AnswerOptions(OptionID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QuizAttempts')
        CREATE TABLE QuizAttempts (
            AttemptID     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            UserID        UNIQUEIDENTIFIER NOT NULL,
            QuizID        UNIQUEIDENTIFIER NOT NULL,
            AttemptNumber INT              NOT NULL,
            Status        NVARCHAR(20)     NOT NULL DEFAULT 'InProgress',
            IsTestData    BIT              NOT NULL DEFAULT 0,
            RawScore      INT              NULL,
            TotalQuestions INT             NULL,
            ScorePercent  DECIMAL(5,2)     NULL,
            CurrentQuestionPosition INT    NULL,
            StartedAt     DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            CompletedAt   DATETIMEOFFSET(3) NULL,
            CONSTRAINT PK_QuizAttempts PRIMARY KEY (AttemptID),
            CONSTRAINT FK_QA_User FOREIGN KEY (UserID) REFERENCES Users(UserID),
            CONSTRAINT FK_QA_Quiz FOREIGN KEY (QuizID) REFERENCES Quizzes(QuizID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('dbo.ReportingAttemptSummaryCache', 'U') IS NULL
        BEGIN
            CREATE TABLE dbo.ReportingAttemptSummaryCache (
                UserID        UNIQUEIDENTIFIER NOT NULL,
                QuizID        UNIQUEIDENTIFIER NOT NULL,
                IsTestData    BIT              NOT NULL,
                HasPassed     BIT              NOT NULL,
                TotalAttempts INT              NOT NULL,
                BestScore     DECIMAL(5,2)     NULL,
                LastAttemptAt DATETIMEOFFSET(3) NULL,
                CONSTRAINT PK_ReportingAttemptSummaryCache PRIMARY KEY (UserID, QuizID, IsTestData)
            );
        END;

        TRUNCATE TABLE dbo.ReportingAttemptSummaryCache;
        INSERT INTO dbo.ReportingAttemptSummaryCache (UserID, QuizID, IsTestData, HasPassed, TotalAttempts, BestScore, LastAttemptAt)
        SELECT
            a.UserID,
            a.QuizID,
            a.IsTestData,
            CAST(CASE WHEN SUM(CASE WHEN a.Status = 'Passed' THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS bit) AS HasPassed,
            COUNT(*) AS TotalAttempts,
            MAX(CASE WHEN a.Status = 'Passed' THEN a.ScorePercent END) AS BestScore,
            MAX(COALESCE(a.CompletedAt, a.StartedAt)) AS LastAttemptAt
        FROM dbo.QuizAttempts a
        WHERE a.Status <> 'InProgress'
        GROUP BY a.UserID, a.QuizID, a.IsTestData;");

    await db.Database.ExecuteSqlRawAsync(@"
        CREATE OR ALTER TRIGGER dbo.TR_QuizAttempts_RefreshReportingAttemptSummaryCache
        ON dbo.QuizAttempts
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            DECLARE @Affected TABLE (
                UserID UNIQUEIDENTIFIER NOT NULL,
                QuizID UNIQUEIDENTIFIER NOT NULL,
                IsTestData BIT NOT NULL
            );

            INSERT INTO @Affected (UserID, QuizID, IsTestData)
            SELECT DISTINCT UserID, QuizID, IsTestData FROM inserted
            UNION
            SELECT DISTINCT UserID, QuizID, IsTestData FROM deleted;

            DELETE c
            FROM dbo.ReportingAttemptSummaryCache c
            INNER JOIN @Affected a
                ON a.UserID = c.UserID
               AND a.QuizID = c.QuizID
               AND a.IsTestData = c.IsTestData;

            INSERT INTO dbo.ReportingAttemptSummaryCache (UserID, QuizID, IsTestData, HasPassed, TotalAttempts, BestScore, LastAttemptAt)
            SELECT
                a.UserID,
                a.QuizID,
                a.IsTestData,
                CAST(CASE WHEN SUM(CASE WHEN a.Status = 'Passed' THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS bit) AS HasPassed,
                COUNT(*) AS TotalAttempts,
                MAX(CASE WHEN a.Status = 'Passed' THEN a.ScorePercent END) AS BestScore,
                MAX(COALESCE(a.CompletedAt, a.StartedAt)) AS LastAttemptAt
            FROM dbo.QuizAttempts a
            INNER JOIN @Affected af
                ON af.UserID = a.UserID
               AND af.QuizID = a.QuizID
               AND af.IsTestData = a.IsTestData
            WHERE a.Status <> 'InProgress'
            GROUP BY a.UserID, a.QuizID, a.IsTestData;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttemptAnswers')
        CREATE TABLE AttemptAnswers (
            AttemptAnswerID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            AttemptID       UNIQUEIDENTIFIER NOT NULL,
            QuestionID      UNIQUEIDENTIFIER NOT NULL,
            OptionID        UNIQUEIDENTIFIER NOT NULL,
            SelectedAt      DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            CONSTRAINT PK_AttemptAnswers PRIMARY KEY (AttemptAnswerID),
            CONSTRAINT FK_AA_Attempt  FOREIGN KEY (AttemptID)  REFERENCES QuizAttempts(AttemptID),
            CONSTRAINT FK_AA_Question FOREIGN KEY (QuestionID) REFERENCES Questions(QuestionID),
            CONSTRAINT FK_AA_Option   FOREIGN KEY (OptionID)   REFERENCES AnswerOptions(OptionID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CompletionRecords')
        CREATE TABLE CompletionRecords (
            CompletionID            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            UserID                  UNIQUEIDENTIFIER NOT NULL,
            CompletedAt             DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            EmailNotificationSentAt DATETIMEOFFSET(3) NULL,
            ExportedAt              DATETIMEOFFSET(3) NULL,
            SnapshotFirstName       NVARCHAR(100)    NOT NULL,
            SnapshotLastName        NVARCHAR(100)    NOT NULL,
            SnapshotEmail           NVARCHAR(256)    NOT NULL,
            SnapshotEmployeeId      NVARCHAR(100)    NOT NULL,
            SnapshotCompany         NVARCHAR(200)    NOT NULL,
            SnapshotPortfolio       NVARCHAR(200)    NOT NULL,
            CONSTRAINT PK_CompletionRecords PRIMARY KEY (CompletionID),
            CONSTRAINT UQ_CR_UserID UNIQUE (UserID),
            CONSTRAINT FK_CR_User   FOREIGN KEY (UserID) REFERENCES Users(UserID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdminAuditLog')
        CREATE TABLE AdminAuditLog (
            LogEntryID      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            LoggedAt        DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            ActionType      NVARCHAR(100)    NOT NULL,
            Outcome         NVARCHAR(50)     NOT NULL,
            DenialReason    NVARCHAR(100)    NULL,
            ActorUserID     UNIQUEIDENTIFIER NULL,
            ActorType       NVARCHAR(20)     NOT NULL DEFAULT 'system',
            TargetUserID    UNIQUEIDENTIFIER NULL,
            BeforeValue     NVARCHAR(1000)   NULL,
            AfterValue      NVARCHAR(1000)   NULL,
            SourceComponent NVARCHAR(100)    NULL,
            CorrelationID   UNIQUEIDENTIFIER NULL,
            IpAddress       NVARCHAR(45)     NULL,
            SessionId       NVARCHAR(256)    NULL,
            AdditionalData  NVARCHAR(MAX)    NULL,
            CONSTRAINT PK_AdminAuditLog PRIMARY KEY (LogEntryID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrgAdminAssignments')
        CREATE TABLE OrgAdminAssignments (
            AssignmentID    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            UserID          UNIQUEIDENTIFIER NOT NULL,
            OrganizationID  INT              NOT NULL,
            AssignedAt      DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            AssignedByUserID UNIQUEIDENTIFIER NOT NULL,
            CONSTRAINT PK_OrgAdminAssignments PRIMARY KEY (AssignmentID),
            CONSTRAINT UQ_OAA_UserOrg UNIQUE (UserID, OrganizationID),
            CONSTRAINT FK_OAA_User FOREIGN KEY (UserID) REFERENCES Users(UserID),
            CONSTRAINT FK_OAA_Org FOREIGN KEY (OrganizationID) REFERENCES Organizations(OrganizationID),
            CONSTRAINT FK_OAA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES Users(UserID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('dbo.OrgAdminAssignments', 'U') IS NOT NULL
        BEGIN
            IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignmentID') IS NULL
                ALTER TABLE dbo.OrgAdminAssignments ADD AssignmentID UNIQUEIDENTIFIER NULL;
            IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedAt') IS NULL
                ALTER TABLE dbo.OrgAdminAssignments ADD AssignedAt DATETIMEOFFSET(3) NULL;
            IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedByUserID') IS NULL
                ALTER TABLE dbo.OrgAdminAssignments ADD AssignedByUserID UNIQUEIDENTIFIER NULL;
            IF COL_LENGTH('dbo.OrgAdminAssignments', 'CreatedByUserID') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments')
                     AND name = 'CreatedByUserID'
                     AND is_nullable = 0
               )
                ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN CreatedByUserID UNIQUEIDENTIFIER NULL;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('dbo.OrgAdminAssignments', 'U') IS NOT NULL
        BEGIN
            UPDATE dbo.OrgAdminAssignments
            SET AssignmentID = ISNULL(AssignmentID, NEWID()),
                AssignedAt = ISNULL(AssignedAt, SYSDATETIMEOFFSET()),
                AssignedByUserID = ISNULL(AssignedByUserID, UserID);

            DECLARE @OrgLegacySql NVARCHAR(MAX) = N'
                UPDATE dbo.OrgAdminAssignments
                SET AssignedByUserID = ISNULL(AssignedByUserID, ' + CASE
                    WHEN COL_LENGTH('dbo.OrgAdminAssignments', 'CreatedByUserID') IS NOT NULL THEN N'CreatedByUserID'
                    ELSE N'UserID'
                END + N');';
            EXEC(@OrgLegacySql);

            IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignmentID') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments')
                     AND name = 'AssignmentID'
                     AND is_nullable = 1
               )
                ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN AssignmentID UNIQUEIDENTIFIER NOT NULL;

            IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedAt') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments')
                     AND name = 'AssignedAt'
                     AND is_nullable = 1
               )
                ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN AssignedAt DATETIMEOFFSET(3) NOT NULL;

            IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedByUserID') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments')
                     AND name = 'AssignedByUserID'
                     AND is_nullable = 1
               )
                ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN AssignedByUserID UNIQUEIDENTIFIER NOT NULL;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('dbo.OrgAdminAssignments', 'U') IS NOT NULL
        BEGIN
            DECLARE @OaaPk sysname = (
                SELECT kc.name
                FROM sys.key_constraints kc
                WHERE kc.parent_object_id = OBJECT_ID('dbo.OrgAdminAssignments')
                  AND kc.[type] = 'PK'
            );

            IF @OaaPk IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.index_columns ic
                   JOIN sys.columns c
                        ON c.object_id = ic.object_id
                       AND c.column_id = ic.column_id
                   WHERE ic.object_id = OBJECT_ID('dbo.OrgAdminAssignments')
                     AND ic.index_id = (SELECT index_id FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments') AND name = @OaaPk)
                     AND ic.key_ordinal = 1
                     AND c.name = 'AssignmentID'
               )
                EXEC('ALTER TABLE dbo.OrgAdminAssignments DROP CONSTRAINT [' + @OaaPk + ']');

            IF NOT EXISTS (
                SELECT 1
                FROM sys.default_constraints dc
                JOIN sys.columns c
                  ON c.object_id = dc.parent_object_id
                 AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.OrgAdminAssignments')
                  AND c.name = 'AssignmentID'
            )
                ALTER TABLE dbo.OrgAdminAssignments ADD CONSTRAINT DF_OrgAdminAssignments_AssignmentID DEFAULT NEWSEQUENTIALID() FOR AssignmentID;
            IF NOT EXISTS (
                SELECT 1
                FROM sys.default_constraints dc
                JOIN sys.columns c
                  ON c.object_id = dc.parent_object_id
                 AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.OrgAdminAssignments')
                  AND c.name = 'AssignedAt'
            )
                ALTER TABLE dbo.OrgAdminAssignments ADD CONSTRAINT DF_OrgAdminAssignments_AssignedAt DEFAULT SYSDATETIMEOFFSET() FOR AssignedAt;

            IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_OrgAdminAssignments')
                ALTER TABLE dbo.OrgAdminAssignments ADD CONSTRAINT PK_OrgAdminAssignments PRIMARY KEY (AssignmentID);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_OAA_UserOrg' AND object_id = OBJECT_ID('dbo.OrgAdminAssignments'))
                ALTER TABLE dbo.OrgAdminAssignments ADD CONSTRAINT UQ_OAA_UserOrg UNIQUE (UserID, OrganizationID);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OAA_User')
                ALTER TABLE dbo.OrgAdminAssignments WITH CHECK ADD CONSTRAINT FK_OAA_User FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OAA_Org')
                ALTER TABLE dbo.OrgAdminAssignments WITH CHECK ADD CONSTRAINT FK_OAA_Org FOREIGN KEY (OrganizationID) REFERENCES dbo.Organizations(OrganizationID);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OAA_AssignedBy')
                ALTER TABLE dbo.OrgAdminAssignments WITH CHECK ADD CONSTRAINT FK_OAA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES dbo.Users(UserID);
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PortfolioAdminAssignments')
        CREATE TABLE PortfolioAdminAssignments (
            AssignmentID     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            UserID           UNIQUEIDENTIFIER NOT NULL,
            PortfolioID      INT              NOT NULL,
            AssignedAt       DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            AssignedByUserID UNIQUEIDENTIFIER NOT NULL,
            CONSTRAINT PK_PortfolioAdminAssignments PRIMARY KEY (AssignmentID),
            CONSTRAINT UQ_PAA_UserPortfolio UNIQUE (UserID, PortfolioID),
            CONSTRAINT FK_PAA_User FOREIGN KEY (UserID) REFERENCES Users(UserID),
            CONSTRAINT FK_PAA_Portfolio FOREIGN KEY (PortfolioID) REFERENCES Portfolios(PortfolioID),
            CONSTRAINT FK_PAA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES Users(UserID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('dbo.PortfolioAdminAssignments', 'U') IS NOT NULL
        BEGIN
            IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignmentID') IS NULL
                ALTER TABLE dbo.PortfolioAdminAssignments ADD AssignmentID UNIQUEIDENTIFIER NULL;
            IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedAt') IS NULL
                ALTER TABLE dbo.PortfolioAdminAssignments ADD AssignedAt DATETIMEOFFSET(3) NULL;
            IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedByUserID') IS NULL
                ALTER TABLE dbo.PortfolioAdminAssignments ADD AssignedByUserID UNIQUEIDENTIFIER NULL;
            IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'CreatedByUserID') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
                     AND name = 'CreatedByUserID'
                     AND is_nullable = 0
               )
                ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN CreatedByUserID UNIQUEIDENTIFIER NULL;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('dbo.PortfolioAdminAssignments', 'U') IS NOT NULL
        BEGIN
            UPDATE dbo.PortfolioAdminAssignments
            SET AssignmentID = ISNULL(AssignmentID, NEWID()),
                AssignedAt = ISNULL(AssignedAt, SYSDATETIMEOFFSET()),
                AssignedByUserID = ISNULL(AssignedByUserID, UserID);

            DECLARE @PortfolioLegacySql NVARCHAR(MAX) = N'
                UPDATE dbo.PortfolioAdminAssignments
                SET AssignedByUserID = ISNULL(AssignedByUserID, ' + CASE
                    WHEN COL_LENGTH('dbo.PortfolioAdminAssignments', 'CreatedByUserID') IS NOT NULL THEN N'CreatedByUserID'
                    ELSE N'UserID'
                END + N');';
            EXEC(@PortfolioLegacySql);

            IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignmentID') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
                     AND name = 'AssignmentID'
                     AND is_nullable = 1
               )
                ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN AssignmentID UNIQUEIDENTIFIER NOT NULL;

            IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedAt') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
                     AND name = 'AssignedAt'
                     AND is_nullable = 1
               )
                ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN AssignedAt DATETIMEOFFSET(3) NOT NULL;

            IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedByUserID') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
                     AND name = 'AssignedByUserID'
                     AND is_nullable = 1
               )
                ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN AssignedByUserID UNIQUEIDENTIFIER NOT NULL;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID('dbo.PortfolioAdminAssignments', 'U') IS NOT NULL
        BEGIN
            DECLARE @PaaPk sysname = (
                SELECT kc.name
                FROM sys.key_constraints kc
                WHERE kc.parent_object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
                  AND kc.[type] = 'PK'
            );

            IF @PaaPk IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.index_columns ic
                   JOIN sys.columns c
                        ON c.object_id = ic.object_id
                       AND c.column_id = ic.column_id
                   WHERE ic.object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
                     AND ic.index_id = (SELECT index_id FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments') AND name = @PaaPk)
                     AND ic.key_ordinal = 1
                     AND c.name = 'AssignmentID'
               )
                EXEC('ALTER TABLE dbo.PortfolioAdminAssignments DROP CONSTRAINT [' + @PaaPk + ']');

            IF NOT EXISTS (
                SELECT 1
                FROM sys.default_constraints dc
                JOIN sys.columns c
                  ON c.object_id = dc.parent_object_id
                 AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
                  AND c.name = 'AssignmentID'
            )
                ALTER TABLE dbo.PortfolioAdminAssignments ADD CONSTRAINT DF_PortfolioAdminAssignments_AssignmentID DEFAULT NEWSEQUENTIALID() FOR AssignmentID;
            IF NOT EXISTS (
                SELECT 1
                FROM sys.default_constraints dc
                JOIN sys.columns c
                  ON c.object_id = dc.parent_object_id
                 AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
                  AND c.name = 'AssignedAt'
            )
                ALTER TABLE dbo.PortfolioAdminAssignments ADD CONSTRAINT DF_PortfolioAdminAssignments_AssignedAt DEFAULT SYSDATETIMEOFFSET() FOR AssignedAt;

            IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_PortfolioAdminAssignments')
                ALTER TABLE dbo.PortfolioAdminAssignments ADD CONSTRAINT PK_PortfolioAdminAssignments PRIMARY KEY (AssignmentID);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_PAA_UserPortfolio' AND object_id = OBJECT_ID('dbo.PortfolioAdminAssignments'))
                ALTER TABLE dbo.PortfolioAdminAssignments ADD CONSTRAINT UQ_PAA_UserPortfolio UNIQUE (UserID, PortfolioID);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PAA_User')
                ALTER TABLE dbo.PortfolioAdminAssignments WITH CHECK ADD CONSTRAINT FK_PAA_User FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PAA_Portfolio')
                ALTER TABLE dbo.PortfolioAdminAssignments WITH CHECK ADD CONSTRAINT FK_PAA_Portfolio FOREIGN KEY (PortfolioID) REFERENCES dbo.Portfolios(PortfolioID);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PAA_AssignedBy')
                ALTER TABLE dbo.PortfolioAdminAssignments WITH CHECK ADD CONSTRAINT FK_PAA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES dbo.Users(UserID);
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrgAdminAssignments_OrganizationID' AND object_id = OBJECT_ID('dbo.OrgAdminAssignments'))
            CREATE INDEX IX_OrgAdminAssignments_OrganizationID ON OrgAdminAssignments(OrganizationID);");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PortfolioAdminAssignments_PortfolioID' AND object_id = OBJECT_ID('dbo.PortfolioAdminAssignments'))
            CREATE INDEX IX_PortfolioAdminAssignments_PortfolioID ON PortfolioAdminAssignments(PortfolioID);");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccessGroups')
        CREATE TABLE AccessGroups (
            AccessGroupID     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            GroupTypeCode     NVARCHAR(30)     NOT NULL,
            Name              NVARCHAR(200)    NOT NULL,
            Description       NVARCHAR(1000)   NULL,
            PortfolioID       INT              NULL,
            OrganizationID    INT              NULL,
            ParentPortfolioID INT              NULL,
            IsSystemManaged   BIT              NOT NULL DEFAULT 0,
            IsActive          BIT              NOT NULL DEFAULT 1,
            CreatedAt         DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            UpdatedAt         DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            CONSTRAINT PK_AccessGroups PRIMARY KEY (AccessGroupID),
            CONSTRAINT CK_AccessGroups_GroupType CHECK (GroupTypeCode IN ('PortfolioGroup', 'PortfolioSubGroup', 'OrgGroup')),
            CONSTRAINT CK_AccessGroups_PortfolioGroupShape CHECK (
                GroupTypeCode <> 'PortfolioGroup'
                OR (PortfolioID IS NOT NULL AND OrganizationID IS NULL AND ParentPortfolioID IS NULL)
            ),
            CONSTRAINT CK_AccessGroups_PortfolioSubGroupShape CHECK (
                GroupTypeCode <> 'PortfolioSubGroup'
                OR (PortfolioID IS NULL AND OrganizationID IS NOT NULL AND ParentPortfolioID IS NOT NULL)
            ),
            CONSTRAINT FK_AccessGroups_Portfolio FOREIGN KEY (PortfolioID) REFERENCES Portfolios(PortfolioID),
            CONSTRAINT FK_AccessGroups_ParentPortfolio FOREIGN KEY (ParentPortfolioID) REFERENCES Portfolios(PortfolioID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessGroups_GroupTypeCode' AND object_id = OBJECT_ID('dbo.AccessGroups'))
            CREATE INDEX IX_AccessGroups_GroupTypeCode ON AccessGroups(GroupTypeCode);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessGroups_PortfolioID' AND object_id = OBJECT_ID('dbo.AccessGroups'))
            CREATE INDEX IX_AccessGroups_PortfolioID ON AccessGroups(PortfolioID);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessGroups_OrganizationID' AND object_id = OBJECT_ID('dbo.AccessGroups'))
            CREATE INDEX IX_AccessGroups_OrganizationID ON AccessGroups(OrganizationID);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessGroups_ParentPortfolioID' AND object_id = OBJECT_ID('dbo.AccessGroups'))
            CREATE INDEX IX_AccessGroups_ParentPortfolioID ON AccessGroups(ParentPortfolioID);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_AccessGroups_GroupType_PortfolioID' AND object_id = OBJECT_ID('dbo.AccessGroups'))
            CREATE UNIQUE INDEX UX_AccessGroups_GroupType_PortfolioID ON AccessGroups(GroupTypeCode, PortfolioID) WHERE PortfolioID IS NOT NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_AccessGroups_GroupType_OrganizationID' AND object_id = OBJECT_ID('dbo.AccessGroups'))
            CREATE UNIQUE INDEX UX_AccessGroups_GroupType_OrganizationID ON AccessGroups(GroupTypeCode, OrganizationID) WHERE OrganizationID IS NOT NULL;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserAccessGroupAssignments')
        CREATE TABLE UserAccessGroupAssignments (
            AssignmentID     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
            UserID           UNIQUEIDENTIFIER NOT NULL,
            AccessGroupID    UNIQUEIDENTIFIER NOT NULL,
            AssignedAt       DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            AssignedByUserID UNIQUEIDENTIFIER NOT NULL,
            CONSTRAINT PK_UserAccessGroupAssignments PRIMARY KEY (AssignmentID),
            CONSTRAINT UQ_UAGA_UserGroup UNIQUE (UserID, AccessGroupID),
            CONSTRAINT FK_UAGA_User FOREIGN KEY (UserID) REFERENCES Users(UserID),
            CONSTRAINT FK_UAGA_AccessGroup FOREIGN KEY (AccessGroupID) REFERENCES AccessGroups(AccessGroupID),
            CONSTRAINT FK_UAGA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES Users(UserID)
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserAccessGroupAssignments_AccessGroupID' AND object_id = OBJECT_ID('dbo.UserAccessGroupAssignments'))
            CREATE INDEX IX_UserAccessGroupAssignments_AccessGroupID ON UserAccessGroupAssignments(AccessGroupID);");

    await db.Database.ExecuteSqlRawAsync(@"
        IF COL_LENGTH('dbo.Quizzes', 'IsPublished') IS NULL
        BEGIN
            ALTER TABLE dbo.Quizzes ADD IsPublished BIT NULL;
            EXEC('UPDATE dbo.Quizzes SET IsPublished = 1');
            ALTER TABLE dbo.Quizzes ALTER COLUMN IsPublished BIT NOT NULL;
            ALTER TABLE dbo.Quizzes ADD CONSTRAINT DF_Quizzes_IsPublished DEFAULT 0 FOR IsPublished;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF COL_LENGTH('dbo.QuizAttempts', 'IsTestData') IS NULL
        BEGIN
            ALTER TABLE dbo.QuizAttempts ADD IsTestData BIT NOT NULL
                CONSTRAINT DF_QuizAttempts_IsTestData DEFAULT 0;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF COL_LENGTH('dbo.QuizAttempts', 'RawScore') IS NULL
        BEGIN
            ALTER TABLE dbo.QuizAttempts ADD RawScore INT NULL;
        END;

        IF COL_LENGTH('dbo.QuizAttempts', 'TotalQuestions') IS NULL
        BEGIN
            ALTER TABLE dbo.QuizAttempts ADD TotalQuestions INT NULL;
        END;

        IF COL_LENGTH('dbo.QuizAttempts', 'CurrentQuestionPosition') IS NULL
        BEGIN
            ALTER TABLE dbo.QuizAttempts ADD CurrentQuestionPosition INT NULL;
        END;");

    await db.Database.ExecuteSqlRawAsync(@"
        IF COL_LENGTH('dbo.AnswerOptions', 'OptionKey') IS NULL
        BEGIN
            ALTER TABLE dbo.AnswerOptions ADD OptionKey NCHAR(1) NULL;
        END;

        IF COL_LENGTH('dbo.AnswerOptions', 'OptionKey') IS NOT NULL
        BEGIN
            UPDATE dbo.AnswerOptions
            SET OptionKey = NCHAR(64 + CASE WHEN DisplayOrder BETWEEN 1 AND 26 THEN DisplayOrder ELSE 26 END)
            WHERE OptionKey IS NULL
               OR LTRIM(RTRIM(OptionKey)) = ''
               OR LTRIM(RTRIM(OptionKey)) <> NCHAR(64 + CASE WHEN DisplayOrder BETWEEN 1 AND 26 THEN DisplayOrder ELSE 26 END);

            IF EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID('dbo.AnswerOptions')
                  AND name = 'OptionKey'
                  AND is_nullable = 1
            )
            BEGIN
                ALTER TABLE dbo.AnswerOptions ALTER COLUMN OptionKey NCHAR(1) NOT NULL;
            END;
        END;");
}
