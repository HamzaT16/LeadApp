namespace LeadApp.Domain;

public sealed class Lead
{
    public string Id { get; set; } = "";
    public long Number { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public CustomerType CustomerType { get; set; }
    public string FullName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public long CityId { get; set; }
    public long LocalCommunityId { get; set; }
    public decimal RequestedAmount { get; set; }
    public LoanPurpose Purpose { get; set; }
    public string SourceCode { get; set; } = "";

    public string? CampaignId { get; set; }
    public string? CampaignUrlId { get; set; }
    public string? CampaignName { get; set; }

    public CrmSyncStatus CrmStatus { get; set; } = CrmSyncStatus.Pending;
    public int CrmAttempts { get; set; }
    public DateTime? CrmLastAttemptUtc { get; set; }
    public long? CrmSalesOpportunityId { get; set; }
    public string? CrmPhaseCode { get; set; }
    public long? CrmCustomerId { get; set; }
    public long? CrmLeadId { get; set; }
    public string? CrmMatchResult { get; set; }
    public string? CrmError { get; set; }

    public Lead Clone() => (Lead)MemberwiseClone();
}
