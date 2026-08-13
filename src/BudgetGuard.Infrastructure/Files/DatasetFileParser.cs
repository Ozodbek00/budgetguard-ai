using System.Globalization;
using System.Text;
using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Domain.Entities;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace BudgetGuard.Infrastructure.Files;

/// <summary>
/// Reads procurement transactions from CSV and Excel uploads.
/// <para>
/// Both formats are reduced to the same shape — a header row plus rows of
/// strings — and then run through one row-mapping routine. Keeping a single
/// mapping path means a CSV and an equivalent .xlsx cannot be interpreted
/// differently, which would otherwise be an easy way to get two different
/// anomaly reports from the same numbers.
/// </para>
/// </summary>
public sealed class DatasetFileParser : IDatasetFileParser
{
    /// <summary>Cap on reported row warnings, so one badly broken file cannot flood the UI.</summary>
    private const int MaxReportedWarnings = 50;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".csv", ".xlsx"];

    /// <inheritdoc />
    public async Task<DatasetParseResult> ParseAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!SupportedExtensions.Contains(extension))
        {
            return Failure($"Unsupported file type \"{extension}\". " +
                           $"Supported types are {string.Join(" and ", SupportedExtensions)}.");
        }

        try
        {
            var (headers, rows) = extension == ".xlsx"
                ? ReadExcel(stream)
                : await ReadCsvAsync(stream, cancellationToken);

            return MapRows(headers, rows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure($"The file could not be read: {ex.Message}");
        }
    }

    private static DatasetParseResult Failure(string error) =>
        new([], [error], [], 0);

    private static async Task<(List<string> Headers, List<string[]> Rows)> ReadCsvAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            // Real exports use commas, semicolons or tabs. Let CsvHelper work
            // it out rather than rejecting two thirds of valid files.
            DetectDelimiter = true,
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
            IgnoreBlankLines = true
        };

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, configuration);

        var rows = new List<string[]>();

        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = (csv.HeaderRecord ?? []).ToList();

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new string[headers.Count];

            for (var i = 0; i < headers.Count; i++)
            {
                row[i] = csv.TryGetField<string>(i, out var value) ? value ?? string.Empty : string.Empty;
            }

            rows.Add(row);
        }

        return (headers, rows);
    }

    private static (List<string> Headers, List<string[]> Rows) ReadExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets.FirstOrDefault()
                        ?? throw new InvalidOperationException("The workbook contains no worksheets.");

        var range = worksheet.RangeUsed()
                    ?? throw new InvalidOperationException("The first worksheet is empty.");

        var allRows = range.RowsUsed().ToList();

        if (allRows.Count == 0)
        {
            throw new InvalidOperationException("The first worksheet contains no rows.");
        }

        var headerRow = allRows[0];
        var columnCount = headerRow.CellCount();

        var headers = Enumerable.Range(1, columnCount)
            .Select(i => headerRow.Cell(i).GetString().Trim())
            .ToList();

        var rows = new List<string[]>(allRows.Count - 1);

        foreach (var row in allRows.Skip(1))
        {
            var values = new string[headers.Count];

            for (var i = 0; i < headers.Count; i++)
            {
                var cell = row.Cell(i + 1);

                // A date cell must be read as a date, not as its display string,
                // which would vary with the workbook's regional formatting.
                values[i] = cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt)
                    ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : cell.GetString().Trim();
            }

            rows.Add(values);
        }

        return (headers, rows);
    }

    /// <summary>Maps raw rows onto transactions, collecting per-row problems as it goes.</summary>
    private static DatasetParseResult MapRows(List<string> headers, List<string[]> rows)
    {
        if (headers.Count == 0)
        {
            return Failure("The file has no header row. The first row must name the columns.");
        }

        var map = DatasetSchema.MapHeaders(headers);
        var missing = DatasetSchema.RequiredColumns.Where(c => !map.ContainsKey(c)).ToArray();

        if (missing.Length > 0)
        {
            return Failure(
                $"Missing required column(s): {string.Join(", ", missing)}. " +
                $"The file provided: {string.Join(", ", headers.Where(h => !string.IsNullOrWhiteSpace(h)))}. " +
                "Accepted alternative names are listed in docs/DATA_MODEL.md.");
        }

        var transactions = new List<ProcurementTransaction>(rows.Count);
        var warnings = new List<string>();

        for (var i = 0; i < rows.Count; i++)
        {
            // +2 puts the number in the user's terms: 1-based, past the header.
            var lineNumber = i + 2;
            var row = rows[i];

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var vendor = Value(row, map, "VendorName");
            var category = Value(row, map, "Category");
            var department = Value(row, map, "Department");

            if (string.IsNullOrWhiteSpace(vendor) ||
                string.IsNullOrWhiteSpace(category) ||
                string.IsNullOrWhiteSpace(department))
            {
                AddWarning(warnings,
                    $"Row {lineNumber}: skipped — vendor, category and department must all have values.");
                continue;
            }

            if (!DatasetSchema.TryParseAmount(Value(row, map, "Amount"), out var amount))
            {
                AddWarning(warnings,
                    $"Row {lineNumber}: skipped — could not read an amount from " +
                    $"\"{Value(row, map, "Amount")}\".");
                continue;
            }

            if (!DatasetSchema.TryParseDate(Value(row, map, "TransactionDate"), out var date))
            {
                AddWarning(warnings,
                    $"Row {lineNumber}: skipped — could not read a date from " +
                    $"\"{Value(row, map, "TransactionDate")}\".");
                continue;
            }

            var reference = Value(row, map, "ExternalReference");
            var currency = Value(row, map, "Currency");

            transactions.Add(new ProcurementTransaction
            {
                ExternalReference = string.IsNullOrWhiteSpace(reference)
                    ? $"ROW-{lineNumber:D5}"
                    : reference.Trim(),
                TransactionDate = date,
                Amount = amount,
                Currency = string.IsNullOrWhiteSpace(currency) ? "UZS" : currency.Trim().ToUpperInvariant(),
                VendorName = vendor.Trim(),
                Category = category.Trim(),
                Department = department.Trim(),
                Description = Value(row, map, "Description").Trim()
            });
        }

        var dataRowCount = rows.Count(r => !r.All(string.IsNullOrWhiteSpace));

        return transactions.Count == 0
            ? new DatasetParseResult(
                [],
                ["No rows could be read. " + (warnings.Count > 0 ? warnings[0] : string.Empty)],
                warnings,
                dataRowCount)
            : new DatasetParseResult(transactions, [], warnings, dataRowCount);
    }

    private static void AddWarning(List<string> warnings, string warning)
    {
        if (warnings.Count < MaxReportedWarnings)
        {
            warnings.Add(warning);
        }
        else if (warnings.Count == MaxReportedWarnings)
        {
            warnings.Add("Further row problems suppressed.");
        }
    }

    private static string Value(string[] row, IReadOnlyDictionary<string, int> map, string field) =>
        map.TryGetValue(field, out var index) && index < row.Length
            ? row[index] ?? string.Empty
            : string.Empty;
}
