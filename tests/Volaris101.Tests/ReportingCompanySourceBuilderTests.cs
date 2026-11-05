using Volaris101.Domain.Entities;
using Volaris101.Infrastructure.Repositories;

namespace Volaris101.Tests;

public class ReportingCompanySourceBuilderTests
{
    [Fact]
    public void BuildAvailableCompanies_IncludesPortfolioOrganizations_AndExcludesOnlyConfiguredIds()
    {
        VbuMasterListRecord[] vbuRows =
        [
            new VbuMasterListRecord { OrganizationId = 30000, ReportToId = 0, OrganizationName = "Omegro" },
            new VbuMasterListRecord { OrganizationId = 30001, ReportToId = 30000, OrganizationName = "Andy Nelson Portfolio" },
            new VbuMasterListRecord { OrganizationId = 30002, ReportToId = 30001, OrganizationName = "Roddy MacLennan Group" },
            new VbuMasterListRecord { OrganizationId = 30562, ReportToId = 30002, OrganizationName = "tlmNexus" },
            new VbuMasterListRecord { OrganizationId = 40000, ReportToId = 0, OrganizationName = "Portfolio B Root" },
            new VbuMasterListRecord { OrganizationId = 40001, ReportToId = 40000, OrganizationName = "Beta" },
            new VbuMasterListRecord { OrganizationId = 40002, ReportToId = 40001, OrganizationName = "Gamma" },
            new VbuMasterListRecord { OrganizationId = 30417, ReportToId = 30002, OrganizationName = "Hidden One" },
            new VbuMasterListRecord { OrganizationId = 30418, ReportToId = 40000, OrganizationName = "Hidden Two" },
            new VbuMasterListRecord { OrganizationId = 30419, ReportToId = 40000, OrganizationName = "Hidden Three" }
        ];
        Portfolio[] portfolios =
        [
            new Portfolio { PortfolioId = 30000, PortfolioName = "Omegro" },
            new Portfolio { PortfolioId = 40000, PortfolioName = "Portfolio B" }
        ];

        var groups = ReportingCompanySourceBuilder.BuildAvailableCompanies(vbuRows, portfolios);
        var omegro = Assert.Single(groups, group => group.PortfolioName == "Omegro");
        var portfolioB = Assert.Single(groups, group => group.PortfolioName == "Portfolio B");

        Assert.True(omegro.Companies.Single(company => company.OrganizationId == 30000).IsPortfolio);
        Assert.True(portfolioB.Companies.Single(company => company.OrganizationId == 40000).IsPortfolio);
        Assert.False(omegro.Companies.Single(company => company.OrganizationId == 30001).IsPortfolio);
        Assert.False(portfolioB.Companies.Single(company => company.OrganizationId == 40001).IsPortfolio);
        Assert.Equal([30000, 30001, 30002, 30562], omegro.Companies.Select(company => company.OrganizationId).OrderBy(id => id).ToArray());
        Assert.Equal([40000, 40001, 40002], portfolioB.Companies.Select(company => company.OrganizationId).OrderBy(id => id).ToArray());
        Assert.DoesNotContain(groups.SelectMany(group => group.Companies), company => company.OrganizationId is 30417 or 30418 or 30419);
    }

    [Fact]
    public void BuildVisibleChildrenMap_ExpandsSelectionDownward_AndSkipsExcludedNodes()
    {
        VbuMasterListRecord[] vbuRows =
        [
            new VbuMasterListRecord { OrganizationId = 30000, ReportToId = 0, OrganizationName = "Omegro" },
            new VbuMasterListRecord { OrganizationId = 30001, ReportToId = 30000, OrganizationName = "Andy Nelson Portfolio" },
            new VbuMasterListRecord { OrganizationId = 30002, ReportToId = 30001, OrganizationName = "Roddy MacLennan Group" },
            new VbuMasterListRecord { OrganizationId = 30562, ReportToId = 30002, OrganizationName = "tlmNexus" },
            new VbuMasterListRecord { OrganizationId = 30417, ReportToId = 30002, OrganizationName = "Hidden Parent" },
            new VbuMasterListRecord { OrganizationId = 40000, ReportToId = 0, OrganizationName = "Portfolio B Root" },
            new VbuMasterListRecord { OrganizationId = 40001, ReportToId = 40000, OrganizationName = "Beta" },
            new VbuMasterListRecord { OrganizationId = 40002, ReportToId = 40001, OrganizationName = "Gamma" }
        ];

        var childrenMap = ReportingCompanySourceBuilder.BuildVisibleChildrenMap(vbuRows, [30000, 30001, 30002, 30562, 40000, 40001, 40002]);

        Assert.Equal([30001], childrenMap[30000]);
        Assert.Equal([30002], childrenMap[30001]);
        Assert.Equal([30562], childrenMap[30002]);
        Assert.Equal([40001], childrenMap[40000]);
        Assert.Equal([40002], childrenMap[40001]);
        Assert.DoesNotContain(30562, childrenMap.Keys);
        Assert.DoesNotContain(30417, childrenMap.Keys);
    }
}
