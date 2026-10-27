using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeadApp.Options;
using Microsoft.Extensions.Options;

namespace LeadApp.Services.Crm;

public interface ICrmClient
{
    Task<CrmResult> CreateSalesOpportunityAsync(CreateSalesOpportunityRequest request, CancellationToken ct = default);
    Task<CrmCampaignResult> CreateOnlineCampaignAsync(CreateOnlineCampaignRequest request, CancellationToken ct = default);
}

/// <summary>
/// Typed HttpClient for the partner CRM. Requests are never retried automatically:
/// a retried POST could create a duplicate sales opportunity or campaign.
/// </summary>
public sealed class CrmClient(HttpClient http, IOptions<CrmOptions> options, ILogger<CrmClient> logger) : ICrmClient
{
    public const string ApiKeyHeader = "X-PARTNER-API-KEY";

    public async Task<CrmResult> CreateSalesOpportunityAsync(CreateSalesOpportunityRequest request, CancellationToken ct = default)
    {
        var (body, error, _) = await PostAsync<CreateSalesOpportunityRequest, CreateSalesOpportunityResponse>(
            options.Value.CreateOpportunityPath, request, ct);
        return body is not null ? CrmResult.Ok(body) : CrmResult.Fail(error!);
    }

    public async Task<CrmCampaignResult> CreateOnlineCampaignAsync(CreateOnlineCampaignRequest request, CancellationToken ct = default)
    {
        var (body, error, status) = await PostAsync<CreateOnlineCampaignRequest, CreateOnlineCampaignResponse>(
            options.Value.CreateCampaignPath, request, ct);
        return body is not null ? CrmCampaignResult.Ok(body) : CrmCampaignResult.Fail(error!, status);
    }

    /// <summary>Posts JSON and expects 201 Created; any other outcome is returned as a readable error.</summary>
    private async Task<(TResponse? Body, string? Error, HttpStatusCode? Status)> PostAsync<TRequest, TResponse>(
        string path, TRequest request, CancellationToken ct) where TResponse : class
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey) || http.BaseAddress is null)
            return (null, "CRM nije konfigurisan (BaseUrl/ApiKey).", null);

        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: CrmJson.Options)
        };
        message.Headers.TryAddWithoutValidation(ApiKeyHeader, $"App {opts.ApiKey}");

        try
        {
            using var response = await http.SendAsync(message, ct);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                var body = await response.Content.ReadFromJsonAsync<TResponse>(CrmJson.Options, ct);
                return body is null
                    ? (null, "CRM je vratio prazan odgovor.", response.StatusCode)
                    : (body, null, response.StatusCode);
            }

            var error = await ReadErrorAsync(response, ct);
            logger.LogWarning("CRM rejected {Path}: {Status} {Error}", path, (int)response.StatusCode, error);
            return (null, error, response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "CRM call {Path} failed", path);
            return (null, ex is TaskCanceledException ? "CRM nije odgovorio na vrijeme." : $"CRM nedostupan: {ex.Message}", null);
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        try
        {
            var error = await response.Content.ReadFromJsonAsync<CrmError>(CrmJson.Options, ct);
            if (error is null)
                return status;

            var details = error.ValidationErrors is { Count: > 0 } errors
                ? " — " + string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"))
                : "";
            return $"{status}: {error.ErrorType} — {error.Message}{details}";
        }
        catch (JsonException)
        {
            return status;
        }
    }
}

/// <summary>Development stand-in that always succeeds.</summary>
public sealed class FakeCrmClient(TimeProvider time, ILogger<FakeCrmClient> logger) : ICrmClient
{
    private long _next = 800;
    private long _nextCampaign = 50;

    public Task<CrmResult> CreateSalesOpportunityAsync(CreateSalesOpportunityRequest request, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _next);
        logger.LogInformation("Fake CRM received {@Request}", request);
        return Task.FromResult(CrmResult.Ok(new CreateSalesOpportunityResponse(id, "CREATED", null, id - 500, "LEAD_CREATED")));
    }

    public Task<CrmCampaignResult> CreateOnlineCampaignAsync(CreateOnlineCampaignRequest request, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextCampaign);
        logger.LogInformation("Fake CRM received campaign {@Request}", request);
        var status = request.StartTime > time.GetUtcNow() ? "DRAFT" : "ACTIVE";
        return Task.FromResult(CrmCampaignResult.Ok(new CreateOnlineCampaignResponse(
            id, request.UrlId, "SALES", "ONLINE", status, request.EndTimeLimit ?? request.EndTime)));
    }
}
