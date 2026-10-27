using LeadApp.Domain;

namespace LeadApp.Options;

public sealed class CrmOptions
{
    public const string Section = "Crm";

    public string BaseUrl { get; set; } = "";
    public string CreateOpportunityPath { get; set; } = "/api/sales-opportunity/web/create";
    public string CreateCampaignPath { get; set; } = "/api/campaign/online";

    /// <summary>Prefills createdByEmployeeId (ROW_ID from employee.csv) in the campaign form.</summary>
    public long? DefaultEmployeeId { get; set; }

    /// <summary>Partner API key. Keep it in user-secrets / environment / Key Vault, never in appsettings.</summary>
    public string? ApiKey { get; set; }

    /// <summary>sourceCode used for leads that do not come through a campaign link.</summary>
    public string DefaultSourceCode { get; set; } = SourceCodes.Email;

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Use a fake CRM that always succeeds (local development only).</summary>
    public bool UseFake { get; set; }
}

public sealed class FirebaseOptions
{
    public const string Section = "Firebase";

    public string ProjectId { get; set; } = "";
    public string? CredentialsPath { get; set; }
    public string? CredentialsJson { get; set; }

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(CredentialsPath) || !string.IsNullOrWhiteSpace(CredentialsJson);
}

public sealed class AdminSeedOptions
{
    public const string Section = "Admin";

    public string? Email { get; set; }
    public string? InitialPassword { get; set; }
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}

public sealed class LeadFormOptions
{
    public const string Section = "LeadForm";

    public decimal MinAmount { get; set; } = 450;
    public decimal MaxAmount { get; set; } = 10_000;
    public decimal Step { get; set; } = 50;
    public decimal DefaultAmount { get; set; } = 5_000;
    public string Currency { get; set; } = "KM";

    public int SubmissionsPerWindow { get; set; } = 5;
    public int WindowMinutes { get; set; } = 10;

    public string TermsUrl { get; set; } = "https://www.partner.ba/wp-content/uploads/2026/06/O-A3-1-35-30-Opsti-uslovi-poslovanja.pdf";
    public string PriceListUrl { get; set; } = "https://www.partner.ba/wp-content/uploads/2026/08/Cjenovnik-MKF-ver-106.pdf";
    public string HomepageUrl { get; set; } = "https://www.partner.ba";
}
