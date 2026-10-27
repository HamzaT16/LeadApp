global using MsOptions = Microsoft.Extensions.Options.Options;
using LeadApp.Options;
using LeadApp.Services;
using LeadApp.Services.Leads;
using LeadApp.Services.Locations;

namespace LeadApp.Tests;

internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class TestData
{
    // Same quirks as the real files: BOM, "\r\r\n" line endings, Bosnian diacritics.
    public const string MunicipalitiesCsv = "﻿id_op;naziv_opcine\r\r\n99;Kiseljak\r\r\n97;Kneževo\r\r\n1;Novi Grad Sarajevo\r\r\n";
    public const string CommunitiesCsv = "﻿id_mz;id_op;naziv_mjesne_zajednice\r\r\n2748;99;Kiseljak\r\r\n2747;99;Gromiljak\r\r\n101;1;Alipašino Polje A\r\r\n";

    public static LocationService Locations() => new(MunicipalitiesCsv, CommunitiesCsv);

    public static LeadFormOptions FormOptions() => new();

    public static AppClock Clock(DateTimeOffset? now = null) =>
        new(new ManualTimeProvider(now ?? new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero)));

    public static LeadFormModel ValidFl() => new()
    {
        FirstName = "Marko",
        LastName = "Marković",
        PhoneNumber = "061 123 456",
        Email = "marko@example.com",
        CityId = 99,
        LocalCommunityId = 2747,
        Amount = 5000,
        Consent = true
    };
}
