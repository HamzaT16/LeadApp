using LeadApp.Services.Locations;

namespace LeadApp.Tests;

public class LocationServiceTests
{
    [Fact]
    public void Parses_bom_blank_lines_and_diacritics()
    {
        var locations = TestData.Locations();

        Assert.Equal(3, locations.Municipalities.Count);
        Assert.Equal("Kneževo", locations.FindMunicipality(97)?.Name);
        Assert.Equal("Alipašino Polje A", locations.FindLocalCommunity(101)?.Name);
    }

    [Fact]
    public void Sorts_by_name_and_groups_communities_by_municipality()
    {
        var locations = TestData.Locations();

        Assert.Equal(["Kiseljak", "Kneževo", "Novi Grad Sarajevo"], locations.Municipalities.Select(m => m.Name));
        Assert.Equal(["Gromiljak", "Kiseljak"], locations.GetLocalCommunities(99).Select(c => c.Name));
        Assert.Empty(locations.GetLocalCommunities(97));
    }

    [Fact]
    public void BelongsTo_checks_the_parent_municipality()
    {
        var locations = TestData.Locations();

        Assert.True(locations.BelongsTo(2747, 99));
        Assert.False(locations.BelongsTo(2747, 1));
        Assert.False(locations.BelongsTo(9999, 99));
    }

    [Fact]
    public void Loads_the_real_data_files()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        var locations = LocationService.FromFiles(Path.Combine(dataDir, "opcina.csv"), Path.Combine(dataDir, "mjesna_zajednica.csv"));

        Assert.Equal(139, locations.Municipalities.Count);
        Assert.Contains(locations.Municipalities, m => m.Name == "Kneževo");
        Assert.All(locations.Municipalities.Take(10), m => Assert.NotEmpty(locations.GetLocalCommunities(m.Id)));
    }
}
