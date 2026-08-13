using System.Text;
using BudgetGuard.Application.Datasets.Commands.UploadDataset;
using BudgetGuard.Infrastructure.Files;
using ClosedXML.Excel;

namespace BudgetGuard.Application.Tests;

public sealed class DatasetFileParserTests
{
    private static readonly DatasetFileParser Parser = new();

    private static Stream Csv(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    private const string ValidCsv = """
        ExternalReference,TransactionDate,Amount,Currency,VendorName,Category,Department,Description
        BG-1,2025-03-14,48750000,UZS,Oltin Yo'l,Construction,Ministry of Transport,Road resurfacing
        BG-2,2025-03-16,1250000.50,UZS,Toshkent Tizimlar,IT Equipment,Ministry of Education,Laptops
        BG-3,2025-04-01,900000,UZS,Buxoro Logistika,Vehicle Fleet,Ministry of Health,Van hire
        """;

    // -----------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------

    [Fact]
    public async Task Valid_csv_parses_every_row()
    {
        var result = await Parser.ParseAsync(Csv(ValidCsv), "spending.csv");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Transactions.Count);
        Assert.Empty(result.Errors);
        Assert.Equal(0, result.SkippedRowCount);

        var first = result.Transactions[0];
        Assert.Equal("BG-1", first.ExternalReference);
        Assert.Equal(48_750_000m, first.Amount);
        Assert.Equal(new DateOnly(2025, 3, 14), first.TransactionDate);
        Assert.Equal("Oltin Yo'l", first.VendorName);
        Assert.Equal("Construction", first.Category);
        Assert.Equal("UZS", first.Currency);
    }

