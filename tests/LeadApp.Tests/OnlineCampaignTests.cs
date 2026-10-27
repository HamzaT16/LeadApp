using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeadApp.Domain;
using LeadApp.Options;
using LeadApp.Services.Campaigns;
using LeadApp.Services.Crm;
using LeadApp.Services.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeadApp.Tests;

/// <summary>Spec section 6: POST /api/campaign/online.</summary>
public class OnlineCampaignTests
{
    private static Campaign SampleCampaign(DateOnly? endTimeLimit = null) => new()
    {
        Name = "Online gotovinski kredit — oktobar 2026",
        Slug = "gotovinski-kredit-2026-10-viber",
        Description = "Promocija gotovinskih kredita putem Vibera.",
        CustomerType = CustomerType.FL,
        TargetGroupDescription = "Fizička lica zainteresovana za gotovinski kredit.",
        ChannelCode = OnlineChannelCodes.Viber,
        StartDate = new DateOnly(2026, 10, 1),
        EndDate = new DateOnly(2026, 10, 31),
        EndTimeLimit = endTimeLimit,
        Cost = 1500m,
        CreatedByEmployeeId = 142
    };

    private static JsonObject ToJson(Campaign c) =>
        JsonNode.Parse(JsonSerializer.Serialize(CreateOnlineCampaignRequest.FromCampaign(c), CrmJson.Options))!.AsObject();

    [Fact]
    public void Request_json_matches_the_spec_example()
    {
        var json = ToJson(SampleCampaign());

        Assert.Equal(
            ["name", "description", "customerTypeCode", "targetGroupDescription", "channelCode", "urlId", "startTime", "endTime", "cost", "createdByEmployeeId"],
            json.Select(kv => kv.Key));
        Assert.Equal("FL", (string?)json["customerTypeCode"]);
        Assert.Equal("VIBER", (string?)json["channelCode"]);
        Assert.Equal("gotovinski-kredit-2026-10-viber", (string?)json["urlId"]);
        Assert.Equal(1500m, (decimal)json["cost"]!);
        Assert.Equal(142, (long)json["createdByEmployeeId"]!);
    }

    [Fact]
    public void Times_are_iso_8601_with_the_sarajevo_offset_across_dst()
    {
        var json = ToJson(SampleCampaign(endTimeLimit: new DateOnly(2026, 11, 15)));

        // 1 Oct is summer time (+02:00), 31 Oct and 15 Nov are winter time (+01:00) — same as the spec example.
        Assert.Equal("2026-10-01T00:00:00+02:00", (string?)json["startTime"]);
        Assert.Equal("2026-10-31T23:59:59+01:00", (string?)json["endTime"]);
        Assert.Equal("2026-11-15T23:59:59+01:00", (string?)json["endTimeLimit"]);
    }

    [Fact]
    public void Cost_is_rounded_to_two_decimals()
    {
        var campaign = SampleCampaign();
        campaign.Cost = 99.999m;

        Assert.Equal(100.00m, CreateOnlineCampaignRequest.FromCampaign(campaign).Cost);
    }

    [Theory]
    [InlineData(OnlineChannelCodes.Viber, SourceCodes.Viber)]
    [InlineData(OnlineChannelCodes.SmsViber, SourceCodes.Viber)]
    [InlineData(OnlineChannelCodes.Sms, SourceCodes.Sms)]
    [InlineData(OnlineChannelCodes.Email, SourceCodes.Email)]
    [InlineData(OnlineChannelCodes.Phone, SourceCodes.CallCenter)]
    [InlineData(OnlineChannelCodes.Branch, SourceCodes.BranchOffice)]
    [InlineData(OnlineChannelCodes.Instagram, null)]
    [InlineData(OnlineChannelCodes.Facebook, null)]
    public void Channel_maps_to_a_default_lead_source(string channelCode, string? expected) =>
        Assert.Equal(expected, OnlineChannelCodes.DefaultSourceCode(channelCode));

    // ---------- CrmClient ----------

