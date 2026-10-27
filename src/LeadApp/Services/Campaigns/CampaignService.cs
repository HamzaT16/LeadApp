using LeadApp.Domain;
using LeadApp.Services.Crm;
using LeadApp.Services.Storage;

namespace LeadApp.Services.Campaigns;

/// <summary>
/// Creates campaigns and registers them in the CRM (spec section 6). The campaign is saved locally first,
/// so a CRM failure never loses it; it is marked failed and can be resent from the admin panel.
/// </summary>
public sealed class CampaignService(
    ICampaignRepository campaigns,
    ICrmClient crm,
    AppClock clock,
    ILogger<CampaignService> logger)
{
    /// <exception cref="SlugConflictException">The slug is already used by another local campaign.</exception>
    public async Task<CrmCampaignResult> CreateAsync(Campaign campaign, CancellationToken ct = default)
    {
        campaign.CreatedAtUtc = clock.UtcNow;
        await campaigns.CreateAsync(campaign, ct);
        return await SendToCrmAsync(campaign, ct);
    }

    /// <summary>Saves edits; a campaign that is not in the CRM yet is (re)sent right away.</summary>
    /// <returns>The CRM result, or <c>null</c> when the campaign was already in the CRM.</returns>
    /// <exception cref="SlugConflictException">The slug is already used by another local campaign.</exception>
    public async Task<CrmCampaignResult?> UpdateAsync(Campaign campaign, CancellationToken ct = default)
    {
        await campaigns.UpdateAsync(campaign, ct);
        return campaign.IsInCrm ? null : await SendToCrmAsync(campaign, ct);
    }

    /// <summary>Sends a stored campaign to the CRM and records the outcome on it.</summary>
    public async Task<CrmCampaignResult> SendToCrmAsync(Campaign campaign, CancellationToken ct = default)
    {
        if (campaign.IsInCrm)
            throw new InvalidOperationException($"Kampanja #{campaign.Id} je već u CRM-u.");

        var result = campaign.CreatedByEmployeeId is null
            ? CrmCampaignResult.Fail("Nedostaje ID zaposlenog (createdByEmployeeId).")
            : await crm.CreateOnlineCampaignAsync(CreateOnlineCampaignRequest.FromCampaign(campaign), ct);

        campaign.CrmLastAttemptUtc = clock.UtcNow;
        if (result.Response is { } r)
        {
            campaign.CrmStatus = CrmSyncStatus.Synced;
            campaign.CrmCampaignId = r.CampaignId;
            campaign.CrmStatusCode = r.StatusCode;
            campaign.CrmError = null;
        }
        else
        {
            campaign.CrmStatus = CrmSyncStatus.Failed;
            campaign.CrmError = result.Error;
            logger.LogWarning("Campaign {Id} not registered in CRM: {Error}", campaign.Id, result.Error);
        }

        await campaigns.UpdateAsync(campaign, ct);
        return result;
    }
}
