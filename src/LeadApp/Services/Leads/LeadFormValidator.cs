using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using LeadApp.Domain;
using LeadApp.Options;
using LeadApp.Services.Locations;
using Microsoft.Extensions.Options;

namespace LeadApp.Services.Leads;

public sealed record FieldError(string Field, string Message);

/// <summary>
/// Validation rules for the public form, derived from spec section 1. Used by the UI (live feedback)
/// and again on the server side of the submit, so the rules cannot be bypassed.
/// </summary>
public sealed partial class LeadFormValidator(ILocationService locations, IOptions<LeadFormOptions> options)
{
    public const int MaxFullNameLength = 255;
    public const int MaxContactLength = 128;

    private static readonly EmailAddressAttribute EmailAttribute = new();

    public IReadOnlyList<FieldError> Validate(LeadFormModel m)
    {
        var errors = new List<FieldError>();
        void Add(string field, string message) => errors.Add(new FieldError(field, message));

        if (m.CustomerType == CustomerType.FL)
        {
            if (string.IsNullOrWhiteSpace(m.FirstName)) Add(nameof(m.FirstName), "Unesite ime.");
            if (string.IsNullOrWhiteSpace(m.LastName)) Add(nameof(m.LastName), "Unesite prezime.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(m.CompanyName)) Add(nameof(m.CompanyName), "Unesite naziv firme.");
            if (string.IsNullOrWhiteSpace(m.ContactPerson)) Add(nameof(m.ContactPerson), "Unesite kontakt osobu.");
        }

        if (errors.Count == 0 && m.BuildFullName().Length > MaxFullNameLength)
            Add(m.CustomerType == CustomerType.FL ? nameof(m.LastName) : nameof(m.CompanyName), "Naziv je predug (najviše 255 znakova).");

        // Business rule: phone is mandatory, e-mail optional (stricter than the spec's "phone or e-mail").
        if (string.IsNullOrWhiteSpace(m.PhoneNumber))
            Add(nameof(m.PhoneNumber), "Unesite broj telefona.");
        else if (NormalizePhone(m.PhoneNumber) is null)
            Add(nameof(m.PhoneNumber), "Neispravan broj telefona (npr. +387 61 123 456).");

        if (!string.IsNullOrWhiteSpace(m.Email) &&(m.Email!.Trim().Length > MaxContactLength || !EmailAttribute.IsValid(m.Email.Trim())))
            Add(nameof(m.Email), "Neispravna e-mail adresa.");

        if (m.CityId is not { } cityId || locations.FindMunicipality(cityId) is null)
            Add(nameof(m.CityId), "Odaberite općinu.");
        else if (m.LocalCommunityId is not { } mzId || !locations.BelongsTo(mzId, cityId))
            Add(nameof(m.LocalCommunityId), "Odaberite mjesnu zajednicu.");

        var o = options.Value;
        if (m.Amount < o.MinAmount || m.Amount > o.MaxAmount || m.Amount <= 0)
            Add(nameof(m.Amount), $"Iznos mora biti između {o.MinAmount:N0} i {o.MaxAmount:N0} {o.Currency}.");

        if (!Enum.IsDefined(m.Purpose))
            Add(nameof(m.Purpose), "Odaberite svrhu kredita.");

        if (!m.Consent)
            Add(nameof(m.Consent), "Potrebna je saglasnost za obradu podataka.");

        return errors;
    }

    /// <summary>
    /// Normalises a BiH phone number to E.164-like form ("+38761123456") so the CRM can match contacts.
    /// Returns <c>null</c> when the input is not a plausible phone number.
    /// </summary>
    public static string? NormalizePhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var compact = PhoneSeparators().Replace(input.Trim(), "");
        if (compact.StartsWith("00", StringComparison.Ordinal))
            compact = "+" + compact[2..];
        else if (compact.StartsWith('0'))
            compact = "+387" + compact[1..];

        return PhonePattern().IsMatch(compact) && compact.Length <= MaxContactLength ? compact : null;
    }

    [GeneratedRegex(@"[\s\-/().]")]
    private static partial Regex PhoneSeparators();

    [GeneratedRegex(@"^\+?[1-9]\d{6,14}$")]
    private static partial Regex PhonePattern();
}
