namespace BudgetGuard.Domain.Detection;

/// <summary>
/// One ranked entry in the anomaly report: a subject, its combined risk score,
/// and every signal that contributed to it.
/// </summary>
/// <param name="SubjectType">Transaction, vendor, or dataset-level scope.</param>
/// <param name="SubjectKey">Stable identifier for drill-down.</param>
/// <param name="SubjectLabel">What to show the auditor.</param>
/// <param name="RiskScore">Combined score in [0,1]; the report's sort key.</param>
/// <param name="Severity">Triage band derived from <paramref name="RiskScore"/>.</param>
/// <param name="Signals">Contributing signals, strongest first.</param>
/// <param name="Category">Spending category, when the subject has one. Enables report filtering.</param>
/// <param name="Department">Department, when the subject has one.</param>
/// <param name="Amount">Transaction amount, for transaction-level findings.</param>
/// <param name="TransactionDate">Transaction date, for transaction-level findings.</param>
public sealed record AnomalyFinding(
    AnomalySubjectType SubjectType,
    string SubjectKey,
    string SubjectLabel,
    double RiskScore,
    Severity Severity,
    IReadOnlyList<AnomalySignal> Signals,
    string? Category = null,
    string? Department = null,
    decimal? Amount = null,
    DateOnly? TransactionDate = null)
{
    /// <summary>
    /// The distinct detectors implicating this subject. Two or more independent
    /// methods agreeing is the strongest thing this report can say, so the UI
    /// surfaces it directly.
    /// </summary>
    public IReadOnlyCollection<DetectorKind> CorroboratingDetectors =>
        Signals.Select(s => s.Detector).Distinct().ToArray();

    /// <summary>The single most important sentence for this finding.</summary>
    public string PrimaryExplanation =>
        Signals.Count == 0 ? string.Empty : Signals[0].Explanation;
}
