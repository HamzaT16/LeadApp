using LeadApp.Domain;
using LeadApp.Options;
using LeadApp.Services;
using LeadApp.Services.Crm;
using LeadApp.Services.Leads;
using LeadApp.Services.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeadApp.Tests;

public class LeadSubmissionServiceTests
{
    private readonly InMemoryLeadRepository _leads = new();
    private readonly InMemoryCampaignRepository _campaigns = new();
    private readonly RecordingCrm _crm = new();
    private readonly AppClock _clock = TestData.Clock();
    private readonly LeadSubmissionService _service;

    public LeadSubmissionServiceTests()
    {
        var formOptions = MsOptions.Create(TestData.FormOptions());
        _service = new LeadSubmissionService(
            new LeadFormValidator(TestData.Locations(), formOptions),
            _leads,
            _campaigns,
            _crm,
            new SubmissionRateLimiter(formOptions, new ManualTimeProvider(DateTimeOffset.UtcNow)),
            _clock,
            MsOptions.Create(new CrmOptions { DefaultSourceCode = SourceCodes.CallCenter }),
            NullLogger<LeadSubmissionService>.Instance);
    }

    [Fact]
    public async Task Accepted_lead_is_stored_and_marked_synced()
    {
        var result = await _service.SubmitAsync(TestData.ValidFl(), campaignId: null, "1.1.1.1");

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);
        var lead = Assert.Single(await _leads.GetAllAsync());
        Assert.Equal(1, lead.Number);
        Assert.Equal("Marko Marković", lead.FullName);
        Assert.Equal("+38761123456", lead.PhoneNumber);
        Assert.Equal(SourceCodes.CallCenter, lead.SourceCode);
        Assert.Equal(CrmSyncStatus.Synced, lead.CrmStatus);
        Assert.Equal(900, lead.CrmSalesOpportunityId);
        Assert.Null(_crm.LastRequest!.UrlId);
    }

    [Fact]
    public async Task Crm_failure_keeps_the_lead_and_retry_can_fix_it()
    {
        _crm.FailWith = "503 Service Unavailable";

        var result = await _service.SubmitAsync(TestData.ValidFl(), null, "1.1.1.1");

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);
        var lead = Assert.Single(await _leads.GetAllAsync());
        Assert.Equal(CrmSyncStatus.Failed, lead.CrmStatus);
        Assert.Equal("503 Service Unavailable", lead.CrmError);

        _crm.FailWith = null;
        var retried = await _service.RetryCrmAsync(lead.Id);

        Assert.Equal(CrmSyncStatus.Synced, retried!.CrmStatus);
        Assert.Equal(2, retried.CrmAttempts);
        Assert.Null(retried.CrmError);
    }

    [Fact]
    public async Task Campaign_lead_uses_channel_and_slug_and_counts_the_lead()
    {
        var campaign = await _campaigns.CreateAsync(new Campaign
        {
            Name = "Agro Viber",
            Slug = "agro-viber",
            Channel = SourceCodes.Viber,
            Product = LoanPurpose.Poljoprivreda,
            StartDate = _clock.Today.AddDays(-1),
            EndDate = _clock.Today.AddDays(10)
        });

        var model = TestData.ValidFl();
        model.Purpose = LoanPurpose.Biznis; // campaign locks the product

        await _service.SubmitAsync(model, campaign.Id, "1.1.1.1");

        var lead = Assert.Single(await _leads.GetAllAsync());
        Assert.Equal(SourceCodes.Viber, lead.SourceCode);
        Assert.Equal(LoanPurpose.Poljoprivreda, lead.Purpose);
        Assert.Equal("agro-viber", _crm.LastRequest!.UrlId);
        Assert.Equal(1, (await _campaigns.GetAsync(campaign.Id))!.LeadsCount);
    }

    [Fact]
    public async Task Inactive_campaign_is_rejected()
    {
        var campaign = await _campaigns.CreateAsync(new Campaign
        {
            Name = "Stara",
            Slug = "stara",
            StartDate = _clock.Today.AddDays(-30),
            EndDate = _clock.Today.AddDays(-1)
        });

        var result = await _service.SubmitAsync(TestData.ValidFl(), campaign.Id, "1.1.1.1");

        Assert.Equal(SubmissionOutcome.CampaignInactive, result.Outcome);
        Assert.Empty(await _leads.GetAllAsync());
    }

    [Fact]
    public async Task Honeypot_pretends_success_but_stores_nothing()
    {
        var model = TestData.ValidFl();
        model.Website = "http://spam";

        var result = await _service.SubmitAsync(model, null, "1.1.1.1");

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);
        Assert.Empty(await _leads.GetAllAsync());
        Assert.Null(_crm.LastRequest);
    }

    [Fact]
    public async Task Invalid_model_is_not_stored()
    {
        var model = TestData.ValidFl();
        model.Consent = false;

        var result = await _service.SubmitAsync(model, null, "1.1.1.1");

        Assert.Equal(SubmissionOutcome.Invalid, result.Outcome);
        Assert.Empty(await _leads.GetAllAsync());
    }

    [Fact]
    public async Task Rate_limit_kicks_in_per_client()
    {
        for (var i = 0; i < 5; i++)
            Assert.Equal(SubmissionOutcome.Accepted, (await _service.SubmitAsync(TestData.ValidFl(), null, "9.9.9.9")).Outcome);

        Assert.Equal(SubmissionOutcome.RateLimited, (await _service.SubmitAsync(TestData.ValidFl(), null, "9.9.9.9")).Outcome);
        Assert.Equal(SubmissionOutcome.Accepted, (await _service.SubmitAsync(TestData.ValidFl(), null, "8.8.8.8")).Outcome);
    }

    private sealed class RecordingCrm : ICrmClient
    {
        public string? FailWith { get; set; }
        public CreateSalesOpportunityRequest? LastRequest { get; private set; }

        public Task<CrmResult> CreateSalesOpportunityAsync(CreateSalesOpportunityRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(FailWith is null
                ? CrmResult.Ok(new CreateSalesOpportunityResponse(900, "CREATED", null, 77, "LEAD_CREATED"))
                : CrmResult.Fail(FailWith));
        }

        public Task<CrmCampaignResult> CreateOnlineCampaignAsync(CreateOnlineCampaignRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
