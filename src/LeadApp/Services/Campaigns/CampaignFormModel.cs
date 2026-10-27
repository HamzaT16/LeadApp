using System.ComponentModel.DataAnnotations;
using LeadApp.Domain;

namespace LeadApp.Services.Campaigns;

/// <summary>Create/edit form for a campaign in the admin panel.</summary>
public sealed class CampaignFormModel : IValidatableObject
{
    public const int MaxTextLength = 255;

    [Required(ErrorMessage = "Unesite naziv kampanje.")]
    [StringLength(MaxTextLength, ErrorMessage = "Najviše 255 znakova.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Unesite URL identifikator.")]
    [StringLength(LeadApp.Services.Campaigns.Slug.MaxLength, ErrorMessage = "Najviše 255 znakova.")]
    public string Slug { get; set; } = "";

    [Required(ErrorMessage = "Unesite opis kampanje.")]
    [StringLength(MaxTextLength, ErrorMessage = "Najviše 255 znakova.")]
    public string Description { get; set; } = "";

    public CustomerType CustomerType { get; set; } = CustomerType.FL;

    [Required(ErrorMessage = "Unesite opis ciljne grupe.")]
    [StringLength(MaxTextLength, ErrorMessage = "Najviše 255 znakova.")]
    public string TargetGroupDescription { get; set; } = "";

    /// <summary>Promotion channel sent to the CRM (<c>channelCode</c>).</summary>
    [Required(ErrorMessage = "Odaberite kanal promocije.")]
    public string ChannelCode { get; set; } = OnlineChannelCodes.Viber;

    /// <summary>Empty = client chooses the product.</summary>
    public string Product { get; set; } = "";

    /// <summary>Lead source for leads from this campaign (<c>sourceCode</c>).</summary>
    [Required(ErrorMessage = "Odaberite izvor prilike.")]
    public string Channel { get; set; } = SourceCodes.Viber;

    public CampaignStatus Status { get; set; } = CampaignStatus.Published;

    [Required(ErrorMessage = "Unesite datum početka.")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "Unesite datum završetka.")]
    public DateOnly? EndDate { get; set; }

    public DateOnly? EndTimeLimit { get; set; }

    [Required(ErrorMessage = "Unesite trošak kampanje (0 ako nema troška).")]
    public decimal? Cost { get; set; }

    [Required(ErrorMessage = "Unesite ID zaposlenog (ROW_ID iz employee.csv).")]
    [Range(1, long.MaxValue, ErrorMessage = "ID zaposlenog mora biti pozitivan broj.")]
    public long? CreatedByEmployeeId { get; set; }

    /// <summary>
    /// Today in Europe/Sarajevo and whether the campaign will be sent to the CRM on save. Set by the page;
    /// the CRM's date rules (start today or later, end in the future) only apply to campaigns not yet in the CRM.
    /// </summary>
    public DateOnly Today { get; set; }
    public bool WillSendToCrm { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Slug) && !LeadApp.Services.Campaigns.Slug.IsValid(Slug))
            yield return new ValidationResult("Dozvoljena su mala slova (a-z), brojevi i crtice, npr. agro-viber-2026.", [nameof(Slug)]);

        if (!OnlineChannelCodes.IsValid(ChannelCode))
            yield return new ValidationResult("Nepodržan kanal promocije.", [nameof(ChannelCode)]);

        if (!SourceCodes.IsValid(Channel))
            yield return new ValidationResult("Nepodržan izvor prilike.", [nameof(Channel)]);

        if (!string.IsNullOrEmpty(Product) && !Enum.TryParse<LoanPurpose>(Product, out _))
            yield return new ValidationResult("Nepodržan proizvod.", [nameof(Product)]);

        if (Cost is { } cost && (cost < 0 || decimal.Round(cost, 2) != cost))
            yield return new ValidationResult("Trošak mora biti 0 ili veći, najviše dvije decimale.", [nameof(Cost)]);

        if (StartDate is { } start && EndDate is { } end && end < start)
            yield return new ValidationResult("Datum završetka mora biti nakon datuma početka.", [nameof(EndDate)]);

        if (EndTimeLimit is { } limit && EndDate is { } endDate && limit < endDate)
            yield return new ValidationResult("Rok pripisivanja ne smije biti prije datuma završetka.", [nameof(EndTimeLimit)]);

        if (WillSendToCrm)
        {
            if (StartDate is { } s && s < Today)
                yield return new ValidationResult("Datum početka mora biti današnji ili budući.", [nameof(StartDate)]);
            if (EndDate is { } e && e < Today)
                yield return new ValidationResult("Datum završetka mora biti u budućnosti.", [nameof(EndDate)]);
        }
    }

    public static CampaignFormModel From(Campaign c, DateOnly today) => new()
    {
        Name = c.Name,
        Slug = c.Slug,
        Description = c.Description,
        CustomerType = c.CustomerType,
        TargetGroupDescription = c.TargetGroupDescription,
        ChannelCode = c.ChannelCode,
        Product = c.Product?.ToString() ?? "",
        Channel = c.Channel,
        Status = c.Status,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        EndTimeLimit = c.EndTimeLimit,
        Cost = c.Cost,
        CreatedByEmployeeId = c.CreatedByEmployeeId,
        Today = today,
        WillSendToCrm = !c.IsInCrm
    };

    /// <summary>
    /// Copies the form into the campaign. For a campaign already in the CRM only local fields change
    /// (product, lead source, status): the spec has no endpoint to update a CRM campaign.
    /// </summary>
    public void ApplyTo(Campaign c)
    {
        c.Product = Enum.TryParse<LoanPurpose>(Product, out var p) ? p : null;
        c.Channel = Channel;
        c.Status = Status;

        if (c.IsInCrm)
            return;

        c.Name = Name.Trim();
        c.Slug = Slug.Trim();
        c.Description = Description.Trim();
        c.CustomerType = CustomerType;
        c.TargetGroupDescription = TargetGroupDescription.Trim();
        c.ChannelCode = ChannelCode;
        c.StartDate = StartDate!.Value;
        c.EndDate = EndDate!.Value;
        c.EndTimeLimit = EndTimeLimit;
        c.Cost = Cost!.Value;
        c.CreatedByEmployeeId = CreatedByEmployeeId;
    }
}
