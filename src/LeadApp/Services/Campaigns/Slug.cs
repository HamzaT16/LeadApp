using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LeadApp.Services.Campaigns;

/// <summary>Campaign URL identifiers (<c>urlId</c>): lower-case ASCII letters, digits and single dashes.</summary>
public static partial class Slug
{
    public const int MaxLength = 255;

    public static string FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        // đ/Đ do not decompose under NFD, so map them explicitly.
        var text = name.Trim().ToLowerInvariant().Replace('đ', 'd');
        var normalized = text.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(ch is >= 'a' and <= 'z' or >= '0' and <= '9' ? ch : '-');
        }

        var slug = MultiDash().Replace(sb.ToString(), "-").Trim('-');
        return slug.Length > MaxLength ? slug[..MaxLength].TrimEnd('-') : slug;
    }

    public static bool IsValid(string? slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= MaxLength && Pattern().IsMatch(slug);

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiDash();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Pattern();
}
