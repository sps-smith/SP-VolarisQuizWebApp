# Volaris 101 — ASP.NET Core Web Application

ASP.NET Core 8 MVC application for Azure App Services, matching the v2.1 prototype.

## Project structure

```
Volaris101.sln
└── src/
    ├── Volaris101.Domain/          — Entities (User, Quiz, Question, Attempt, etc.)
    ├── Volaris101.Application/     — DTOs, service interfaces, service implementations
    │   ├── Dtos/Dtos.cs
    │   ├── Interfaces/IServices.cs
    │   ├── Interfaces/IRepositories.cs
    │   └── Services/
    │       ├── LearnerService.cs
    │       ├── AdminServices.cs    (ReportingService + UserAdminService)
    │       └── CmsService.cs
    ├── Volaris101.Infrastructure/  — EF Core DbContext + repositories (Azure SQL)
    │   ├── Data/Volaris101DbContext.cs
    │   └── Repositories/Repositories.cs
    └── Volaris101.Web/             — MVC controllers + Razor views + static assets
        ├── Auth/OktaJitService.cs  — JIT provisioning on Okta sign-in
        ├── Middleware/
        ├── Controllers/
        │   ├── LearnerController.cs
        │   ├── ReportingController.cs
        │   └── AdminController.cs
        ├── Views/
        │   ├── Shared/_Layout.cshtml
        │   ├── Learner/Dashboard.cshtml
        │   ├── Learner/Quiz.cshtml
        │   ├── Reporting/Reporting.cshtml
        │   └── Admin/Cms.cshtml
        ├── wwwroot/css/site.css
        ├── wwwroot/js/site.js
        ├── appsettings.json
        └── Program.cs
```

## Pages

| URL | Controller | Description |
|-----|-----------|-------------|
| `/` | LearnerController.Dashboard | My Learning dashboard — module grid + progress |
| `/learner/quiz/start/{quizId}` | LearnerController.StartQuiz | Begins a quiz attempt |
| `/reporting` | ReportingController.Overview | Admin: Overview tab |
| `/reporting/users` | ReportingController.Users | Admin: User Activity tab |
| `/reporting/export` | ReportingController.Export | Admin: Reporting/export tab |
| `/admin` | AdminController.Cms | Super Admin: CMS — By Quiz tab |
| `/admin/cms/questions` | AdminController.AllQuestions | Super Admin: All questions tab |
| `/admin/users` | AdminController.Users | Super Admin: Roles & users tab |
| `/auth/login` | AuthController | Redirects to Okta OIDC |
| `/auth/logout` | AuthController | Signs out of cookie + Okta |

## Role access

| Role | Access |
|------|--------|
| Learner | `/` quiz flow only |
| OrgAdmin | + `/reporting` |
| PortfolioAdmin | + `/reporting` (cross-portfolio) |
| GlobalAdmin | + `/reporting` + `/admin` |

## Prerequisites

- .NET 8 SDK
- Azure SQL Database (schema in `volaris101_database_schema.sql`)
- Okta OIDC application (Authorization Code flow)
- Azure Key Vault (production)
- Azure App Service (Linux or Windows)

## Local setup

1. Clone the repository
2. Create `src/Volaris101.Web/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "AzureSQL": "Server=...;Database=Volaris101Dev;..."
     },
     "Okta": {
       "Authority": "https://<your-okta-domain>/oauth2/default",
       "ClientId": "<your-client-id>",
       "ClientSecret": "<your-client-secret>"
     }
   }
   ```
3. Apply the database schema:
   ```bash
   sqlcmd -S <server> -d Volaris101 -i volaris101_database_schema.sql
   ```
4. Run the application:
   ```bash
   cd src/Volaris101.Web
   dotnet run
   ```

## Test coverage

Run the coverage scripts from the repo root:

```powershell
.\Test-Coverage.ps1
.\Update-Coverage.ps1
```

Coverage output is written to `artifacts/coverage/coverage/coverage.cobertura.xml`.

## EF Core migrations

```bash
# From the solution root
dotnet ef migrations add InitialCreate --project src/Volaris101.Infrastructure --startup-project src/Volaris101.Web
dotnet ef database update --project src/Volaris101.Infrastructure --startup-project src/Volaris101.Web
```

## Azure App Service deployment

### Option A — GitHub Actions (recommended)
1. In Azure Portal → App Service → Deployment Center → GitHub
2. Select your repo; Azure generates `.github/workflows/azure-webapps-dotnet-core.yml`

### Option B — Azure CLI
```bash
az webapp up \
  --name volaris101 \
  --resource-group volaris-rg \
  --runtime "DOTNET:8" \
  --sku B1
```

### Required App Service configuration

Set these in **Configuration → Application settings**:

| Name | Value |
|------|-------|
| `ConnectionStrings__AzureSQL` | Azure SQL connection string |
| `KeyVault__Uri` | `https://<your-keyvault>.vault.azure.net/` |
| `ApplicationInsights__ConnectionString` | App Insights connection string |

**Key Vault secrets** (all other sensitive values):

| Secret name | Maps to |
|-------------|---------|
| `Okta--ClientSecret` | `Okta:ClientSecret` |
| `Okta--Authority` | `Okta:Authority` |
| `Okta--ClientId` | `Okta:ClientId` |

### Managed Identity

Enable System-Assigned Managed Identity on the App Service and grant it:
- **Key Vault Secrets User** role on the Key Vault
- **Azure AD** authentication on the Azure SQL server (Entra-only mode recommended)

### Okta OIDC setup

1. Create an Okta OIDC app (Web, Authorization Code)
2. Sign-in redirect URI: `https://<your-app>.azurewebsites.net/signin-oidc`
3. Sign-out redirect URI: `https://<your-app>.azurewebsites.net/signout-callback-oidc`
4. Required SAML/OIDC claims:
   - `sub` — employee number (used as canonical login)
   - `email`
   - `given_name`, `family_name`
   - `employeeNumber` (mapped to `employeeId`)
   - `organization`
   - `customPortfolio` (mapped to `portfolio`)
