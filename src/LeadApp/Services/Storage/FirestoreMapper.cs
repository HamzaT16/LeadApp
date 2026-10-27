using System.Globalization;
using Google.Cloud.Firestore;
using LeadApp.Domain;

namespace LeadApp.Services.Storage;

/// <summary>
/// Explicit mapping between domain objects and Firestore documents, so the domain stays free of
/// Firestore attributes and the stored shape is obvious. Decimals are stored as doubles (amounts have 2 decimals).
/// </summary>
internal static class FirestoreMapper
{
    public static Dictionary<string, object?> ToMap(Lead l) => new()
    {
        ["number"] = l.Number,
        ["createdAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(l.CreatedAtUtc, DateTimeKind.Utc)),
        ["customerType"] = l.CustomerType.ToString(),
        ["fullName"] = l.FullName,
        ["phoneNumber"] = l.PhoneNumber,
        ["email"] = l.Email,
        ["cityId"] = l.CityId,
        ["localCommunityId"] = l.LocalCommunityId,
        ["requestedAmount"] = (double)l.RequestedAmount,
        ["purpose"] = l.Purpose.ToString(),
        ["sourceCode"] = l.SourceCode,
        ["campaignId"] = l.CampaignId,
        ["campaignUrlId"] = l.CampaignUrlId,
        ["campaignName"] = l.CampaignName,
        ["crmStatus"] = l.CrmStatus.ToString(),
        ["crmAttempts"] = l.CrmAttempts,
        ["crmLastAttemptAt"] = ToTimestamp(l.CrmLastAttemptUtc),
        ["crmSalesOpportunityId"] = l.CrmSalesOpportunityId,
        ["crmPhaseCode"] = l.CrmPhaseCode,
        ["crmCustomerId"] = l.CrmCustomerId,
        ["crmLeadId"] = l.CrmLeadId,
        ["crmMatchResult"] = l.CrmMatchResult,
        ["crmError"] = l.CrmError
    };

    public static Dictionary<string, object?> CrmFields(Lead l) =>
        ToMap(l).Where(kv => kv.Key.StartsWith("crm", StringComparison.Ordinal)).ToDictionary();

    public static Lead ToLead(DocumentSnapshot doc)
    {
        var d = doc.ToDictionary();
        return new Lead
        {
            Id = doc.Id,
            Number = Long(d, "number") ?? 0,
            CreatedAtUtc = Date(d, "createdAt") ?? DateTime.MinValue,
            CustomerType = Enum<CustomerType>(d, "customerType"),
            FullName = Str(d, "fullName") ?? "",
            PhoneNumber = Str(d, "phoneNumber"),
            Email = Str(d, "email"),
            CityId = Long(d, "cityId") ?? 0,
            LocalCommunityId = Long(d, "localCommunityId") ?? 0,
            RequestedAmount = Dec(d, "requestedAmount"),
            Purpose = Enum<LoanPurpose>(d, "purpose"),
            SourceCode = Str(d, "sourceCode") ?? "",
            CampaignId = Str(d, "campaignId"),
            CampaignUrlId = Str(d, "campaignUrlId"),
            CampaignName = Str(d, "campaignName"),
            CrmStatus = Enum<CrmSyncStatus>(d, "crmStatus"),
            CrmAttempts = (int)(Long(d, "crmAttempts") ?? 0),
            CrmLastAttemptUtc = Date(d, "crmLastAttemptAt"),
            CrmSalesOpportunityId = Long(d, "crmSalesOpportunityId"),
            CrmPhaseCode = Str(d, "crmPhaseCode"),
            CrmCustomerId = Long(d, "crmCustomerId"),
            CrmLeadId = Long(d, "crmLeadId"),
            CrmMatchResult = Str(d, "crmMatchResult"),
            CrmError = Str(d, "crmError")
        };
    }

    public static Dictionary<string, object?> ToMap(Campaign c, bool includeCounters)
    {
        var map = new Dictionary<string, object?>
        {
            ["name"] = c.Name,
            ["slug"] = c.Slug,
            ["product"] = c.Product?.ToString(),
            ["channel"] = c.Channel,
            ["status"] = c.Status.ToString(),
            ["startDate"] = c.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["endDate"] = c.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["description"] = c.Description,
            ["customerType"] = c.CustomerType.ToString(),
            ["targetGroupDescription"] = c.TargetGroupDescription,
            ["channelCode"] = c.ChannelCode,
            ["cost"] = (double)c.Cost,
            ["createdByEmployeeId"] = c.CreatedByEmployeeId,
            ["endTimeLimit"] = c.EndTimeLimit?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["crmStatus"] = c.CrmStatus.ToString(),
            ["crmCampaignId"] = c.CrmCampaignId,
            ["crmStatusCode"] = c.CrmStatusCode,
            ["crmError"] = c.CrmError,
            ["crmLastAttemptAt"] = ToTimestamp(c.CrmLastAttemptUtc),
        };
        if (includeCounters)
        {
            map["viewsCount"] = c.ViewsCount;
            map["leadsCount"] = c.LeadsCount;
            map["createdAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(c.CreatedAtUtc, DateTimeKind.Utc));
        }
        return map;
    }

    public static Campaign ToCampaign(DocumentSnapshot doc)
    {
        var d = doc.ToDictionary();
        var product = Str(d, "product");
        return new Campaign
        {
            Id = doc.Id,
            Name = Str(d, "name") ?? "",
            Slug = Str(d, "slug") ?? "",
            Product = System.Enum.TryParse<LoanPurpose>(product, out var p) ? p : null,
            Channel = Str(d, "channel") ?? SourceCodes.Email,
            Status = Enum<CampaignStatus>(d, "status"),
            StartDate = DateOnly.ParseExact(Str(d, "startDate") ?? "2000-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = DateOnly.ParseExact(Str(d, "endDate") ?? "2000-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ViewsCount = Long(d, "viewsCount") ?? 0,
            LeadsCount = Long(d, "leadsCount") ?? 0,
            CreatedAtUtc = Date(d, "createdAt") ?? DateTime.MinValue,
            Description = Str(d, "description") ?? "",
            CustomerType = Enum<CustomerType>(d, "customerType"),
            TargetGroupDescription = Str(d, "targetGroupDescription") ?? "",
            ChannelCode = Str(d, "channelCode") ?? OnlineChannelCodes.Viber,
            Cost = Dec(d, "cost"),
            CreatedByEmployeeId = Long(d, "createdByEmployeeId"),
            EndTimeLimit = DateOnly.TryParseExact(Str(d, "endTimeLimit"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var limit) ? limit : null,
            CrmStatus = Enum<CrmSyncStatus>(d, "crmStatus"),
            CrmCampaignId = Long(d, "crmCampaignId"),
            CrmStatusCode = Str(d, "crmStatusCode"),
            CrmError = Str(d, "crmError"),
            CrmLastAttemptUtc = Date(d, "crmLastAttemptAt")
        };
    }

    public static Dictionary<string, object?> ToMap(AdminUser u) => new()
    {
        ["email"] = u.Email,
        ["passwordHash"] = u.PasswordHash,
        ["failedAttempts"] = u.FailedAttempts,
        ["lockoutEnd"] = ToTimestamp(u.LockoutEndUtc),
        ["lastLogin"] = ToTimestamp(u.LastLoginUtc)
    };

    public static AdminUser ToAdmin(DocumentSnapshot doc)
    {
        var d = doc.ToDictionary();
        return new AdminUser
        {
            Email = Str(d, "email") ?? doc.Id,
            PasswordHash = Str(d, "passwordHash") ?? "",
            FailedAttempts = (int)(Long(d, "failedAttempts") ?? 0),
            LockoutEndUtc = Date(d, "lockoutEnd"),
            LastLoginUtc = Date(d, "lastLogin")
        };
    }

    private static Timestamp? ToTimestamp(DateTime? utc) =>
        utc is { } v ? Timestamp.FromDateTime(DateTime.SpecifyKind(v, DateTimeKind.Utc)) : null;

    private static string? Str(Dictionary<string, object> d, string key) =>
        d.TryGetValue(key, out var v) ? v as string : null;

    private static long? Long(Dictionary<string, object> d, string key) =>
        d.TryGetValue(key, out var v) ? v switch { long l => l, double x => (long)x, _ => null } : null;

    private static decimal Dec(Dictionary<string, object> d, string key) =>
        d.TryGetValue(key, out var v) ? v switch { double x => (decimal)x, long l => l, _ => 0m } : 0m;

    private static DateTime? Date(Dictionary<string, object> d, string key) =>
        d.TryGetValue(key, out var v) && v is Timestamp t ? t.ToDateTime() : null;

    private static T Enum<T>(Dictionary<string, object> d, string key) where T : struct, System.Enum =>
        System.Enum.TryParse<T>(Str(d, key), out var value) ? value : default;
}
