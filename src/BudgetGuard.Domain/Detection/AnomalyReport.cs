using BudgetGuard.Domain.Detection.Benford;
using BudgetGuard.Domain.Detection.Concentration;

namespace BudgetGuard.Domain.Detection;

/// <summary>
/// The complete output of one analysis run: every ranked finding, plus the
/// supporting views the UI renders (digit distribution, scope competitiveness).
/// </summary>
/// <param name="DatasetLabel">What was analysed.</param>
/// <param name="TransactionCount">Rows fed to the detectors.</param>
/// <param name="Findings">Ranked findings, highest risk first.</param>
/// <param name="DatasetBenford">Dataset-wide first-digit result, always present.</param>
/// <param name="Scopes">Per-category and per-department competitiveness summaries.</param>
public sealed record AnomalyReport(
    string DatasetLabel,
    int TransactionCount,
    IReadOnlyList<AnomalyFinding> Findings,
    BenfordResult DatasetBenford,
    IReadOnlyList<ScopeConcentrationSummary> Scopes)
{
    /// <summary>Findings in a given severity band.</summary>
    public int CountAt(Severity severity) => Findings.Count(f => f.Severity == severity);

    /// <summary>Findings at High or Critical — the auditor's actual work queue.</summary>
    public int ActionableCount =>
        Findings.Count(f => f.Severity is Severity.High or Severity.Critical);

    /// <summary>Findings corroborated by more than one independent detector.</summary>
    public int CorroboratedCount =>
        Findings.Count(f => f.CorroboratingDetectors.Count > 1);
}
