namespace LeadApp.Domain;

/// <summary>CRM customer type code (spec: "Tip CRM klijenta").</summary>
public enum CustomerType
{
    FL, // Fizičko lice
    PL  // Pravno lice
}

/// <summary>Loan product the client is interested in. Stored locally only, not sent to the CRM.</summary>
public enum LoanPurpose
{
    Stanovnistvo,
    Poljoprivreda,
    Biznis,
    ZeleniKrediti
}

public enum CrmSyncStatus
{
    Pending,
    Synced,
    Failed
}

public enum CampaignStatus
{
    Published,
    Inactive
}

public static class DisplayNames
{
    public static string Of(CustomerType type) => type switch
    {
        CustomerType.FL => "Fizičko lice",
        CustomerType.PL => "Pravno lice",
        _ => type.ToString()
    };

    public static string Of(LoanPurpose purpose) => purpose switch
    {
        LoanPurpose.Stanovnistvo => "Stanovništvo",
        LoanPurpose.Poljoprivreda => "Poljoprivreda",
        LoanPurpose.Biznis => "Biznis",
        LoanPurpose.ZeleniKrediti => "Zeleni krediti",
        _ => purpose.ToString()
    };

    public static string Of(CrmSyncStatus status) => status switch
    {
        CrmSyncStatus.Pending => "Na čekanju",
        CrmSyncStatus.Synced => "Poslano u CRM",
        CrmSyncStatus.Failed => "Greška",
        _ => status.ToString()
    };

    public static string Of(CampaignStatus status) => status switch
    {
        CampaignStatus.Published => "Objavljena",
        CampaignStatus.Inactive => "Neaktivna",
        _ => status.ToString()
    };
}
