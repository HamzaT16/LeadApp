using ClosedXML.Excel;
using LeadApp.Domain;
using LeadApp.Services.Locations;

namespace LeadApp.Services.Export;

public static class CellSanitizer
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>True when Excel (or another spreadsheet) could interpret the value as a formula.</summary>
    public static bool IsFormulaLike(string? value) =>
        !string.IsNullOrEmpty(value) && FormulaPrefixes.Contains(value[0]);

    /// <summary>Neutralises formula-like text with a leading apostrophe (for CSV/plain-text exports).</summary>
    public static string Sanitize(string? value) =>
        IsFormulaLike(value) ? "'" + value : value ?? "";
}

public sealed class LeadExcelExporter(ILocationService locations, AppClock clock)
{
    private static readonly string[] Headers =
    [
        "Lead #", "Datum zaprimanja", "Klijent", "Tip lica", "Telefon", "E-mail", "Općina", "City ID",
        "Mjesna zajednica", "MZ ID", "Iznos", "Svrha", "Izvor (sourceCode)", "Kampanja", "urlId",
        "CRM status", "CRM prilika ID", "CRM matchResult", "CRM greška"
    ];

    public byte[] Export(IEnumerable<Lead> leads)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Leadovi");

        for (var c = 0; c < Headers.Length; c++)
            sheet.Cell(1, c + 1).Value = Headers[c];

        var header = sheet.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0066b2");

        var row = 2;
        foreach (var l in leads)
        {
            var col = 1;
            sheet.Cell(row, col++).Value = l.Number;
            sheet.Cell(row, col).Value = clock.ToLocal(l.CreatedAtUtc);
            sheet.Cell(row, col++).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
            Text(sheet.Cell(row, col++), l.FullName);
            Text(sheet.Cell(row, col++), DisplayNames.Of(l.CustomerType));
            Text(sheet.Cell(row, col++), l.PhoneNumber);
            Text(sheet.Cell(row, col++), l.Email);
            Text(sheet.Cell(row, col++), locations.FindMunicipality(l.CityId)?.Name);
            sheet.Cell(row, col++).Value = l.CityId;
            Text(sheet.Cell(row, col++), locations.FindLocalCommunity(l.LocalCommunityId)?.Name);
            sheet.Cell(row, col++).Value = l.LocalCommunityId;
            sheet.Cell(row, col).Value = l.RequestedAmount;
            sheet.Cell(row, col++).Style.NumberFormat.Format = "#,##0.00";
            Text(sheet.Cell(row, col++), DisplayNames.Of(l.Purpose));
            Text(sheet.Cell(row, col++), l.SourceCode);
            Text(sheet.Cell(row, col++), l.CampaignName ?? "—");
            Text(sheet.Cell(row, col++), l.CampaignUrlId);
            Text(sheet.Cell(row, col++), DisplayNames.Of(l.CrmStatus));
            if (l.CrmSalesOpportunityId is { } oppId) sheet.Cell(row, col).Value = oppId;
            col++;
            Text(sheet.Cell(row, col++), l.CrmMatchResult);
            Text(sheet.Cell(row, col), l.CrmError);
            row++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(row - 1, 1), Headers.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents(minWidth: 10, maxWidth: 60);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // User-entered text is always written as a string cell (never a formula). Formula-like values
    // additionally get Excel's quote prefix, which keeps them literal even after a user edits the cell.
    private static void Text(IXLCell cell, string? value)
    {
        cell.Value = value ?? "";
        if (CellSanitizer.IsFormulaLike(value))
            cell.Style.IncludeQuotePrefix = true;
    }
}
