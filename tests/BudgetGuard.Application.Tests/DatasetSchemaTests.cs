using BudgetGuard.Infrastructure.Files;

namespace BudgetGuard.Application.Tests;

/// <summary>
/// Parsing rules for real-world files.
/// <para>
/// These matter more than they look. A misread amount does not throw — it
/// silently changes a leading digit, a peer-group mean and a vendor's share of
/// spend, producing a confident and wrong report.
/// </para>
/// </summary>
public sealed class DatasetSchemaTests
{
    // -----------------------------------------------------------------
    // Amounts
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1234", 1234)]
    [InlineData("0.5", 0.5)]
    [InlineData("-250", -250)]
    public void Plain_numbers_parse(string raw, decimal expected)
    {
        Assert.True(DatasetSchema.TryParseAmount(raw, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    // Anglophone convention: comma groups, dot decimal.
    [InlineData("1,234.56", 1234.56)]
    [InlineData("12,345,678.90", 12345678.90)]
    // Continental/CIS convention: dot groups, comma decimal.
    [InlineData("1.234,56", 1234.56)]
    [InlineData("12.345.678,90", 12345678.90)]
    public void Both_separator_conventions_are_resolved_by_which_symbol_comes_last(
        string raw, decimal expected)
    {
        Assert.True(DatasetSchema.TryParseAmount(raw, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("1,234", 1234)]      // three digits after the comma: thousands
    [InlineData("1,23", 1.23)]       // two digits: a decimal comma
    [InlineData("1,2", 1.2)]
    public void A_lone_comma_is_thousands_only_in_a_thousands_position(string raw, decimal expected)
    {
        Assert.True(DatasetSchema.TryParseAmount(raw, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("48 750 000 UZS", 48750000)]
    [InlineData("$1,500.00", 1500.00)]
    [InlineData("  2500  ", 2500)]
    public void Currency_symbols_codes_and_spacing_are_tolerated(string raw, decimal expected)
    {
        Assert.True(DatasetSchema.TryParseAmount(raw, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("n/a")]
    [InlineData("pending")]
    public void Unreadable_amounts_are_rejected_rather_than_defaulted_to_zero(string? raw)
    {
        // Silently substituting zero would corrupt every statistic computed
        // over the column; the row must be skipped and reported instead.
        Assert.False(DatasetSchema.TryParseAmount(raw, out _));
    }

    // -----------------------------------------------------------------
    // Dates
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("2025-03-14", 2025, 3, 14)]
    [InlineData("2025/03/14", 2025, 3, 14)]
    [InlineData("14.03.2025", 2025, 3, 14)]
    [InlineData("14/03/2025", 2025, 3, 14)]
    [InlineData("2025-03-14T09:30:00", 2025, 3, 14)]
    public void Common_date_formats_parse(string raw, int year, int month, int day)
    {
        Assert.True(DatasetSchema.TryParseDate(raw, out var date));
        Assert.Equal(new DateOnly(year, month, day), date);
    }

    [Fact]
    public void Ambiguous_slash_dates_are_read_day_first()
    {
        // 03/04/2025 is a different day under each convention. The target users
        // are Uzbek and wider CIS agencies, where dd.MM.yyyy is standard, so
        // day-first wins — a documented choice, asserted here so it cannot be
        // changed silently. See docs/DATA_MODEL.md.
        Assert.True(DatasetSchema.TryParseDate("03/04/2025", out var date));
        Assert.Equal(new DateOnly(2025, 4, 3), date);
    }

    [Fact]
    public void Excel_date_serial_numbers_are_handled()
    {
        // Excel day serial 45730 = 2025-03-14.
        Assert.True(DatasetSchema.TryParseDate("45730", out var date));
        Assert.Equal(new DateOnly(2025, 3, 14), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData(null)]
    public void Unreadable_dates_are_rejected(string? raw) =>
        Assert.False(DatasetSchema.TryParseDate(raw, out _));

    // -----------------------------------------------------------------
    // Header mapping
    // -----------------------------------------------------------------

    [Fact]
    public void Canonical_headers_map()
    {
        var map = DatasetSchema.MapHeaders(
            ["ExternalReference", "TransactionDate", "Amount", "Currency",
             "VendorName", "Category", "Department", "Description"]);

        Assert.Equal(8, map.Count);
        Assert.Equal(2, map["Amount"]);
        Assert.Equal(4, map["VendorName"]);
    }

    [Theory]
    [InlineData("Supplier", "VendorName")]
    [InlineData("Contractor Name", "VendorName")]
    [InlineData("Ministry", "Department")]
    [InlineData("Procuring Entity", "Department")]
    [InlineData("Contract Value", "Amount")]
    [InlineData("Payment Date", "TransactionDate")]
    public void Alternative_header_spellings_resolve_to_schema_fields(string header, string field)
    {
        var map = DatasetSchema.MapHeaders([header]);

        Assert.True(map.ContainsKey(field), $"\"{header}\" should map to {field}.");
    }

    [Theory]
    [InlineData("vendor_name")]
    [InlineData("VENDOR NAME")]
    [InlineData("Vendor-Name")]
    [InlineData("  VendorName  ")]
    public void Header_matching_ignores_case_spacing_and_punctuation(string header) =>
        Assert.True(DatasetSchema.MapHeaders([header]).ContainsKey("VendorName"));

    [Fact]
    public void When_two_headers_match_one_field_the_leftmost_wins()
    {
        var map = DatasetSchema.MapHeaders(["Vendor", "VendorName"]);

        Assert.Equal(0, map["VendorName"]);
    }

    [Fact]
    public void Unrecognised_headers_are_ignored_rather_than_failing_the_file()
    {
        var map = DatasetSchema.MapHeaders(["Amount", "SomeInternalCode", "VendorName"]);

        Assert.Equal(2, map.Count);
    }
}
