using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Demo;

/// <summary>The kind of manipulation deliberately planted in a synthetic dataset.</summary>
public enum PlantedAnomalyKind
{
    /// <summary>Amounts clustered just under an approval ceiling, distorting first digits.</summary>
    ThresholdEvasion = 0,

    /// <summary>One vendor awarded an implausible share of a category's spend.</summary>
    VendorConcentration = 1,

    /// <summary>Individual payments far outside their peer group's range.</summary>
    ExtremeOutlier = 2,

    /// <summary>A vendor whose invoice amounts are manufactured round numbers.</summary>
    RoundNumberInvoicing = 3
}

/// <summary>
/// A manipulation planted in demo data, recorded so tests can assert the
/// detectors actually find it. This is the ground truth the test suite scores
/// the engine against.
/// </summary>
/// <param name="Kind">What was planted.</param>
/// <param name="SubjectKey">Vendor name, category, or transaction reference affected.</param>
/// <param name="Description">What an auditor should end up concluding.</param>
/// <param name="AffectedTransactions">How many rows carry the manipulation.</param>
public sealed record PlantedAnomaly(
    PlantedAnomalyKind Kind,
    string SubjectKey,
    string Description,
    int AffectedTransactions);

/// <summary>
/// Generated demo data plus the ground-truth list of what was planted in it.
/// <para>
/// This data is entirely fictional. Vendor names, departments and amounts are
/// invented. It must never be presented as real government procurement data.
/// </para>
/// </summary>
/// <param name="Transactions">The generated rows.</param>
/// <param name="PlantedAnomalies">Ground truth for tests and for the demo walkthrough.</param>
public sealed record SyntheticDataset(
    IReadOnlyList<ProcurementTransaction> Transactions,
    IReadOnlyList<PlantedAnomaly> PlantedAnomalies);

/// <summary>Knobs for the synthetic generator. Defaults produce the standard demo dataset.</summary>
public sealed class SyntheticDataOptions
{
    /// <summary>Seed for the pseudo-random generator. Fixed seed means reproducible demos and tests.</summary>
    public int Seed { get; set; } = 20260813;

    /// <summary>Number of ordinary, non-manipulated transactions to generate.</summary>
    public int CleanTransactionCount { get; set; } = 1400;

    /// <summary>Rows placed just below an approval ceiling to bend the first-digit curve.</summary>
    public int ThresholdEvasionCount { get; set; } = 190;

    /// <summary>Invoices issued by the round-number vendor.</summary>
    public int RoundNumberInvoiceCount { get; set; } = 80;

    /// <summary>Extra contracts funnelled to the concentrated vendor.</summary>
    public int ConcentrationContractCount { get; set; } = 34;

    /// <summary>Individual payments planted far outside their category's normal range.</summary>
    public int ExtremeOutlierCount { get; set; } = 6;

    /// <summary>First date in the generated period.</summary>
    public DateOnly StartDate { get; set; } = new(2025, 1, 1);

    /// <summary>Last date in the generated period.</summary>
    public DateOnly EndDate { get; set; } = new(2025, 12, 31);
}
