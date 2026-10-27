using System.Globalization;
using LeadApp.Domain;
using Microsoft.AspNetCore.WebUtilities;

namespace LeadApp.Services.Leads;

/// <summary>Admin lead filters; shared by the grid and the Excel export endpoint via the query string.</summary>
public sealed record LeadFilter
{
    public string? Search { get; init; }
    public string? CampaignId { get; init; }
    public CrmSyncStatus? Status { get; init; }
    public CustomerType? CustomerType { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    public IEnumerable<Lead> Apply(IEnumerable<Lead> leads, AppClock clock)
    {
        var query = leads;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            query = query.Where(l =>
                l.FullName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (l.Email?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (l.PhoneNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                l.Number.ToString(CultureInfo.InvariantCulture) == s.TrimStart('#'));
        }

        if (CampaignId == "none")
            query = query.Where(l => l.CampaignId is null);
        else if (!string.IsNullOrEmpty(CampaignId))
            query = query.Where(l => l.CampaignId == CampaignId);

        if (Status is { } status)
            query = query.Where(l => l.CrmStatus == status);

        if (CustomerType is { } type)
            query = query.Where(l => l.CustomerType == type);

        if (From is { } from)
        {
            var fromUtc = clock.StartOfDayUtc(from);
            query = query.Where(l => l.CreatedAtUtc >= fromUtc);
        }

        if (To is { } to)
        {
            var toUtc = clock.StartOfDayUtc(to.AddDays(1));
            query = query.Where(l => l.CreatedAtUtc < toUtc);
        }

        return query;
    }

    public string ToQueryString()
    {
        var values = new Dictionary<string, string?>
        {
            ["q"] = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
            ["campaign"] = string.IsNullOrEmpty(CampaignId) ? null : CampaignId,
            ["status"] = Status?.ToString(),
            ["type"] = CustomerType?.ToString(),
            ["from"] = From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        return QueryHelpers.AddQueryString("", values.Where(kv => kv.Value is not null)!);
    }

    public static LeadFilter FromQuery(IQueryCollection query) => new()
    {
        Search = query["q"],
        CampaignId = query["campaign"],
        Status = Enum.TryParse<CrmSyncStatus>(query["status"], out var s) ? s : null,
        CustomerType = Enum.TryParse<CustomerType>(query["type"], out var t) ? t : null,
        From = DateOnly.TryParseExact(query["from"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null,
        To = DateOnly.TryParseExact(query["to"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e) ? e : null
    };
}
