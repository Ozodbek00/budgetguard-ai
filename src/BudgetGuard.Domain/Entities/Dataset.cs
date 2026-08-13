namespace BudgetGuard.Domain.Entities;

/// <summary>
/// One uploaded body of spending data, analysed as a unit.
/// <para>
/// Analysis is always scoped to a dataset because every statistical baseline
/// used here (Benford expectation, group means, vendor shares) is relative to
/// the population it is computed over. Mixing two unrelated procurement
/// exports into one population would produce meaningless flags.
/// </para>
/// </summary>
public sealed class Dataset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-facing name, shown in the UI dataset picker.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Original filename as uploaded, for provenance.</summary>
    public string SourceFileName { get; set; } = string.Empty;

    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// True when this dataset came from the built-in synthetic generator.
    /// <para>
    /// Surfaced prominently in the UI and API. Demo data contains deliberately
    /// planted anomalies and must never be presented as real government data.
    /// </para>
    /// </summary>
    public bool IsSyntheticDemo { get; set; }

    /// <summary>Number of transaction rows accepted during ingest.</summary>
    public int RowCount { get; set; }

    public List<ProcurementTransaction> Transactions { get; set; } = [];
}
