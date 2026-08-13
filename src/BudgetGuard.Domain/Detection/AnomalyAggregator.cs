using BudgetGuard.Domain.Detection.Benford;
using BudgetGuard.Domain.Detection.Concentration;
using BudgetGuard.Domain.Detection.Outliers;
using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Detection;

/// <summary>Runs the full detection pipeline and merges the results into one ranked report.</summary>
public interface IAnomalyAggregator
{
    /// <summary>Analyses a dataset and returns findings ranked by combined risk.</summary>
    AnomalyReport Analyze(IReadOnlyList<ProcurementTransaction> transactions, string datasetLabel);
}

/// <summary>
/// Combines the three detectors into a single ranked list.
/// <para>
/// <b>Scoring.</b> A subject's risk score is driven by its strongest single
/// signal, multiplied by that detector's configured weight, then increased by a
/// fixed bonus for each additional independent detector that also implicated
/// it. Scores are not summed: a transaction flagged as an outlier against its
/// vendor, its category and its department has one anomaly, not three, and
/// summing would let repetition of a single method outrank genuinely
/// corroborated evidence.
/// </para>
/// <para>
/// <b>Why corroboration earns a bonus.</b> The three detectors read different
/// properties of the data — the digits of an amount, its distance from peer
/// amounts, and the distribution of awards across suppliers. They can fire
/// independently on innocent data. When two of them land on the same vendor,
/// that agreement is real evidence, so it is rewarded. The bonus is capped and
/// additive, so it can sharpen a ranking but can never create a finding out of
/// signals that never fired.
/// </para>
/// <para>
/// <b>Everything stays attributable.</b> A finding always carries the full list
/// of signals that produced it, each with its own sentence and raw statistics.
/// The score orders the queue; the explanations are what the auditor acts on.
/// </para>
/// </summary>
public sealed class AnomalyAggregator : IAnomalyAggregator
{
    private readonly IBenfordAnalyzer _benford;
    private readonly IZScoreOutlierDetector _outliers;
    private readonly IVendorConcentrationAnalyzer _concentration;
    private readonly DetectionSettings _settings;

    public AnomalyAggregator(
        IBenfordAnalyzer benford,
        IZScoreOutlierDetector outliers,
        IVendorConcentrationAnalyzer concentration,
        DetectionSettings settings)
    {
        _benford = benford ?? throw new ArgumentNullException(nameof(benford));
        _outliers = outliers ?? throw new ArgumentNullException(nameof(outliers));
        _concentration = concentration ?? throw new ArgumentNullException(nameof(concentration));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public AnomalyReport Analyze(IReadOnlyList<ProcurementTransaction> transactions, string datasetLabel)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var signals = new List<AnomalySignal>();

        var datasetBenford = _benford.Analyze(transactions.Select(t => t.Amount), datasetLabel);
        signals.AddRange(DatasetBenfordSignals(datasetBenford, datasetLabel));
        signals.AddRange(VendorBenfordSignals(transactions));

        var outlierFindings = _outliers.Detect(transactions);
        signals.AddRange(OutlierSignals(outlierFindings));

        var concentrationResult = _concentration.Analyze(transactions);
        signals.AddRange(ConcentrationSignals(concentrationResult));

        var findings = Combine(signals, transactions, outlierFindings);

        return new AnomalyReport(
            datasetLabel,
            transactions.Count,
            findings,
            datasetBenford,
            concentrationResult);
    }

    private IEnumerable<AnomalySignal> DatasetBenfordSignals(BenfordResult result, string datasetLabel)
    {
        if (!result.IsAnomalous)
        {
            yield break;
        }

        yield return AnomalySignal.Create(
            DetectorKind.Benford,
            AnomalySubjectType.Dataset,
            subjectKey: "dataset",
            subjectLabel: datasetLabel,
            score: BenfordScore(result),
            explanation: result.Explanation,
            evidence: BenfordEvidence(result));
    }

