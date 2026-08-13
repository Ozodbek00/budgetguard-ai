using System.Globalization;

namespace BudgetGuard.Infrastructure.Files;

/// <summary>
/// The input schema, and the tolerant parsing that maps real files onto it.
/// <para>
/// Procurement portals export wildly inconsistent files: different column
/// names, different date orders, amounts with thousands separators and currency
/// symbols. Being strict about the shape while being tolerant about the spelling
/// is what makes the difference between a tool an auditor can use on their own
/// export and one that only works on data prepared for it.
/// </para>
/// </summary>
public static class DatasetSchema
{
    /// <summary>Accepted header spellings for each field, matched case-insensitively.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ColumnAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ExternalReference"] =
                ["externalreference", "reference", "ref", "contractnumber", "contractno",
                 "contractid", "paymentreference", "transactionid", "id"],

            ["TransactionDate"] =
                ["transactiondate", "date", "paymentdate", "awarddate", "contractdate",
                 "signeddate", "period"],

            ["Amount"] =
                ["amount", "value", "sum", "total", "contractvalue", "paymentamount", "price"],

            ["Currency"] =
                ["currency", "currencycode", "ccy"],

            ["VendorName"] =
                ["vendorname", "vendor", "supplier", "suppliername", "contractor",
                 "contractorname", "counterparty", "payee"],

            ["Category"] =
                ["category", "spendcategory", "procurementcategory", "goodscategory",
                 "classification", "type"],

            ["Department"] =
                ["department", "agency", "ministry", "buyer", "buyername", "organisation",
                 "organization", "entity", "procuringentity"],

            ["Description"] =
                ["description", "details", "subject", "purpose", "goods", "item", "notes"]
        };

    /// <summary>Fields a file must provide. Without these the detectors have nothing to group on.</summary>
    public static readonly string[] RequiredColumns =
        ["TransactionDate", "Amount", "VendorName", "Category", "Department"];

    /// <summary>Fields that are filled with a sensible default when absent.</summary>
    public static readonly string[] OptionalColumns =
        ["ExternalReference", "Currency", "Description"];

    /// <summary>
    /// Date formats tried in order, before falling back to a general parse.
    /// <para>
    /// Day-first orderings are tried before month-first because the target
    /// users are Uzbek and wider CIS agencies, where dd.MM.yyyy is standard.
    /// This is a genuine ambiguity — 03/04/2025 is a different day under each
    /// convention — so the choice is stated here and in docs/DATA_MODEL.md
    /// rather than left implicit.
    /// </para>
    /// </summary>
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
        "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy",
        "MM/dd/yyyy", "MM-dd-yyyy",
        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss",
        "d.M.yyyy", "d/M/yyyy"
    ];

    /// <summary>Resolves a file's headers onto schema field names.</summary>
    /// <param name="headers">Header cells as they appear in the file.</param>
    /// <returns>Field name to zero-based column index.</returns>
    public static Dictionary<string, int> MapHeaders(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            var normalised = Normalise(headers[i]);

            if (normalised.Length == 0)
            {
                continue;
            }

            foreach (var (field, aliases) in ColumnAliases)
            {
                // First matching column wins, so a file with both "Vendor" and
                // "VendorName" resolves predictably to the leftmost.
                if (!map.ContainsKey(field) && aliases.Contains(normalised))
                {
                    map[field] = i;
                    break;
                }
            }
        }

        return map;
    }

    /// <summary>Strips spaces, underscores and punctuation so "Vendor Name" matches "vendorname".</summary>
    private static string Normalise(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>
    /// Parses a monetary amount, tolerating currency symbols, spaces and either
    /// separator convention.
    /// <para>
    /// When both "." and "," appear, whichever comes last is treated as the
    /// decimal separator, which resolves 1.234,56 and 1,234.56 correctly. A
    /// lone comma is read as a decimal separator only when it is not in a
    /// thousands position.
    /// </para>
    /// </summary>
    public static bool TryParseAmount(string? raw, out decimal amount)
    {
        amount = 0m;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = new string(raw
            .Where(c => char.IsDigit(c) || c is '.' or ',' or '-' or '+')
            .ToArray());

        if (cleaned.Length == 0)
        {
            return false;
        }

        var lastDot = cleaned.LastIndexOf('.');
        var lastComma = cleaned.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
        {
            cleaned = lastComma > lastDot
                ? cleaned.Replace(".", string.Empty).Replace(',', '.')
                : cleaned.Replace(",", string.Empty);
        }
        else if (lastComma >= 0)
        {
            // "1,234" is thousands; "1,23" is a decimal comma.
            var digitsAfter = cleaned.Length - lastComma - 1;

            cleaned = digitsAfter == 3
                ? cleaned.Replace(",", string.Empty)
                : cleaned.Replace(',', '.');
        }

        return decimal.TryParse(
            cleaned,
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out amount);
    }

    /// <summary>Parses a date, trying explicit formats before a general parse.</summary>
    public static bool TryParseDate(string? raw, out DateOnly date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();

        if (DateTime.TryParseExact(
                trimmed, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
        {
            date = DateOnly.FromDateTime(exact);
            return true;
        }

        // Excel keeps dates as day serial numbers; a cell read as text may
        // arrive that way.
        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial)
            && serial is > 0 and < 100_000)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return true;
        }

        if (DateTime.TryParse(
                trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose))
        {
            date = DateOnly.FromDateTime(loose);
            return true;
        }

        return false;
    }
}
