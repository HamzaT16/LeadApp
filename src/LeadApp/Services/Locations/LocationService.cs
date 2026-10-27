using System.Globalization;

namespace LeadApp.Services.Locations;

/// <summary>Općina; <see cref="Id"/> is the CRM <c>cityId</c> (ROW_ID).</summary>
public sealed record Municipality(long Id, string Name);

/// <summary>Mjesna zajednica; <see cref="Id"/> is the CRM <c>localCommunityId</c> (ROW_ID).</summary>
public sealed record LocalCommunity(long Id, long MunicipalityId, string Name);

public interface ILocationService
{
    IReadOnlyList<Municipality> Municipalities { get; }
    IReadOnlyList<LocalCommunity> GetLocalCommunities(long municipalityId);
    Municipality? FindMunicipality(long id);
    LocalCommunity? FindLocalCommunity(long id);
    bool BelongsTo(long localCommunityId, long municipalityId);
}

/// <summary>
/// Loads opcina.csv and mjesna_zajednica.csv once (';' separated, UTF-8, header row).
/// </summary>
public sealed class LocationService : ILocationService
{
    private static readonly StringComparer NameComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("bs-Latn-BA"), ignoreCase: true);

    private readonly Dictionary<long, Municipality> _municipalities;
    private readonly Dictionary<long, LocalCommunity> _localCommunities;
    private readonly Dictionary<long, IReadOnlyList<LocalCommunity>> _byMunicipality;

    public LocationService(string municipalitiesCsv, string localCommunitiesCsv)
    {
        var municipalities = ParseMunicipalities(municipalitiesCsv);
        var communities = ParseLocalCommunities(localCommunitiesCsv);

        _municipalities = municipalities.ToDictionary(m => m.Id);
        _localCommunities = communities.ToDictionary(c => c.Id);
        _byMunicipality = communities
            .GroupBy(c => c.MunicipalityId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<LocalCommunity>)g.OrderBy(c => c.Name, NameComparer).ToList());

        Municipalities = municipalities.OrderBy(m => m.Name, NameComparer).ToList();
    }

    public static LocationService FromFiles(string municipalitiesPath, string localCommunitiesPath) =>
        new(File.ReadAllText(municipalitiesPath), File.ReadAllText(localCommunitiesPath));

    public IReadOnlyList<Municipality> Municipalities { get; }

    public IReadOnlyList<LocalCommunity> GetLocalCommunities(long municipalityId) =>
        _byMunicipality.TryGetValue(municipalityId, out var list) ? list : [];

    public Municipality? FindMunicipality(long id) => _municipalities.GetValueOrDefault(id);

    public LocalCommunity? FindLocalCommunity(long id) => _localCommunities.GetValueOrDefault(id);

    public bool BelongsTo(long localCommunityId, long municipalityId) =>
        _localCommunities.TryGetValue(localCommunityId, out var c) && c.MunicipalityId == municipalityId;

    internal static List<Municipality> ParseMunicipalities(string csv) =>
        ReadRows(csv, minColumns: 2)
            .Select(cols => new Municipality(long.Parse(cols[0], CultureInfo.InvariantCulture), cols[1]))
            .ToList();

    internal static List<LocalCommunity> ParseLocalCommunities(string csv) =>
        ReadRows(csv, minColumns: 3)
            .Select(cols => new LocalCommunity(
                long.Parse(cols[0], CultureInfo.InvariantCulture),
                long.Parse(cols[1], CultureInfo.InvariantCulture),
                cols[2]))
            .ToList();

    // The source files use mixed "\r\r\n" line endings and a BOM, so split on any line break and skip blanks.
    private static IEnumerable<string[]> ReadRows(string csv, int minColumns) =>
        csv.TrimStart('﻿')
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1) // header
            .Select(line => line.Split(';', StringSplitOptions.TrimEntries))
            .Where(cols => cols.Length >= minColumns && long.TryParse(cols[0], out _));
}
