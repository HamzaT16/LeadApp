using System.Net;
using System.Text.Json.Serialization;
using LeadApp.Domain;

namespace LeadApp.Services.Crm;

/// <summary>Body of POST /api/campaign/online (spec section 6).</summary>
public sealed record CreateOnlineCampaignRequest
{
    public static readonly TimeOnly StartOfDay = TimeOnly.MinValue;
    public static readonly TimeOnly EndOfDay = new(23, 59, 59);

    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string CustomerTypeCode { get; init; }
    public required string TargetGroupDescription { get; init; }
    public required string ChannelCode { get; init; }
    public required string UrlId { get; init; }

    /// <summary>ISO 8601 with offset, e.g. 2026-10-01T00:00:00+02:00.</summary>
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Omitted when not set; the CRM then uses <see cref="EndTime"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? EndTimeLimit { get; init; }

    public required decimal Cost { get; init; }
    public required long CreatedByEmployeeId { get; init; }

    /// <summary>
    /// Campaign dates are whole local days (Europe/Sarajevo): start at 00:00:00 of the start date,
    /// end and endTimeLimit at 23:59:59 of their dates.
    /// </summary>
    public static CreateOnlineCampaignRequest FromCampaign(Campaign c) => new()
    {
        Name = c.Name,
        Description = c.Description,
        CustomerTypeCode = c.CustomerType.ToString(),
        TargetGroupDescription = c.TargetGroupDescription,
        ChannelCode = c.ChannelCode,
        UrlId = c.Slug,
        StartTime = AppClock.AtLocal(c.StartDate, StartOfDay),
        EndTime = AppClock.AtLocal(c.EndDate, EndOfDay),
        EndTimeLimit = c.EndTimeLimit is { } limit ? AppClock.AtLocal(limit, EndOfDay) : null,
        Cost = decimal.Round(c.Cost, 2),
        CreatedByEmployeeId = c.CreatedByEmployeeId ?? 0
    };
}

/// <summary>201 Created response.</summary>
public sealed record CreateOnlineCampaignResponse(
    long CampaignId,
    string UrlId,
    string CategoryCode,
    string TypeCode,
    string StatusCode,
    DateTimeOffset? EndTimeLimit);

public sealed record CrmCampaignResult(CreateOnlineCampaignResponse? Response, string? Error, HttpStatusCode? Status = null)
{
    public bool Success => Response is not null;

    /// <summary>409: the urlId is already used by another online campaign in the CRM.</summary>
    public bool UrlIdConflict => Status == HttpStatusCode.Conflict;

    /// <summary>404: createdByEmployeeId does not exist in the CRM.</summary>
    public bool EmployeeNotFound => Status == HttpStatusCode.NotFound;

    public static CrmCampaignResult Ok(CreateOnlineCampaignResponse response) => new(response, null, HttpStatusCode.Created);
    public static CrmCampaignResult Fail(string error, HttpStatusCode? status = null) => new(null, error, status);
}
