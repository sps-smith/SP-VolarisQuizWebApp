using Volaris101.Application.Dtos;
using Volaris101.Domain.Entities;

namespace Volaris101.Infrastructure.Repositories;

public static class ReportingCompanySourceBuilder
{
    private static readonly HashSet<int> ExcludedOrganizationIds = new()
    {
        30417,
        30418,
        30419
    };

    public static bool IsExcludedOrganizationId(int organizationId) =>
        ExcludedOrganizationIds.Contains(organizationId);

    public static IReadOnlyList<CompanyGroupDto> BuildAvailableCompanies(
        IReadOnlyCollection<VbuMasterListRecord> vbuRows,
        IReadOnlyCollection<Portfolio> portfolios,
        IReadOnlyCollection<int>? scopeOrganizationIds = null)
    {
        var scopedOrganizationIds = scopeOrganizationIds is null
            ? null
            : scopeOrganizationIds.Where(id => id > 0).ToHashSet();

        if (scopedOrganizationIds is { Count: 0 })
            return Array.Empty<CompanyGroupDto>();

        var portfolioById = portfolios
            .Where(p => p.PortfolioId > 0)
            .GroupBy(p => p.PortfolioId)
            .ToDictionary(g => g.Key, g => g.First().PortfolioName);

        var portfolioIds = portfolioById.Keys.ToHashSet();
        var rowsByOrganizationId = vbuRows
            .Where(row => row.OrganizationId > 0)
            .GroupBy(row => row.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());

        var visibleOrganizations = rowsByOrganizationId.Values
            .Where(row => !IsExcludedOrganizationId(row.OrganizationId))
            .Where(row => scopedOrganizationIds is null || scopedOrganizationIds.Contains(row.OrganizationId))
            .Select(row => new
            {
                row.OrganizationId,
                CompanyName = row.OrganizationName.Trim(),
                PortfolioId = ResolvePortfolioId(row.OrganizationId, rowsByOrganizationId, portfolioIds)
            })
            .ToList();

        return visibleOrganizations
            .GroupBy(row => row.PortfolioId)
            .Select(group => new CompanyGroupDto(
                GetPortfolioName(group.Key, portfolioById),
                group
                    .GroupBy(row => row.OrganizationId)
                    .Select(orgGroup =>
                    {
                        var companyName = orgGroup
                            .Select(row => row.CompanyName)
                            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                            ?? $"Organization {orgGroup.Key}";
                        return new CompanyOptionDto(orgGroup.Key, companyName, portfolioIds.Contains(orgGroup.Key));
                    })
                    .OrderBy(company => company.CompanyName)
                    .ThenBy(company => company.OrganizationId)
                    .ToList()))
            .OrderBy(group => group.PortfolioName)
            .ToList();
    }

    public static IReadOnlyDictionary<int, IReadOnlyList<int>> BuildVisibleChildrenMap(
        IReadOnlyCollection<VbuMasterListRecord> vbuRows,
        IReadOnlyCollection<int> visibleOrganizationIds)
    {
        var visibleIds = visibleOrganizationIds
            .Where(id => id > 0 && !IsExcludedOrganizationId(id))
            .ToHashSet();

        if (visibleIds.Count == 0)
            return new Dictionary<int, IReadOnlyList<int>>();

        var rowsByOrganizationId = vbuRows
            .Where(row => row.OrganizationId > 0)
            .GroupBy(row => row.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());

        return visibleIds
            .Where(rowsByOrganizationId.ContainsKey)
            .Select(organizationId => new
            {
                OrganizationId = organizationId,
                ParentOrganizationId = ResolveVisibleParentOrganizationId(organizationId, rowsByOrganizationId, visibleIds)
            })
            .Where(item => item.ParentOrganizationId.HasValue && item.ParentOrganizationId.Value != item.OrganizationId)
            .GroupBy(item => item.ParentOrganizationId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group
                    .Select(item => item.OrganizationId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray());
    }

    private static string GetPortfolioName(int? portfolioId, IReadOnlyDictionary<int, string> portfolioById) =>
        portfolioId.HasValue && portfolioById.TryGetValue(portfolioId.Value, out var portfolioName)
            ? portfolioName
            : "Unassigned";

    private static int? ResolvePortfolioId(
        int organizationId,
        IReadOnlyDictionary<int, VbuMasterListRecord> rowsByOrganizationId,
        HashSet<int> portfolioIds)
    {
        if (portfolioIds.Contains(organizationId))
            return organizationId;

        var seen = new HashSet<int>();
        var currentId = organizationId;

        while (rowsByOrganizationId.TryGetValue(currentId, out var row))
        {
            if (!seen.Add(currentId))
                return null;

            var parentId = row.ReportToId;
            if (parentId <= 0)
                return null;

            if (IsExcludedOrganizationId(parentId))
            {
                currentId = parentId;
                continue;
            }

            if (portfolioIds.Contains(parentId))
                return parentId;

            currentId = parentId;
        }

        return null;
    }

    private static int? ResolveVisibleParentOrganizationId(
        int organizationId,
        IReadOnlyDictionary<int, VbuMasterListRecord> rowsByOrganizationId,
        HashSet<int> visibleOrganizationIds)
    {
        var seen = new HashSet<int>();
        var currentId = organizationId;

        while (rowsByOrganizationId.TryGetValue(currentId, out var row))
        {
            if (!seen.Add(currentId))
                return null;

            var parentId = row.ReportToId;
            if (parentId <= 0)
                return null;

            if (IsExcludedOrganizationId(parentId))
            {
                currentId = parentId;
                continue;
            }

            if (visibleOrganizationIds.Contains(parentId))
                return parentId;

            currentId = parentId;
        }

        return null;
    }
}
