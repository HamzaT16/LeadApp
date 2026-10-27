using LeadApp.Domain;

namespace LeadApp.Services.Leads;

/// <summary>Bound to the public form. Validation lives in <see cref="LeadFormValidator"/>.</summary>
public sealed class LeadFormModel
{
    public CustomerType CustomerType { get; set; } = CustomerType.FL;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }

    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    public long? CityId { get; set; }
    public long? LocalCommunityId { get; set; }

    public decimal Amount { get; set; }
    public LoanPurpose Purpose { get; set; } = LoanPurpose.Stanovnistvo;
    public bool Consent { get; set; }

    /// <summary>Honeypot: hidden from people, bots tend to fill it.</summary>
    public string? Website { get; set; }

    /// <summary>CRM <c>fullName</c>: "Ime Prezime" or "Firma (Kontakt osoba)".</summary>
    public string BuildFullName() => CustomerType == CustomerType.FL
        ? $"{Clean(FirstName)} {Clean(LastName)}".Trim()
        : $"{Clean(CompanyName)} ({Clean(ContactPerson)})";

    internal static string Clean(string? value) =>
        string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
