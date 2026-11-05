using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Options;
using Volaris101.Web.Infrastructure;

namespace Volaris101.Tests;

public sealed class PeopleDirectoryServiceTests
{
    [Fact]
    public async Task GetEmployeesByCompaniesAsync_MapsManagerUserPrincipalName_WhenApiUsesUnderscores()
    {
        var handler = new CapturingHttpMessageHandler
        {
            ResponseJson = """
                [
                  {
                    "employeeName": "Contractor User",
                    "email": "contractor@example.com",
                    "company": "Aion",
                    "vbu": "Aion",
                    "employeeUserId": 123,
                    "attribute9": "30505",
                    "Manager_User_Principal_Name": "Contractor - ourVolaris Disallowed"
                  }
                ]
                """
        };
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://directory.example/")
        };
        var service = new PeopleDirectoryService(
            httpClient,
            Options.Create(new PeopleDirectoryApiOptions
            {
                BaseUrl = "https://directory.example/"
            }),
            new FixedTokenProvider("test-access-token"));

        var result = await service.GetEmployeesByCompaniesAsync(["Aion"]);

        Assert.Equal(
            "Contractor - ourVolaris Disallowed",
            Assert.Single(result).ManagerUserPrincipalName);
        Assert.Equal("https://directory.example/GetFilterRecordsForEmployeeType", handler.RequestUri);
        Assert.Equal("Bearer test-access-token", handler.AuthorizationHeader);
        Assert.Contains("\"name\":\"VBU\"", handler.RequestBody);
        Assert.Contains("\"filterValues\":[\"Aion\"]", handler.RequestBody);
    }

    [Fact]
    public async Task GetEmployeesByCompaniesAsync_UsesAppOnlyToken_WithoutLegacyCredentials()
    {
        var handler = new CapturingHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://volarispeopledirectorywebapi-staging.azurewebsites.net/")
        };
        var tokenProvider = new FixedTokenProvider("app-only-token");
        var service = new PeopleDirectoryService(
            httpClient,
            Options.Create(new PeopleDirectoryApiOptions
            {
                BaseUrl = "https://volarispeopledirectorywebapi-staging.azurewebsites.net/",
                Scope = "api://8180dcd8-3144-4f32-8f9b-64245ae94ac0/.default"
            }),
            tokenProvider);

        await service.GetEmployeesByCompaniesAsync(["Aion"]);

        Assert.Equal(1, tokenProvider.CallCount);
        Assert.DoesNotContain("UserName", handler.RequestUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", handler.RequestUri, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Bearer app-only-token", handler.AuthorizationHeader);
    }

    [Fact]
    public async Task GetEmployeesByCompaniesAsync_DoesNotRequestToken_WhenCompanyListIsEmpty()
    {
        var handler = new CapturingHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://directory.example/")
        };
        var tokenProvider = new FixedTokenProvider("unused");
        var service = new PeopleDirectoryService(
            httpClient,
            Options.Create(new PeopleDirectoryApiOptions()),
            tokenProvider);

        var result = await service.GetEmployeesByCompaniesAsync([]);

        Assert.Empty(result);
        Assert.Equal(0, tokenProvider.CallCount);
        Assert.Null(handler.RequestUri);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string ResponseJson { get; init; } = "[]";
        public string? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;
        public string? AuthorizationHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            AuthorizationHeader = request.Headers.Authorization?.ToString();
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FixedTokenProvider(string token) : IAppOnlyTokenProvider
    {
        public int CallCount { get; private set; }

        public Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(token);
        }
    }
}
