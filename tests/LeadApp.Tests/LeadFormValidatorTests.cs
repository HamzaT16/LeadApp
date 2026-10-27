using LeadApp.Domain;
using LeadApp.Services.Leads;
using Microsoft.Extensions.Options;

namespace LeadApp.Tests;

public class LeadFormValidatorTests
{
    private readonly LeadFormValidator _validator = new(TestData.Locations(), MsOptions.Create(TestData.FormOptions()));

    private IEnumerable<string> FieldsWithErrors(LeadFormModel model) => _validator.Validate(model).Select(e => e.Field).Distinct();

    [Fact]
    public void Valid_individual_has_no_errors() =>
        Assert.Empty(_validator.Validate(TestData.ValidFl()));

    [Fact]
    public void Phone_is_required_even_with_email()
    {
        var model = TestData.ValidFl();
        model.PhoneNumber = " ";

        Assert.Equal([nameof(model.PhoneNumber)], FieldsWithErrors(model));
    }

    [Fact]
    public void Email_is_optional()
    {
        var model = TestData.ValidFl();
        model.Email = null;

        Assert.Empty(_validator.Validate(model));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    public void Rejects_invalid_email(string email)
    {
        var model = TestData.ValidFl();
        model.Email = email;

        Assert.Equal([nameof(model.Email)], FieldsWithErrors(model));
    }

    [Fact]
    public void Local_community_must_belong_to_the_municipality()
    {
        var model = TestData.ValidFl();
        model.LocalCommunityId = 101; // belongs to municipality 1, not 99

        Assert.Equal([nameof(model.LocalCommunityId)], FieldsWithErrors(model));
    }

    [Fact]
    public void Unknown_municipality_is_rejected()
    {
        var model = TestData.ValidFl();
        model.CityId = 12345;

        Assert.Equal([nameof(model.CityId)], FieldsWithErrors(model));
    }

    [Theory]
    [InlineData(449)]
    [InlineData(10_001)]
    [InlineData(0)]
    public void Amount_must_be_within_limits(decimal amount)
    {
        var model = TestData.ValidFl();
        model.Amount = amount;

        Assert.Equal([nameof(model.Amount)], FieldsWithErrors(model));
    }

    [Fact]
    public void Consent_is_required()
    {
        var model = TestData.ValidFl();
        model.Consent = false;

        Assert.Equal([nameof(model.Consent)], FieldsWithErrors(model));
    }

    [Fact]
    public void Legal_entity_requires_company_and_contact_and_builds_full_name()
    {
        var model = TestData.ValidFl();
        model.CustomerType = CustomerType.PL;

        Assert.Equal([nameof(model.CompanyName), nameof(model.ContactPerson)], FieldsWithErrors(model));

        model.CompanyName = "  Agro   Plus d.o.o. ";
        model.ContactPerson = "Ana Anić";
        Assert.Empty(_validator.Validate(model));
        Assert.Equal("Agro Plus d.o.o. (Ana Anić)", model.BuildFullName());
    }

    [Fact]
    public void Full_name_longer_than_255_is_rejected()
    {
        var model = TestData.ValidFl();
        model.FirstName = new string('a', 130);
        model.LastName = new string('b', 130);

        Assert.Contains(_validator.Validate(model), e => e.Message.Contains("255"));
    }

    [Theory]
    [InlineData("061 123 456", "+38761123456")]
    [InlineData("+387 61 123-456", "+38761123456")]
    [InlineData("00387 61 123 456", "+38761123456")]
    [InlineData("(033) 123/456", "+38733123456")]
    [InlineData("+382 67 123 456", "+38267123456")]
    public void Normalizes_phone_numbers(string input, string expected) =>
        Assert.Equal(expected, LeadFormValidator.NormalizePhone(input));

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("=1+1")]
    public void Rejects_garbage_phone_numbers(string input) =>
        Assert.Null(LeadFormValidator.NormalizePhone(input));
}
