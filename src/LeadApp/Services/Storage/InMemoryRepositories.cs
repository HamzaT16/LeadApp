using System.Globalization;
using LeadApp.Domain;

namespace LeadApp.Services.Storage;

// Development-only stores used when no Firebase credentials are configured.
// Data is lost on restart. Objects are cloned in and out to behave like a real database.

public sealed class InMemoryLeadRepository : ILeadRepository
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Lead> _leads = [];
    private long _counter;

    public Task<Lead> AddAsync(Lead lead, CancellationToken ct = default)
    {
        lock (_lock)
        {
            lead.Number = ++_counter;
            lead.Id = lead.Number.ToString(CultureInfo.InvariantCulture);
            _leads[lead.Id] = lead.Clone();
        }
        return Task.FromResult(lead);
    }

    public Task UpdateCrmResultAsync(Lead lead, CancellationToken ct = default)
    {
        lock (_lock)
            _leads[lead.Id] = lead.Clone();
        return Task.CompletedTask;
    }

    public Task<Lead?> GetAsync(string id, CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult(_leads.GetValueOrDefault(id)?.Clone());
    }

    public Task<IReadOnlyList<Lead>> GetAllAsync(CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<Lead>>(_leads.Values.OrderByDescending(l => l.Number).Select(l => l.Clone()).ToList());
    }
}

public sealed class InMemoryCampaignRepository : ICampaignRepository
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Campaign> _campaigns = [];
    private long _counter;

    public Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<Campaign>>(_campaigns.Values.OrderByDescending(c => c.CreatedAtUtc).Select(c => c.Clone()).ToList());
    }

    public Task<Campaign?> GetAsync(string id, CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult(_campaigns.GetValueOrDefault(id)?.Clone());
    }

    public Task<Campaign?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult(_campaigns.Values.FirstOrDefault(c => c.Slug == slug)?.Clone());
    }

    public Task<Campaign> CreateAsync(Campaign campaign, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_campaigns.Values.Any(c => c.Slug == campaign.Slug))
                throw new SlugConflictException(campaign.Slug);

            campaign.Id = (++_counter).ToString(CultureInfo.InvariantCulture);
            _campaigns[campaign.Id] = campaign.Clone();
        }
        return Task.FromResult(campaign);
    }

    public Task UpdateAsync(Campaign campaign, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_campaigns.TryGetValue(campaign.Id, out var existing))
                throw new InvalidOperationException($"Kampanja #{campaign.Id} ne postoji.");
            if (_campaigns.Values.Any(c => c.Slug == campaign.Slug && c.Id != campaign.Id))
                throw new SlugConflictException(campaign.Slug);

            var updated = campaign.Clone();
            updated.ViewsCount = existing.ViewsCount;
            updated.LeadsCount = existing.LeadsCount;
            updated.CreatedAtUtc = existing.CreatedAtUtc;
            _campaigns[campaign.Id] = updated;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        lock (_lock)
            _campaigns.Remove(id);
        return Task.CompletedTask;
    }

    public Task IncrementViewsAsync(string id, CancellationToken ct = default)
    {
        lock (_lock)
            if (_campaigns.TryGetValue(id, out var c)) c.ViewsCount++;
        return Task.CompletedTask;
    }

    public Task IncrementLeadsAsync(string id, CancellationToken ct = default)
    {
        lock (_lock)
            if (_campaigns.TryGetValue(id, out var c)) c.LeadsCount++;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryAdminUserRepository : IAdminUserRepository
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AdminUser> _admins = [];

    public Task<AdminUser?> GetAsync(string email, CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult(_admins.GetValueOrDefault(email.ToLowerInvariant())?.Clone());
    }

    public Task<bool> AnyAsync(CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult(_admins.Count > 0);
    }

    public Task SaveAsync(AdminUser user, CancellationToken ct = default)
    {
        lock (_lock)
            _admins[user.Email.ToLowerInvariant()] = user.Clone();
        return Task.CompletedTask;
    }
}
