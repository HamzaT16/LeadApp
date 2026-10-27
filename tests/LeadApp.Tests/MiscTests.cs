using ClosedXML.Excel;
using LeadApp.Domain;
using LeadApp.Options;
using LeadApp.Services.Auth;
using LeadApp.Services.Campaigns;
using LeadApp.Services.Export;
using LeadApp.Services.Leads;
using LeadApp.Services.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeadApp.Tests;

public class SlugTests
{
    [Theory]
    [InlineData("Online gotovinski kredit — oktobar 2026", "online-gotovinski-kredit-oktobar-2026")]
    [InlineData("  Đurđevdan Čevapi Šuma Žito ćevap ", "durdevdan-cevapi-suma-zito-cevap")]
    [InlineData("Agro!!! Viber__2026", "agro-viber-2026")]
    [InlineData("", "")]
    public void FromName_produces_url_safe_slugs(string name, string expected) =>
        Assert.Equal(expected, Slug.FromName(name));

    [Theory]
    [InlineData("agro-viber-2026", true)]
    [InlineData("Agro", false)]
    [InlineData("-agro", false)]
    [InlineData("agro--viber", false)]
    [InlineData("agro viber", false)]
    public void IsValid(string slug, bool expected) => Assert.Equal(expected, Slug.IsValid(slug));
}

public class ExcelExportTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", true)]
    [InlineData("+38761123456", true)]
    [InlineData("-5", true)]
    [InlineData("@SUM(A1)", true)]
    [InlineData("Marko", false)]
    [InlineData(null, false)]
    public void Detects_formula_like_values(string? value, bool expected) =>
        Assert.Equal(expected, CellSanitizer.IsFormulaLike(value));

    [Fact]
    public void Sanitize_prefixes_an_apostrophe() =>
        Assert.Equal("'=1+1", CellSanitizer.Sanitize("=1+1"));

    [Fact]
    public void Exported_formula_text_stays_a_literal_string()
    {
        var exporter = new LeadExcelExporter(TestData.Locations(), TestData.Clock());
        var lead = new Lead { Number = 1, FullName = "=1+1", CityId = 99, LocalCommunityId = 2747, RequestedAmount = 500, SourceCode = "EMAIL", CreatedAtUtc = DateTime.UtcNow };

        using var workbook = new XLWorkbook(new MemoryStream(exporter.Export([lead])));
        var cell = workbook.Worksheet(1).Cell(2, 3);

        Assert.False(cell.HasFormula);
        Assert.Equal("=1+1", cell.GetString());
        Assert.True(cell.Style.IncludeQuotePrefix);
        Assert.Equal("Kiseljak", workbook.Worksheet(1).Cell(2, 7).GetString());
    }
}

public class LeadFilterTests
{
    [Fact]
    public void Filters_by_search_status_and_dates_and_round_trips_through_the_query_string()
    {
        var clock = TestData.Clock();
        var leads = new[]
        {
            new Lead { Number = 1, FullName = "Ana Anić", CrmStatus = CrmSyncStatus.Synced, CreatedAtUtc = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc) },
            new Lead { Number = 2, FullName = "Marko Marković", CrmStatus = CrmSyncStatus.Failed, CreatedAtUtc = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc) },
            new Lead { Number = 3, FullName = "Mara Marić", CrmStatus = CrmSyncStatus.Synced, CreatedAtUtc = new DateTime(2026, 10, 26, 23, 30, 0, DateTimeKind.Utc) } // 00:30 on 27.10. local (UTC+1 after DST ends)
        };

        var filter = new LeadFilter { Search = "mar", To = new DateOnly(2026, 10, 26) };
        Assert.Equal([2], filter.Apply(leads, clock).Select(l => l.Number));

        var parsed = LeadFilter.FromQuery(new Microsoft.AspNetCore.Http.QueryCollection(
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new LeadFilter { Status = CrmSyncStatus.Synced, From = new DateOnly(2026, 10, 21) }.ToQueryString())));
        Assert.Equal([3], parsed.Apply(leads, clock).Select(l => l.Number));
    }
}

public class AdminAuthServiceTests
{
    [Fact]
    public async Task Locks_out_after_repeated_failures_and_accepts_correct_password()
    {
        var repo = new InMemoryAdminUserRepository();
        var hasher = new PasswordHasher<AdminUser>();
        var user = new AdminUser { Email = "admin@partner.ba" };
        user.PasswordHash = hasher.HashPassword(user, "correct horse battery");
        await repo.SaveAsync(user);

        var time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero));
        var auth = new AdminAuthService(repo, hasher, MsOptions.Create(new AdminSeedOptions()), new LeadApp.Services.AppClock(time), NullLogger<AdminAuthService>.Instance);

        Assert.Equal(LoginOutcome.Success, (await auth.LoginAsync("Admin@Partner.ba", "correct horse battery")).Outcome);

        for (var i = 0; i < 4; i++)
            Assert.Equal(LoginOutcome.InvalidCredentials, (await auth.LoginAsync("admin@partner.ba", "wrong")).Outcome);
        Assert.Equal(LoginOutcome.LockedOut, (await auth.LoginAsync("admin@partner.ba", "wrong")).Outcome);
        Assert.Equal(LoginOutcome.LockedOut, (await auth.LoginAsync("admin@partner.ba", "correct horse battery")).Outcome);

        time.Now = time.Now.AddMinutes(16);
        var result = await auth.LoginAsync("admin@partner.ba", "correct horse battery");
        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.True(result.Principal!.IsInRole(AdminRoles.Admin));
    }

    [Fact]
    public async Task Unknown_user_gets_the_same_answer_as_a_wrong_password()
    {
        var auth = new AdminAuthService(new InMemoryAdminUserRepository(), new PasswordHasher<AdminUser>(),
            MsOptions.Create(new AdminSeedOptions()), TestData.Clock(), NullLogger<AdminAuthService>.Instance);

        Assert.Equal(LoginOutcome.InvalidCredentials, (await auth.LoginAsync("nobody@partner.ba", "x")).Outcome);
    }
}
