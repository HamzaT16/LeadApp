using LeadApp.Domain;
using LeadApp.Options;
using LeadApp.Services.Crm;
using LeadApp.Services.Storage;
using Microsoft.Extensions.Options;

namespace LeadApp.Services.Leads;

public enum SubmissionOutcome { Accepted, Invalid, RateLimited, CampaignInactive, Error }

public sealed record SubmissionResult(SubmissionOutcome Outcome, IReadOnlyList<FieldError>? Errors = null)
{
    public static readonly SubmissionResult Accepted = new(SubmissionOutcome.Accepted);
}

/// <summary>
/// Saves a lead first, then forwards it to the CRM (spec section 1). The lead is stored even when the
/// CRM call fails, so nothing is lost; failed leads can be resent from the admin panel.
/// </summary>
public sealed class LeadSubmissionService(
    LeadFormValidator validator,
    ILeadRepository leads,
    ICampaignRepository campaigns,
    ICrmClient crm,
    SubmissionRateLimiter rateLimiter,
    AppClock clock,
    IOptions<CrmOptions> crmOptions,
    ILogger<LeadSubmissionService> logger)
{
    public async Task<SubmissionResult> SubmitAsync(LeadFormModel model, string? campaignId, string clientKey, CancellationToken ct = default)
    {
        // Bots that fill the honeypot get a fake success and nothing is stored.
        if (!string.IsNullOrEmpty(model.Website))
        {
            logger.LogInformation("Honeypot triggered from {Client}", clientKey);
            return SubmissionResult.Accepted;
        }

        var errors = validator.Validate(model);
        if (errors.Count > 0)
            return new SubmissionResult(SubmissionOutcome.Invalid, errors);

        if (!rateLimiter.TryAcquire(clientKey))
            return new SubmissionResult(SubmissionOutcome.RateLimited);

        Campaign? campaign = null;
        if (campaignId is not null)
        {
            campaign = await campaigns.GetAsync(campaignId, ct);
            if (campaign is null || !campaign.IsLive(clock.Today))
                return new SubmissionResult(SubmissionOutcome.CampaignInactive);
        }

        try
        {
            var lead = new Lead
            {
                CreatedAtUtc = clock.UtcNow,
                CustomerType = model.CustomerType,
                FullName = model.BuildFullName(),
                PhoneNumber = LeadFormValidator.NormalizePhone(model.PhoneNumber),
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                CityId = model.CityId!.Value,
                LocalCommunityId = model.LocalCommunityId!.Value,
                RequestedAmount = model.Amount,
                Purpose = campaign?.Product ?? model.Purpose,
                SourceCode = campaign?.Channel ?? crmOptions.Value.DefaultSourceCode,
                CampaignId = campaign?.Id,
                CampaignUrlId = campaign?.Slug,
                CampaignName = campaign?.Name
            };

            await leads.AddAsync(lead, ct);
            if (campaign is not null)
                await campaigns.IncrementLeadsAsync(campaign.Id, ct);

            await SendToCrmAsync(lead, ct);
            return SubmissionResult.Accepted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Saving lead failed");
            return new SubmissionResult(SubmissionOutcome.Error);
        }
    }

    /// <summary>Resends a stored lead to the CRM (admin action for failed syncs).</summary>
    public async Task<Lead?> RetryCrmAsync(string leadId, CancellationToken ct = default)
    {
        var lead = await leads.GetAsync(leadId, ct);
        if (lead is null || lead.CrmStatus == CrmSyncStatus.Synced)
            return lead;

        await SendToCrmAsync(lead, ct);
        return lead;
    }

    private async Task SendToCrmAsync(Lead lead, CancellationToken ct)
    {
        var result = await crm.CreateSalesOpportunityAsync(CreateSalesOpportunityRequest.FromLead(lead), ct);

        lead.CrmAttempts++;
        lead.CrmLastAttemptUtc = clock.UtcNow;
        if (result.Response is { } r)
        {
            lead.CrmStatus = CrmSyncStatus.Synced;
            lead.CrmSalesOpportunityId = r.SalesOpportunityId;
            lead.CrmPhaseCode = r.PhaseCode;
            lead.CrmCustomerId = r.CustomerId;
            lead.CrmLeadId = r.LeadId;
            lead.CrmMatchResult = r.MatchResult;
            lead.CrmError = null;
        }
        else
        {
            lead.CrmStatus = CrmSyncStatus.Failed;
            lead.CrmError = result.Error;
        }

        await leads.UpdateCrmResultAsync(lead, ct);
    }
}
