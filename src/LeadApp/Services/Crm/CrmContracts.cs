using System.Text.Json;
using System.Text.Json.Serialization;
using LeadApp.Domain;

namespace LeadApp.Services.Crm;

/// <summary>Body of POST /api/sales-opportunity/web/create (spec section 1).</summary>
public sealed record CreateSalesOpportunityRequest
{
    public required string FullName { get; init; }
    public required string CustomerTypeCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhoneNumber { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    public required long CityId { get; init; }
    public required long LocalCommunityId { get; init; }
    public required string SourceCode { get; init; }
    public required decimal RequestedAmount { get; init; }

    /// <summary>Online campaign identifier; omitted when the lead is not tied to a campaign.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UrlId { get; init; }

    public static CreateSalesOpportunityRequest FromLead(Lead lead) => new()
    {
        FullName = lead.FullName,
        CustomerTypeCode = lead.CustomerType.ToString(),
        PhoneNumber = string.IsNullOrWhiteSpace(lead.PhoneNumber) ? null : lead.PhoneNumber,
        Email = string.IsNullOrWhiteSpace(lead.Email) ? null : lead.Email,
        CityId = lead.CityId,
        LocalCommunityId = lead.LocalCommunityId,
        SourceCode = lead.SourceCode,
        RequestedAmount = decimal.Round(lead.RequestedAmount, 2),
        UrlId = string.IsNullOrWhiteSpace(lead.CampaignUrlId) ? null : lead.CampaignUrlId
    };
}

/// <summary>201 Created response.</summary>
public sealed record CreateSalesOpportunityResponse(
    long SalesOpportunityId,
    string PhaseCode,
    long? CustomerId,
    long? LeadId,
    string MatchResult);

public sealed record CrmValidationError(string Field, string Message);

/// <summary>Common CRM error model.</summary>
public sealed record CrmError(
    string ErrorType,
    string Message,
    IReadOnlyList<CrmValidationError>? ValidationErrors,
    string? Timestamp);

public sealed record CrmResult(CreateSalesOpportunityResponse? Response, string? Error)
{
    public bool Success => Response is not null;

    public static CrmResult Ok(CreateSalesOpportunityResponse response) => new(response, null);
    public static CrmResult Fail(string error) => new(null, error);
}

public static class CrmJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
