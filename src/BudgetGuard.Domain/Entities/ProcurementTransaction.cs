namespace BudgetGuard.Domain.Entities;

/// <summary>
/// A single line of government procurement or budget spending: one payment,
/// from one department, to one vendor, in one spending category.
/// <para>
/// This is the atomic unit every detector operates on. It is intentionally a
/// flat record rather than a deep object graph — forensic analysis is
/// column-oriented, and a flat shape maps cleanly onto the CSV/Excel exports
/// that procurement portals actually publish.
/// </para>
/// </summary>
public sealed class ProcurementTransaction
{
    /// <summary>Surrogate key assigned on ingest.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The dataset this transaction was uploaded as part of.</summary>
    public Guid DatasetId { get; set; }

    /// <summary>
    /// The identifier used by the source system (contract number, payment
    /// reference). Preserved verbatim so an auditor can trace a flag back to
    /// the original record — this is what makes a finding actionable.
    /// </summary>
    public string ExternalReference { get; set; } = string.Empty;

    /// <summary>Date the payment or contract award was recorded.</summary>
    public DateOnly TransactionDate { get; set; }

    /// <summary>
    /// Payment amount in <see cref="Currency"/>. Stored as <see cref="decimal"/>
    /// rather than double: these are money values, and Benford analysis reads
    /// the literal decimal digits, so binary floating-point rounding would
    /// corrupt the leading digit of edge-case values.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>ISO-4217-style currency code. Defaults to UZS for Uzbek data.</summary>
    public string Currency { get; set; } = "UZS";

    /// <summary>Supplier receiving the payment. Used as a grouping key.</summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>Spending category (e.g. "Construction", "Medical Supplies").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Government body making the payment.</summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>Free-text description of goods or services procured.</summary>
    public string Description { get; set; } = string.Empty;
}
