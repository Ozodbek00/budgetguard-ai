namespace BudgetGuard.Application.Analysis.Dtos;

/// <summary>One detector's contribution to a finding, flattened for transport.</summary>
/// <param name="Detector">Which method fired.</param>
/// <param name="Score">Normalised strength, 0-1.</param>
/// <param name="Explanation">The sentence an auditor reads.</param>
/// <param name="Evidence">Named raw statistics for the drill-down panel.</param>
public sealed record AnomalySignalDto(
    string Detector,
    double Score,
    string Explanation,
    IReadOnlyDictionary<string, string> Evidence);

/// <summary>One ranked row of the anomaly report.</summary>
/// <param name="SubjectType">Transaction, Vendor, or Dataset.</param>
/// <param name="SubjectKey">Stable identifier.</param>
/// <param name="SubjectLabel">Display label.</param>
/// <param name="RiskScore">Combined score, 0-1.</param>
/// <param name="Severity">Triage band.</param>
/// <param name="Category">Spending category, when applicable — the report filters on this.</param>
/// <param name="Department">Department, when applicable.</param>
/// <param name="Amount">Transaction amount, for transaction-level findings.</param>
/// <param name="TransactionDate">Transaction date, for transaction-level findings.</param>
/// <param name="Detectors">Distinct detectors implicating this subject.</param>
/// <param name="Signals">Contributing signals, strongest first.</param>
public sealed record AnomalyFindingDto(
    string SubjectType,
    string SubjectKey,
    string SubjectLabel,
    double RiskScore,
    string Severity,
    string? Category,
    string? Department,
    decimal? Amount,
    DateOnly? TransactionDate,
    IReadOnlyList<string> Detectors,
    IReadOnlyList<AnomalySignalDto> Signals)
{
    /// <summary>True when more than one independent method implicated this subject.</summary>
    public bool IsCorroborated => Detectors.Count > 1;

    /// <summary>The headline sentence for the report row.</summary>
    public string PrimaryExplanation => Signals.Count == 0 ? string.Empty : Signals[0].Explanation;
}

/// <summary>Counts by severity, for the report header.</summary>
/// <param name="Critical">Findings scoring at or above the critical threshold.</param>
/// <param name="High">Findings in the high band.</param>
/// <param name="Medium">Findings in the medium band.</param>
/// <param name="Low">Findings in the low band.</param>
/// <param name="Corroborated">Findings implicated by more than one detector.</param>
public sealed record AnomalySummaryDto(
    int Critical,
    int High,
    int Medium,
    int Low,
    int Corroborated)
{
    /// <summary>High and Critical together — the auditor's work queue.</summary>
    public int Actionable => Critical + High;

    public int Total => Critical + High + Medium + Low;
}

/// <summary>The complete anomaly report for one dataset.</summary>
/// <param name="DatasetId">Dataset analysed.</param>
/// <param name="DatasetName">Its display name.</param>
/// <param name="IsSyntheticDemo">True when this is generated demo data, not real spending.</param>
/// <param name="TransactionCount">Rows analysed.</param>
/// <param name="GeneratedAtUtc">When this report was produced.</param>
/// <param name="Summary">Severity counts.</param>
/// <param name="Findings">Ranked findings, highest risk first.</param>
/// <param name="Categories">Distinct categories present, for the filter dropdown.</param>
/// <param name="Departments">Distinct departments present, for the filter dropdown.</param>
public sealed record AnomalyReportDto(
    Guid DatasetId,
    string DatasetName,
    bool IsSyntheticDemo,
    int TransactionCount,
    DateTimeOffset GeneratedAtUtc,
    AnomalySummaryDto Summary,
    IReadOnlyList<AnomalyFindingDto> Findings,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Departments);
