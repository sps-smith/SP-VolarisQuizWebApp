using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Volaris101.Application.Dtos;
using Volaris101.Application.Interfaces;

namespace Volaris101.Web.Infrastructure;

public sealed class PeopleDirectoryApiOptions
{
    public string BaseUrl { get; set; } = "https://volarispeopledirectorywebapi-staging.azurewebsites.net/";
    public string Scope { get; set; } = "api://8180dcd8-3144-4f32-8f9b-64245ae94ac0/.default";
    public int TimeoutSeconds { get; set; } = 30;
}

public interface IAppOnlyTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}

public sealed class ManagedIdentityTokenProvider : IAppOnlyTokenProvider
{
    private readonly TokenCredential _credential;
    private readonly string _scope;

    public ManagedIdentityTokenProvider(IOptions<PeopleDirectoryApiOptions> options)
    {
        _credential = new ManagedIdentityCredential();
        _scope = options.Value.Scope?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_scope))
            throw new InvalidOperationException("People Directory API scope is not configured.");
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext([_scope]),
            ct);

        return token.Token;
    }
}

public sealed class PeopleDirectoryService : IPeopleDirectoryService
{
    private readonly HttpClient _httpClient;
    private readonly PeopleDirectoryApiOptions _options;
    private readonly IAppOnlyTokenProvider _tokenProvider;

    public PeopleDirectoryService(
        HttpClient httpClient,
        IOptions<PeopleDirectoryApiOptions> options,
        IAppOnlyTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _tokenProvider = tokenProvider;
    }

    public async Task<IReadOnlyList<PeopleDirectoryEmployeeDto>> GetEmployeesByCompaniesAsync(IReadOnlyList<string> companyNames, CancellationToken ct = default)
    {
        return await GetFilterRecordsAsync("VBU", companyNames, ct);
    }

    private async Task<IReadOnlyList<PeopleDirectoryEmployeeDto>> GetFilterRecordsAsync(
        string filterName,
        IReadOnlyList<string> filterValues,
        CancellationToken ct)
    {
        var normalizedValues = filterValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedValues.Length == 0)
            return Array.Empty<PeopleDirectoryEmployeeDto>();

        var payload = new[]
        {
            new PeopleDirectoryFilterRequest(filterName, normalizedValues)
        };

        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "GetFilterRecordsForEmployeeType")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var details = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
            throw new InvalidOperationException($"People Directory API request failed with HTTP {(int)response.StatusCode}: {details}");
        }

        var employees = await response.Content.ReadFromJsonAsync<List<PeopleDirectoryApiEmployee>>(cancellationToken: ct)
            ?? new List<PeopleDirectoryApiEmployee>();

        return employees
            .Select(employee => new PeopleDirectoryEmployeeDto(
                employee.EmployeeName?.Trim() ?? string.Empty,
                employee.Email?.Trim() ?? string.Empty,
                employee.Company?.Trim() ?? string.Empty,
                employee.Vbu?.Trim() ?? string.Empty,
                employee.EmployeeUserId,
                TryParseOrganizationId(employee.Attribute9),
                employee.GetManagerUserPrincipalName()))
            .Where(employee =>
                !string.IsNullOrWhiteSpace(employee.EmployeeName) ||
                employee.EmployeeUserId > 0 ||
                !string.IsNullOrWhiteSpace(employee.Email))
            .ToList();
    }

    private static int? TryParseOrganizationId(string? value) =>
        int.TryParse(value?.Trim(), out var parsed) && parsed > 0
            ? parsed
            : null;

    private sealed record PeopleDirectoryFilterRequest(string Name, IReadOnlyList<string> FilterValues);

    private sealed class PeopleDirectoryApiEmployee
    {
        [JsonPropertyName("employeeName")]
        public string? EmployeeName { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("company")]
        public string? Company { get; init; }

        [JsonPropertyName("vbu")]
        public string? Vbu { get; init; }

        [JsonPropertyName("employeeUserId")]
        public int EmployeeUserId { get; init; }

        [JsonPropertyName("attribute9")]
        public string? Attribute9 { get; init; }

        [JsonPropertyName("managerUserPrincipalName")]
        public string? ManagerUserPrincipalName { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalFields { get; init; }

        public string? GetManagerUserPrincipalName()
        {
            if (!string.IsNullOrWhiteSpace(ManagerUserPrincipalName))
                return ManagerUserPrincipalName.Trim();

            var alternate = AdditionalFields?
                .FirstOrDefault(field => NormalizeFieldName(field.Key) == "manageruserprincipalname")
                .Value;

            return alternate is { ValueKind: JsonValueKind.String }
                ? alternate.Value.GetString()?.Trim()
                : null;
        }

        private static string NormalizeFieldName(string fieldName) =>
            new string(fieldName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
