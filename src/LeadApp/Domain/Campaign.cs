namespace LeadApp.Domain;

public sealed class Campaign
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>URL identifier used in the public link (/k/{slug}) and sent to the CRM as <c>urlId</c>.</summary>
    public string Slug { get; set; } = "";

    /// <summary>Locked product for the campaign; <c>null</c> lets the client choose.</summary>
    public LoanPurpose? Product { get; set; }

    /// <summary>Sent to the CRM as <c>sourceCode</c>; one of <see cref="SourceCodes"/>.</summary>
    public string Channel { get; set; } = SourceCodes.Viber;

    public CampaignStatus Status { get; set; } = CampaignStatus.Published;

    // ---- fields sent to the CRM (spec section 6, POST /api/campaign/online) ----
    public string Description { get; set; } = "";
    public CustomerType CustomerType { get; set; } = CustomerType.FL;
    public string TargetGroupDescription { get; set; } = "";

    /// <summary>Promotion channel; one of <see cref="OnlineChannelCodes"/>.</summary>
    public string ChannelCode { get; set; } = OnlineChannelCodes.Viber;

    /// <summary>Campaign cost in EUR (spec).</summary>
    public decimal Cost { get; set; }

    /// <summary>ROW_ID from employee.csv of the employee recorded as the campaign creator.</summary>
    public long? CreatedByEmployeeId { get; set; }

    /// <summary>Last day sales count toward the campaign; <c>null</c> = CRM uses the end date.</summary>
    public DateOnly? EndTimeLimit { get; set; }

    // ---- CRM sync result ----
    public CrmSyncStatus CrmStatus { get; set; } = CrmSyncStatus.Pending;
    public long? CrmCampaignId { get; set; }
    public string? CrmStatusCode { get; set; }
    public string? CrmError { get; set; }
    public DateTime? CrmLastAttemptUtc { get; set; }

    /// <summary>Once in the CRM the campaign cannot be changed there (the spec has no update endpoint).</summary>
    public bool IsInCrm => CrmStatus == CrmSyncStatus.Synced;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public long ViewsCount { get; set; }
    public long LeadsCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public bool IsLive(DateOnly today) =>
        Status == CampaignStatus.Published && today >= StartDate && today <= EndDate;

    public double ConversionRate => ViewsCount > 0 ? (double)LeadsCount / ViewsCount : 0;

    public Campaign Clone() => (Campaign)MemberwiseClone();
}
