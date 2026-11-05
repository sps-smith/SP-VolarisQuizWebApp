using Microsoft.Extensions.Diagnostics.HealthChecks;
using Volaris101.Infrastructure.Data;

namespace Volaris101.Web.Infrastructure;

/// <summary>
/// Pings the database by executing a lightweight SQL query.
/// Uses IDbContextFactory so it doesn't consume the request-scoped DbContext.
/// </summary>
public class DbContextHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DbContextHealthCheck(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Volaris101DbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}