    /// <summary>
    /// Runs the first-digit test per vendor as well as dataset-wide.
    /// <para>
    /// A dataset-level Benford failure tells an auditor that something in the
    /// ledger is wrong but not where to look. Testing each sufficiently large
    /// vendor separately localises it, and a single supplier whose invoice
    /// digits do not behave naturally is a far more actionable lead than a
    /// whole-ledger verdict.
    /// </para>
    /// </summary>
    private IEnumerable<AnomalySignal> VendorBenfordSignals(IReadOnlyList<ProcurementTransaction> transactions)
    {
        foreach (var vendorGroup in transactions
                     .GroupBy(t => t.VendorName.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var amounts = vendorGroup.Select(t => t.Amount).ToArray();

            if (amounts.Count(a => a > 0m) < _settings.Benford.MinimumVendorSampleSize)
            {
                continue;
            }

            var result = _benford.Analyze(amounts, $"vendor \"{vendorGroup.Key}\"");

            if (!result.IsAnomalous)
            {
                continue;
            }

            yield return AnomalySignal.Create(
                DetectorKind.Benford,
                AnomalySubjectType.Vendor,
                subjectKey: vendorGroup.Key,
                subjectLabel: vendorGroup.Key,
                score: BenfordScore(result),
                explanation: result.Explanation,
                evidence: BenfordEvidence(result));
        }
    }

    private IEnumerable<AnomalySignal> OutlierSignals(IReadOnlyList<OutlierFinding> findings) =>
        findings.Select(f => AnomalySignal.Create(
            DetectorKind.ZScoreOutlier,
            AnomalySubjectType.Transaction,
            subjectKey: f.TransactionId.ToString(),
            subjectLabel: $"{f.ExternalReference} — {f.VendorName}",
            score: f.NormalisedScore,
            explanation: f.Explanation,
            evidence: new Dictionary<string, string>
            {
                ["Method"] = f.Method.ToString(),
                ["Grouping"] = f.Grouping.ToString(),
                ["Peer group"] = f.GroupKey,
                ["Peer group size"] = f.GroupSize.ToString("N0"),
                ["Test statistic"] = f.Score.ToString("F2"),
                ["Threshold"] = f.Threshold.ToString("F2"),
                ["Group centre"] = f.GroupCentre.ToString("N0"),
                ["Group dispersion"] = f.GroupDispersion.ToString("N0")
            }));

    private IEnumerable<AnomalySignal> ConcentrationSignals(VendorConcentrationResult result) =>
        result.Findings.Select(f => AnomalySignal.Create(
            DetectorKind.VendorConcentration,
            AnomalySubjectType.Vendor,
            subjectKey: f.VendorName,
            subjectLabel: f.VendorName,
            score: f.NormalisedScore,
            explanation: f.Explanation,
            evidence: new Dictionary<string, string>
            {
                ["Scope"] = $"{f.Scope} \"{f.ScopeKey}\"",
                ["Vendor spend"] = f.Spend.ToString("N0"),
                ["Scope spend"] = f.ScopeTotalSpend.ToString("N0"),
                ["Spend share"] = DetectionFormat.Percent(f.SpendShare),
                ["Even-split expectation"] = DetectionFormat.Percent(f.ExpectedShare),
                ["Excess multiple"] = $"{f.ExcessMultiple:F1}x",
                ["Contracts held"] = $"{f.ContractCount:N0} of {f.ScopeTransactionCount:N0}",
                ["Contracts expected by chance"] = f.ExpectedContractCount.ToString("N1"),
                ["Contract count excess"] = $"{f.ContractCountZScore:F1} sigma",
                ["Vendors in scope"] = f.VendorsInScope.ToString("N0")
            }));

    /// <summary>
    /// Normalises a Benford verdict onto [0,1]. Sitting exactly on the
    /// threshold this population had to clear scores 0.5; twice that deviation
    /// saturates.
    /// <para>
    /// Scored against the effective threshold rather than the fixed band, so a
    /// small vendor whose deviation only just clears its (higher) noise floor
    /// is not ranked as severely as a large population showing the same raw
    /// MAD against a much lower bar.
    /// </para>
    /// </summary>
    private static double BenfordScore(BenfordResult result) =>
        Math.Clamp(
            result.MeanAbsoluteDeviation / (2d * result.EffectiveMadThreshold),
            0d,
            1d);

    private static Dictionary<string, string> BenfordEvidence(BenfordResult result) =>
        new()
        {
            ["Sample size"] = result.SampleSize.ToString("N0"),
            ["Excluded (zero/negative)"] = result.ExcludedCount.ToString("N0"),
            ["Mean absolute deviation"] = result.MeanAbsoluteDeviation.ToString("F4"),
            ["Threshold applied"] = result.EffectiveMadThreshold.ToString("F4"),
            ["Conformity"] = result.Conformity.ToString(),
            ["Chi-square"] = result.ChiSquare.ToString("F2"),
            ["Chi-square critical value"] = result.ChiSquareCriticalValue.ToString("F3")
        };

    /// <summary>
    /// Merges signals by subject and computes each subject's combined risk score.
    /// </summary>
    private List<AnomalyFinding> Combine(
        IReadOnlyList<AnomalySignal> signals,
        IReadOnlyList<ProcurementTransaction> transactions,
        IReadOnlyList<OutlierFinding> outlierFindings)
    {
        var transactionsById = transactions.ToDictionary(t => t.Id.ToString());

        // A vendor's category/department context, used to make vendor-level
        // findings filterable alongside transaction-level ones.
        var vendorContext = outlierFindings
            .GroupBy(f => f.VendorName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var findings = new List<AnomalyFinding>();

        foreach (var group in signals.GroupBy(s => (s.SubjectType, s.SubjectKey)))
        {
            var subjectSignals = group
                .OrderByDescending(s => s.Score)
                .ToArray();

            // Strongest signal per detector — repetition of one method within a
            // subject must not inflate the score.
            var strongestPerDetector = subjectSignals
                .GroupBy(s => s.Detector)
                .Select(d => d.Max(s => s.Score * WeightFor(d.Key)))
                .ToArray();

            var distinctDetectors = strongestPerDetector.Length;

            var riskScore = Math.Clamp(
                strongestPerDetector.Max() +
                _settings.Scoring.CorroborationBonus * (distinctDetectors - 1),
                0d,
                1d);

            var (subjectType, subjectKey) = group.Key;

            string? category = null;
            string? department = null;
            decimal? amount = null;
            DateOnly? date = null;

            if (subjectType == AnomalySubjectType.Transaction &&
                transactionsById.TryGetValue(subjectKey, out var transaction))
            {
                category = transaction.Category;
                department = transaction.Department;
                amount = transaction.Amount;
                date = transaction.TransactionDate;
            }
            else if (subjectType == AnomalySubjectType.Vendor &&
                     vendorContext.TryGetValue(subjectKey, out var context))
            {
                category = context.Category;
                department = context.Department;
            }

            findings.Add(new AnomalyFinding(
                subjectType,
                subjectKey,
                subjectSignals[0].SubjectLabel,
                riskScore,
                ClassifySeverity(riskScore),
                subjectSignals,
                category,
                department,
                amount,
                date));
        }

        return findings
            .OrderByDescending(f => f.RiskScore)
            .ThenByDescending(f => f.Signals.Count)
            .ThenBy(f => f.SubjectLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private double WeightFor(DetectorKind detector) => detector switch
    {
        DetectorKind.Benford => _settings.Scoring.BenfordWeight,
        DetectorKind.ZScoreOutlier => _settings.Scoring.ZScoreWeight,
        DetectorKind.VendorConcentration => _settings.Scoring.VendorConcentrationWeight,
        _ => 1d
    };

    private Severity ClassifySeverity(double riskScore)
    {
        if (riskScore >= _settings.Scoring.CriticalThreshold)
        {
            return Severity.Critical;
        }

        if (riskScore >= _settings.Scoring.HighThreshold)
        {
            return Severity.High;
        }

        return riskScore >= _settings.Scoring.MediumThreshold
            ? Severity.Medium
            : Severity.Low;
    }
}