    [Fact]
    public async Task Created_response_is_parsed_and_posted_to_the_campaign_path()
    {
        var handler = new StubHandler(HttpStatusCode.Created,
            """{"campaignId":54,"urlId":"gotovinski-kredit-2026-10-viber","categoryCode":"SALES","typeCode":"ONLINE","statusCode":"DRAFT","endTimeLimit":"2026-10-31T23:59:59+01:00"}""");

        var result = await CreateClient(handler).CreateOnlineCampaignAsync(CreateOnlineCampaignRequest.FromCampaign(SampleCampaign()));

        Assert.True(result.Success);
        Assert.Equal(54, result.Response!.CampaignId);
        Assert.Equal("DRAFT", result.Response.StatusCode);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.FromHours(1)), result.Response.EndTimeLimit);
        Assert.Equal("https://crm.test/api/campaign/online", handler.Request!.RequestUri!.ToString());
        Assert.Equal("App secret-key", handler.Request.Headers.GetValues(CrmClient.ApiKeyHeader).Single());
    }

    [Fact]
    public async Task Unknown_employee_is_reported_as_404()
    {
        var handler = new StubHandler(HttpStatusCode.NotFound,
            """{"errorType":"NOT_FOUND","message":"Zaposleni ne postoji.","validationErrors":null,"timestamp":"2026-09-01 14:30:00"}""");

        var result = await CreateClient(handler).CreateOnlineCampaignAsync(CreateOnlineCampaignRequest.FromCampaign(SampleCampaign()));

        Assert.False(result.Success);
        Assert.True(result.EmployeeNotFound);
        Assert.False(result.UrlIdConflict);
        Assert.Contains("NOT_FOUND", result.Error);
    }

    [Fact]
    public async Task Taken_url_id_is_reported_as_conflict()
    {
        var handler = new StubHandler(HttpStatusCode.Conflict,
            """{"errorType":"GENERAL","message":"urlId je već dodijeljen.","validationErrors":null,"timestamp":"2026-09-01 14:30:00"}""");

        var result = await CreateClient(handler).CreateOnlineCampaignAsync(CreateOnlineCampaignRequest.FromCampaign(SampleCampaign()));

        Assert.True(result.UrlIdConflict);
        Assert.Contains("409", result.Error);
    }

    // ---------- CampaignService ----------

    [Fact]
    public async Task Create_saves_locally_then_registers_in_crm()
    {
        var repo = new InMemoryCampaignRepository();
        var crm = new ScriptedCrm(CrmCampaignResult.Ok(new CreateOnlineCampaignResponse(54, "x", "SALES", "ONLINE", "ACTIVE", null)));
        var service = new CampaignService(repo, crm, TestData.Clock(), NullLogger<CampaignService>.Instance);

        var campaign = SampleCampaign();
        await service.CreateAsync(campaign);

        var stored = await repo.GetAsync(campaign.Id);
        Assert.Equal(CrmSyncStatus.Synced, stored!.CrmStatus);
        Assert.Equal(54, stored.CrmCampaignId);
        Assert.Equal("ACTIVE", stored.CrmStatusCode);
        Assert.True(stored.IsInCrm);
    }

    [Fact]
    public async Task Crm_conflict_keeps_the_campaign_and_marks_it_failed_and_retry_succeeds()
    {
        var repo = new InMemoryCampaignRepository();
        var crm = new ScriptedCrm(CrmCampaignResult.Fail("409 Conflict: urlId zauzet", HttpStatusCode.Conflict));
        var service = new CampaignService(repo, crm, TestData.Clock(), NullLogger<CampaignService>.Instance);

        var campaign = SampleCampaign();
        var result = await service.CreateAsync(campaign);

        Assert.True(result.UrlIdConflict);
        var stored = (await repo.GetAsync(campaign.Id))!;
        Assert.Equal(CrmSyncStatus.Failed, stored.CrmStatus);
        Assert.Contains("409", stored.CrmError);

        crm.Next = CrmCampaignResult.Ok(new CreateOnlineCampaignResponse(55, "y", "SALES", "ONLINE", "DRAFT", null));
        stored.Slug = "drugi-url-id";
        await service.UpdateAsync(stored);

        var retried = (await repo.GetAsync(campaign.Id))!;
        Assert.Equal(CrmSyncStatus.Synced, retried.CrmStatus);
        Assert.Equal("drugi-url-id", crm.LastRequest!.UrlId);
        Assert.Null(retried.CrmError);
    }

    [Fact]
    public async Task Missing_employee_id_fails_without_calling_the_crm()
    {
        var repo = new InMemoryCampaignRepository();
        var crm = new ScriptedCrm(CrmCampaignResult.Ok(new CreateOnlineCampaignResponse(1, "x", "SALES", "ONLINE", "ACTIVE", null)));
        var service = new CampaignService(repo, crm, TestData.Clock(), NullLogger<CampaignService>.Instance);

        var campaign = SampleCampaign();
        campaign.CreatedByEmployeeId = null;
        var result = await service.CreateAsync(campaign);

        Assert.False(result.Success);
        Assert.Null(crm.LastRequest);
    }

    [Fact]
    public void Synced_campaign_only_takes_local_fields_from_the_form()
    {
        var campaign = SampleCampaign();
        campaign.CrmStatus = CrmSyncStatus.Synced;

        var form = CampaignFormModel.From(campaign, new DateOnly(2026, 9, 1));
        form.Name = "Promijenjeno";
        form.Slug = "promijenjeno";
        form.Channel = SourceCodes.Sms;
        form.Status = CampaignStatus.Inactive;
        form.ApplyTo(campaign);

        Assert.False(form.WillSendToCrm);
        Assert.Equal("Online gotovinski kredit — oktobar 2026", campaign.Name);
        Assert.Equal("gotovinski-kredit-2026-10-viber", campaign.Slug);
        Assert.Equal(SourceCodes.Sms, campaign.Channel);
        Assert.Equal(CampaignStatus.Inactive, campaign.Status);
    }

    // ---------- form validation ----------

    private static CampaignFormModel ValidForm(DateOnly today) => new()
    {
        Name = "Agro Viber",
        Slug = "agro-viber",
        Description = "Opis",
        TargetGroupDescription = "Poljoprivrednici",
        ChannelCode = OnlineChannelCodes.Viber,
        Channel = SourceCodes.Viber,
        StartDate = today,
        EndDate = today.AddDays(30),
        Cost = 0,
        CreatedByEmployeeId = 142,
        Today = today
    };

    private static IEnumerable<string> InvalidMembers(CampaignFormModel form)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).Distinct();
    }

    [Fact]
    public void Valid_form_passes() =>
        Assert.Empty(InvalidMembers(ValidForm(new DateOnly(2026, 10, 27))));

    [Fact]
    public void Requires_description_target_group_cost_and_employee()
    {
        var form = ValidForm(new DateOnly(2026, 10, 27));
        form.Description = "";
        form.TargetGroupDescription = " ";
        form.Cost = null;
        form.CreatedByEmployeeId = null;

        Assert.Equal(
            new[] { nameof(form.Cost), nameof(form.CreatedByEmployeeId), nameof(form.Description), nameof(form.TargetGroupDescription) },
            InvalidMembers(form).Order());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10.005)]
    public void Cost_must_be_non_negative_with_two_decimals(double cost)
    {
        var form = ValidForm(new DateOnly(2026, 10, 27));
        form.Cost = (decimal)cost;

        Assert.Equal([nameof(form.Cost)], InvalidMembers(form));
    }

    [Fact]
    public void Start_in_the_past_and_ended_campaigns_are_rejected_for_crm()
    {
        var today = new DateOnly(2026, 10, 27);
        var form = ValidForm(today);
        form.StartDate = today.AddDays(-5);
        form.EndDate = today.AddDays(-1);

        Assert.Equal([nameof(form.EndDate), nameof(form.StartDate)], InvalidMembers(form).Order());

        form.WillSendToCrm = false; // already in the CRM: date rules no longer apply
        Assert.Empty(InvalidMembers(form));
    }

    [Fact]
    public void End_time_limit_cannot_be_before_end_date()
    {
        var form = ValidForm(new DateOnly(2026, 10, 27));
        form.EndTimeLimit = form.EndDate!.Value.AddDays(-1);

        Assert.Equal([nameof(form.EndTimeLimit)], InvalidMembers(form));
    }

    [Fact]
    public void Channel_code_and_source_code_use_separate_codebooks()
    {
        var form = ValidForm(new DateOnly(2026, 10, 27));
        form.ChannelCode = SourceCodes.WhatsApp; // a sourceCode, not a channelCode
        form.Channel = OnlineChannelCodes.Instagram; // a channelCode, not a sourceCode

        Assert.Equal([nameof(form.Channel), nameof(form.ChannelCode)], InvalidMembers(form).Order());
    }

    // ---------- helpers ----------

    private static CrmClient CreateClient(StubHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://crm.test") };
        var options = MsOptions.Create(new CrmOptions { BaseUrl = "https://crm.test", ApiKey = "secret-key" });
        return new CrmClient(http, options, NullLogger<CrmClient>.Instance);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class ScriptedCrm(CrmCampaignResult next) : ICrmClient
    {
        public CrmCampaignResult Next { get; set; } = next;
        public CreateOnlineCampaignRequest? LastRequest { get; private set; }

        public Task<CrmResult> CreateSalesOpportunityAsync(CreateSalesOpportunityRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CrmCampaignResult> CreateOnlineCampaignAsync(CreateOnlineCampaignRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(Next);
        }
    }
}
