using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Application.Common.Interfaces;

/// <summary>
/// Outcome of parsing an uploaded file.
/// <para>
/// Row-level problems are collected rather than thrown. A real procurement
/// export routinely has a few malformed rows, and rejecting a 5,000-row file
/// because of three bad dates would make the tool unusable — but silently
/// dropping them would corrupt the statistics. So both the accepted rows and
/// the rejected ones are reported, and the UI shows the count.
/// </para>
/// </summary>
/// <param name="Transactions">Rows that parsed cleanly.</param>
/// <param name="Errors">Fatal problems — a missing required column, an unreadable file.</param>
/// <param name="RowWarnings">Per-row problems that caused a row to be skipped.</param>
/// <param name="TotalRowsRead">Data rows seen, including skipped ones.</param>
public sealed record DatasetParseResult(
    IReadOnlyList<ProcurementTransaction> Transactions,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> RowWarnings,
    int TotalRowsRead)
{
    public bool IsSuccess => Errors.Count == 0 && Transactions.Count > 0;

    public int SkippedRowCount => TotalRowsRead - Transactions.Count;
}

/// <summary>Reads procurement transactions out of an uploaded CSV or Excel file.</summary>
public interface IDatasetFileParser
{
    /// <summary>File extensions this parser accepts, lowercase and dot-prefixed.</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>Parses an uploaded file into transactions plus any schema problems.</summary>
    /// <param name="stream">The uploaded file's content.</param>
    /// <param name="fileName">Original filename, used to pick CSV or Excel handling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DatasetParseResult> ParseAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);
}
