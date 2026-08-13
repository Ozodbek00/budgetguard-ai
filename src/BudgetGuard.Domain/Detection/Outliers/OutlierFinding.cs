namespace BudgetGuard.Domain.Detection.Outliers;

/// <summary>
/// A single transaction whose amount sits implausibly far from its peer group.
/// </summary>
/// <param name="TransactionId">Surrogate key of the flagged transaction.</param>
/// <param name="ExternalReference">Source-system reference, so the auditor can pull the file.</param>
/// <param name="VendorName">Supplier paid.</param>
/// <param name="Category">Spending category.</param>
/// <param name="Department">Paying department.</param>
/// <param name="Amount">The flagged amount.</param>
/// <param name="TransactionDate">Date of the payment.</param>
/// <param name="Grouping">Which peer group produced the flag.</param>
/// <param name="GroupKey">The peer group's key — the vendor, category, or department name.</param>
/// <param name="GroupSize">Transactions in the peer group.</param>
/// <param name="Method">Statistic used.</param>
/// <param name="Transform">Scale the comparison was made on.</param>
/// <param name="Score">Signed test statistic. Positive means above the group centre.</param>
/// <param name="Threshold">Absolute value the statistic had to exceed.</param>
/// <param name="GroupCentre">
/// Group mean (classic) or median (modified), expressed in currency. Under a
/// log comparison this is the geometric mean — the typical payment.
/// </param>
/// <param name="GroupDispersion">
/// Group standard deviation or MAD. Under a raw comparison this is a currency
/// amount; under a log comparison it is a multiplicative factor, so one
/// standard deviation means "times this much".
/// </param>
/// <param name="NormalisedScore">Strength in [0,1], comparable with other detectors.</param>
/// <param name="Explanation">Plain-language, arithmetic-included reason.</param>
public sealed record OutlierFinding(
    Guid TransactionId,
    string ExternalReference,
    string VendorName,
    string Category,
    string Department,
    decimal Amount,
    DateOnly TransactionDate,
    OutlierGrouping Grouping,
    string GroupKey,
    int GroupSize,
    OutlierMethod Method,
    AmountTransform Transform,
    double Score,
    double Threshold,
    decimal GroupCentre,
    decimal GroupDispersion,
    double NormalisedScore,
    string Explanation)
{
    /// <summary>True when the amount is above its peer group's centre.</summary>
    public bool IsHighOutlier => Score > 0;
}
