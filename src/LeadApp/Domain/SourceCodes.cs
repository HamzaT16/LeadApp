namespace LeadApp.Domain;

/// <summary>Allowed values of <c>sourceCode</c> from the CX360 spec ("Izvor prodajne prilike").</summary>
public static class SourceCodes
{
    public const string Sms = "SMS";
    public const string Viber = "VIBER";
    public const string WhatsApp = "WHATSAPP";
    public const string Email = "EMAIL";
    public const string CallCenter = "CALL_CENTER";
    public const string BranchOffice = "BRANCH_OFFICE";

    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        (Sms, "SMS"),
        (Viber, "Viber"),
        (WhatsApp, "WhatsApp"),
        (Email, "E-mail"),
        (CallCenter, "Call centar"),
        (BranchOffice, "Filijale/Kancelarije")
    ];

    public static bool IsValid(string? code) => code is not null && All.Any(s => s.Code == code);

    public static string NameOf(string code) => All.FirstOrDefault(s => s.Code == code).Name ?? code;
}
