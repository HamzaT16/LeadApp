using LeadApp.Domain;

namespace LeadApp.Services.Storage;

public interface ILeadRepository
{
    /// <summary>Stores a new lead and assigns it the next sequential <see cref="Lead.Number"/>.</summary>
    Task<Lead> AddAsync(Lead lead, CancellationToken ct = default);
    Task UpdateCrmResultAsync(Lead lead, CancellationToken ct = default);
    Task<Lead?> GetAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Lead>> GetAllAsync(CancellationToken ct = default);
}

public interface ICampaignRepository
{
    Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken ct = default);
    Task<Campaign?> GetAsync(string id, CancellationToken ct = default);
    Task<Campaign?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <exception cref="SlugConflictException">The slug is already used by another campaign.</exception>
    Task<Campaign> CreateAsync(Campaign campaign, CancellationToken ct = default);

    /// <summary>Updates editable fields; counters are left untouched.</summary>
    /// <exception cref="SlugConflictException">The slug is already used by another campaign.</exception>
    Task UpdateAsync(Campaign campaign, CancellationToken ct = default);

    Task DeleteAsync(string id, CancellationToken ct = default);
    Task IncrementViewsAsync(string id, CancellationToken ct = default);
    Task IncrementLeadsAsync(string id, CancellationToken ct = default);
}

public interface IAdminUserRepository
{
    Task<AdminUser?> GetAsync(string email, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task SaveAsync(AdminUser user, CancellationToken ct = default);
}

public sealed class SlugConflictException(string slug)
    : Exception($"URL identifikator '{slug}' je već zauzet.")
{
    public string Slug { get; } = slug;
}
