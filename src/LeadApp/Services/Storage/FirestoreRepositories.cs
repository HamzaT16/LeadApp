using System.Globalization;
using Google.Cloud.Firestore;
using LeadApp.Domain;

namespace LeadApp.Services.Storage;

// Collections: leads, campaigns, campaignSlugs (uniqueness index), counters, admins.
// The server talks to Firestore with a service account; firestore.rules denies all client SDK access.

public sealed class FirestoreLeadRepository(FirestoreDb db) : ILeadRepository
{
    private CollectionReference Leads => db.Collection("leads");
    private DocumentReference Counter => db.Collection("counters").Document("leads");

    public async Task<Lead> AddAsync(Lead lead, CancellationToken ct = default)
    {
        // Sequential numbers come from a counter document inside a transaction (no max+1 race).
        await db.RunTransactionAsync(async tx =>
        {
            var counter = await tx.GetSnapshotAsync(Counter, ct);
            var next = (counter.Exists && counter.TryGetValue<long>("value", out var current) ? current : 0) + 1;

            lead.Number = next;
            lead.Id = next.ToString(CultureInfo.InvariantCulture);
            tx.Set(Counter, new Dictionary<string, object> { ["value"] = next });
            tx.Create(Leads.Document(lead.Id), FirestoreMapper.ToMap(lead));
        }, cancellationToken: ct);

        return lead;
    }

    public Task UpdateCrmResultAsync(Lead lead, CancellationToken ct = default) =>
        Leads.Document(lead.Id).UpdateAsync(FirestoreMapper.CrmFields(lead)!, cancellationToken: ct);

    public async Task<Lead?> GetAsync(string id, CancellationToken ct = default)
    {
        var snap = await Leads.Document(id).GetSnapshotAsync(ct);
        return snap.Exists ? FirestoreMapper.ToLead(snap) : null;
    }

    public async Task<IReadOnlyList<Lead>> GetAllAsync(CancellationToken ct = default)
    {
        var snap = await Leads.OrderByDescending("number").GetSnapshotAsync(ct);
        return snap.Documents.Select(FirestoreMapper.ToLead).ToList();
    }
}

public sealed class FirestoreCampaignRepository(FirestoreDb db) : ICampaignRepository
{
    private CollectionReference Campaigns => db.Collection("campaigns");
    private CollectionReference Slugs => db.Collection("campaignSlugs");
    private DocumentReference Counter => db.Collection("counters").Document("campaigns");

    public async Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken ct = default)
    {
        var snap = await Campaigns.GetSnapshotAsync(ct);
        return snap.Documents.Select(FirestoreMapper.ToCampaign)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToList();
    }

    public async Task<Campaign?> GetAsync(string id, CancellationToken ct = default)
    {
        var snap = await Campaigns.Document(id).GetSnapshotAsync(ct);
        return snap.Exists ? FirestoreMapper.ToCampaign(snap) : null;
    }

    public async Task<Campaign?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var index = await Slugs.Document(slug).GetSnapshotAsync(ct);
        return index.Exists && index.TryGetValue<string>("campaignId", out var id)
            ? await GetAsync(id, ct)
            : null;
    }

    public async Task<Campaign> CreateAsync(Campaign campaign, CancellationToken ct = default)
    {
        await db.RunTransactionAsync(async tx =>
        {
            var slugRef = Slugs.Document(campaign.Slug);
            var slug = await tx.GetSnapshotAsync(slugRef, ct);
            if (slug.Exists)
                throw new SlugConflictException(campaign.Slug);

            var counter = await tx.GetSnapshotAsync(Counter, ct);
            var next = (counter.Exists && counter.TryGetValue<long>("value", out var current) ? current : 0) + 1;

            campaign.Id = next.ToString(CultureInfo.InvariantCulture);
            tx.Set(Counter, new Dictionary<string, object> { ["value"] = next });
            tx.Create(Campaigns.Document(campaign.Id), FirestoreMapper.ToMap(campaign, includeCounters: true));
            tx.Create(slugRef, new Dictionary<string, object> { ["campaignId"] = campaign.Id });
        }, cancellationToken: ct);

        return campaign;
    }

    public async Task UpdateAsync(Campaign campaign, CancellationToken ct = default)
    {
        await db.RunTransactionAsync(async tx =>
        {
            var docRef = Campaigns.Document(campaign.Id);
            var existing = await tx.GetSnapshotAsync(docRef, ct);
            if (!existing.Exists)
                throw new InvalidOperationException($"Kampanja #{campaign.Id} ne postoji.");

            var oldSlug = existing.GetValue<string>("slug");
            if (oldSlug != campaign.Slug)
            {
                var newSlugRef = Slugs.Document(campaign.Slug);
                var newSlug = await tx.GetSnapshotAsync(newSlugRef, ct);
                if (newSlug.Exists)
                    throw new SlugConflictException(campaign.Slug);

                tx.Delete(Slugs.Document(oldSlug));
                tx.Create(newSlugRef, new Dictionary<string, object> { ["campaignId"] = campaign.Id });
            }

            tx.Update(docRef, FirestoreMapper.ToMap(campaign, includeCounters: false)!);
        }, cancellationToken: ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await db.RunTransactionAsync(async tx =>
        {
            var docRef = Campaigns.Document(id);
            var existing = await tx.GetSnapshotAsync(docRef, ct);
            if (!existing.Exists)
                return;

            tx.Delete(Slugs.Document(existing.GetValue<string>("slug")));
            tx.Delete(docRef);
        }, cancellationToken: ct);
    }

    public Task IncrementViewsAsync(string id, CancellationToken ct = default) =>
        Campaigns.Document(id).UpdateAsync("viewsCount", FieldValue.Increment(1), cancellationToken: ct);

    public Task IncrementLeadsAsync(string id, CancellationToken ct = default) =>
        Campaigns.Document(id).UpdateAsync("leadsCount", FieldValue.Increment(1), cancellationToken: ct);
}

public sealed class FirestoreAdminUserRepository(FirestoreDb db) : IAdminUserRepository
{
    private CollectionReference Admins => db.Collection("admins");

    public async Task<AdminUser?> GetAsync(string email, CancellationToken ct = default)
    {
        var snap = await Admins.Document(email.ToLowerInvariant()).GetSnapshotAsync(ct);
        return snap.Exists ? FirestoreMapper.ToAdmin(snap) : null;
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        var snap = await Admins.Limit(1).GetSnapshotAsync(ct);
        return snap.Count > 0;
    }

    public Task SaveAsync(AdminUser user, CancellationToken ct = default) =>
        Admins.Document(user.Email.ToLowerInvariant()).SetAsync(FirestoreMapper.ToMap(user), cancellationToken: ct);
}