    [Fact]
    public async Task Semicolon_delimited_files_are_detected()
    {
        // Excel exports in much of Europe and the CIS use semicolons.
        const string csv = """
            TransactionDate;Amount;VendorName;Category;Department
            2025-03-14;48750000;Oltin Yo'l;Construction;Ministry of Transport
            2025-03-16;1250000;Buxoro Logistika;IT Equipment;Ministry of Education
            """;

        var result = await Parser.ParseAsync(Csv(csv), "spending.csv");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Transactions.Count);
    }

    [Fact]
    public async Task Alternative_column_names_are_accepted()
    {
        const string csv = """
            Contract No,Payment Date,Contract Value,Supplier,Spend Category,Procuring Entity
            C-1,14.03.2025,"1.234.567,89",Alfa Qurilish,Construction,Tashkent City Administration
            """;

        var result = await Parser.ParseAsync(Csv(csv), "export.csv");

        Assert.True(result.IsSuccess);

        var transaction = Assert.Single(result.Transactions);
        Assert.Equal("C-1", transaction.ExternalReference);
        Assert.Equal(1_234_567.89m, transaction.Amount);
        Assert.Equal(new DateOnly(2025, 3, 14), transaction.TransactionDate);
        Assert.Equal("Alfa Qurilish", transaction.VendorName);
    }

    [Fact]
    public async Task Optional_columns_receive_documented_defaults()
    {
        const string csv = """
            TransactionDate,Amount,VendorName,Category,Department
            2025-03-14,1000,Vendor A,Construction,Ministry A
            """;

        var result = await Parser.ParseAsync(Csv(csv), "minimal.csv");

        var transaction = Assert.Single(result.Transactions);
        Assert.Equal("UZS", transaction.Currency);
        Assert.Equal(string.Empty, transaction.Description);
        Assert.StartsWith("ROW-", transaction.ExternalReference);
    }

    // -----------------------------------------------------------------
    // Partial success: the behaviour that makes the tool usable on real files
    // -----------------------------------------------------------------

    [Fact]
    public async Task Unparseable_rows_are_skipped_and_reported_while_good_rows_survive()
    {
        const string csv = """
            TransactionDate,Amount,VendorName,Category,Department
            2025-03-14,1000,Vendor A,Construction,Ministry A
            not-a-date,2000,Vendor B,Construction,Ministry A
            2025-03-16,n/a,Vendor C,Construction,Ministry A
            2025-03-17,3000,,Construction,Ministry A
            2025-03-18,4000,Vendor E,Construction,Ministry A
            """;

        var result = await Parser.ParseAsync(Csv(csv), "messy.csv");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal(3, result.SkippedRowCount);
        Assert.Equal(3, result.RowWarnings.Count);

        // Warnings must name the file line so the auditor can go and fix it.
        Assert.Contains(result.RowWarnings, w => w.Contains("Row 3"));
        Assert.Contains(result.RowWarnings, w => w.Contains("Row 4"));
        Assert.Contains(result.RowWarnings, w => w.Contains("Row 5"));
    }

    [Fact]
    public async Task Blank_lines_are_ignored_without_being_counted_as_skipped()
    {
        const string csv = """
            TransactionDate,Amount,VendorName,Category,Department
            2025-03-14,1000,Vendor A,Construction,Ministry A

            2025-03-18,4000,Vendor E,Construction,Ministry A
            """;

        var result = await Parser.ParseAsync(Csv(csv), "gappy.csv");

        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal(0, result.SkippedRowCount);
    }

    // -----------------------------------------------------------------
    // Fatal problems
    // -----------------------------------------------------------------

    [Fact]
    public async Task Missing_required_column_is_fatal_and_the_error_names_it()
    {
        const string csv = """
            TransactionDate,Amount,VendorName,Category
            2025-03-14,1000,Vendor A,Construction
            """;

        var result = await Parser.ParseAsync(Csv(csv), "incomplete.csv");

        Assert.False(result.IsSuccess);

        var error = Assert.Single(result.Errors);
        Assert.Contains("Department", error);
        // The error must also say what the file did provide, or the user is guessing.
        Assert.Contains("VendorName", error);
    }

    [Fact]
    public async Task A_file_with_no_usable_rows_is_reported_as_a_failure()
    {
        const string csv = """
            TransactionDate,Amount,VendorName,Category,Department
            bad,bad,,Construction,Ministry A
            """;

        var result = await Parser.ParseAsync(Csv(csv), "empty.csv");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Unsupported_extensions_are_rejected_with_a_useful_message()
    {
        var result = await Parser.ParseAsync(Csv("anything"), "report.pdf");

        Assert.False(result.IsSuccess);
        Assert.Contains(".pdf", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task A_corrupt_file_produces_an_error_rather_than_throwing()
    {
        var garbage = new MemoryStream([0x00, 0x01, 0x02, 0x03, 0x04]);

        var result = await Parser.ParseAsync(garbage, "broken.xlsx");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    // -----------------------------------------------------------------
    // Excel
    // -----------------------------------------------------------------

    [Fact]
    public async Task Excel_files_parse_including_native_date_cells()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Spending");

        sheet.Cell(1, 1).Value = "TransactionDate";
        sheet.Cell(1, 2).Value = "Amount";
        sheet.Cell(1, 3).Value = "VendorName";
        sheet.Cell(1, 4).Value = "Category";
        sheet.Cell(1, 5).Value = "Department";

        // A real date cell, not a string — its display text would vary with the
        // workbook's regional formatting.
        sheet.Cell(2, 1).Value = new DateTime(2025, 3, 14);
        sheet.Cell(2, 2).Value = 48_750_000;
        sheet.Cell(2, 3).Value = "Oltin Yo'l";
        sheet.Cell(2, 4).Value = "Construction";
        sheet.Cell(2, 5).Value = "Ministry of Transport";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = await Parser.ParseAsync(stream, "spending.xlsx");

        Assert.True(result.IsSuccess);

        var transaction = Assert.Single(result.Transactions);
        Assert.Equal(new DateOnly(2025, 3, 14), transaction.TransactionDate);
        Assert.Equal(48_750_000m, transaction.Amount);
    }

    [Fact]
    public async Task Csv_and_equivalent_excel_produce_identical_transactions()
    {
        // Both formats run through one mapping routine precisely so that the
        // same numbers cannot yield two different anomaly reports.
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Spending");

        var headers = new[]
        {
            "ExternalReference", "TransactionDate", "Amount", "Currency",
            "VendorName", "Category", "Department", "Description"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var rows = ValidCsv.Split('\n').Skip(1).Select(r => r.Trim()).ToArray();

        for (var r = 0; r < rows.Length; r++)
        {
            var cells = rows[r].Split(',');
            for (var c = 0; c < cells.Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = cells[c];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fromExcel = await Parser.ParseAsync(stream, "spending.xlsx");
        var fromCsv = await Parser.ParseAsync(Csv(ValidCsv), "spending.csv");

        Assert.Equal(fromCsv.Transactions.Count, fromExcel.Transactions.Count);

        Assert.Equal(
            fromCsv.Transactions.Select(t => (t.ExternalReference, t.Amount, t.TransactionDate, t.VendorName)),
            fromExcel.Transactions.Select(t => (t.ExternalReference, t.Amount, t.TransactionDate, t.VendorName)));
    }

    // -----------------------------------------------------------------
    // Contract between the parser and the command validator
    // -----------------------------------------------------------------

    [Fact]
    public void Validator_accepts_exactly_the_extensions_the_parser_supports()
    {
        // These two lists live in different projects. If they drift, a user
        // either gets a confusing rejection for a file the parser can read, or
        // passes validation only to fail deeper in. This test is the seam.
        var validator = new UploadDatasetCommandValidator();

        foreach (var extension in Parser.SupportedExtensions)
        {
            var result = validator.Validate(
                new UploadDatasetCommand(Stream.Null, $"data{extension}"));

            Assert.True(result.IsValid, $"The validator rejected {extension}, which the parser supports.");
        }
    }
}
