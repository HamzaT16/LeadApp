namespace LeadApp.Domain;

/// <summary>
/// Allowed values of <c>channelCode</c> from the CX360 spec ("Kanal online kampanje").
/// A separate codebook from <see cref="SourceCodes"/>: adding a campaign channel does not add a lead source.
/// </summary>
public static class OnlineChannelCodes
{
    public const string Viber = "VIBER";
    public const string Instagram = "INSTAGRAM";
    public const string Facebook = "FACEBOOK";
    public const string Phone = "PHONE";
    public const string Sms = "SMS";
    public const string SmsViber = "SMS_VIBER";
    public const string Email = "EMAIL";
    public const string Branch = "BRANCH";

    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        (Viber, "Viber"),
        (Instagram, "Instagram"),
        (Facebook, "Facebook"),
        (Phone, "Telefon"),
        (Sms, "SMS"),
        (SmsViber, "SMS i Viber"),
        (Email, "E-mail"),
        (Branch, "Filijala")
    ];

    public static bool IsValid(string? code) => code is not null && All.Any(c => c.Code == code);

    public static string NameOf(string code) => All.FirstOrDefault(c => c.Code == code).Name ?? code;

    /// <summary>
    /// Natural lead <c>sourceCode</c> for a promotion channel, or <c>null</c> when there is none
    /// (Instagram/Facebook have no matching source in the spec).
    /// </summary>
    public static string? DefaultSourceCode(string? channelCode) => channelCode switch
    {
        Viber or SmsViber => SourceCodes.Viber,
        Sms => SourceCodes.Sms,
        Email => SourceCodes.Email,
        Phone => SourceCodes.CallCenter,
        Branch => SourceCodes.BranchOffice,
        _ => null
    };
}
