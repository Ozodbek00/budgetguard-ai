using BudgetGuard.Domain.Demo;
using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Benford;
using BudgetGuard.Domain.Detection.Concentration;
using BudgetGuard.Domain.Detection.Outliers;

namespace BudgetGuard.Domain.Tests;

/// <summary>
/// End-to-end scoring of the detection engine against ground truth.
/// <para>
/// The synthetic generator records exactly which manipulations it planted.
/// These tests run the full pipeline over that data and assert the engine finds
/// each planted anomaly. This is the closest thing available to a recall
/// measurement without real labelled procurement fraud data, and it is what
/// stops a refactor from silently blinding a detector.
/// </para>
/// </summary>
public sealed class SyntheticDatasetDetectionTests
{
    private static readonly SyntheticDataset Demo = new SyntheticDatasetGenerator().Generate();

    private static AnomalyReport Analyze(DetectionSettings? settings = null)
    {
        settings ??= new DetectionSettings();

        var aggregator = new AnomalyAggregator(
            new BenfordAnalyzer(settings.Benford),
            new ZScoreOutlierDetector(settings.ZScore),
            new VendorConcentrationAnalyzer(settings.VendorConcentration),
            settings);

        return aggregator.Analyze(Demo.Transactions, "Demo dataset");
    }

    [Fact]
    public void Generator_is_deterministic_for_a_fixed_seed()
    {
        var first = new SyntheticDatasetGenerator().Generate();
        var second = new SyntheticDatasetGenerator().Generate();

        Assert.Equal(first.Transactions.Count, second.Transactions.Count);
        Assert.Equal(
            first.Transactions.Select(t => t.Amount),
            second.Transactions.Select(t => t.Amount));
    }

    [Fact]
    public void Generator_produces_distinct_data_for_a_different_seed()
    {
        var other = new SyntheticDatasetGenerator().Generate(new SyntheticDataOptions { Seed = 777 });

        Assert.NotEqual(
            Demo.Transactions.Select(t => t.Amount).Take(50),
            other.Transactions.Select(t => t.Amount).Take(50));
    }

    [Fact]
    public void Generator_records_every_planted_anomaly_as_ground_truth()
    {
        var kinds = Demo.PlantedAnomalies.Select(p => p.Kind).ToArray();

        Assert.Contains(PlantedAnomalyKind.ThresholdEvasion, kinds);
        Assert.Contains(PlantedAnomalyKind.RoundNumberInvoicing, kinds);
        Assert.Contains(PlantedAnomalyKind.VendorConcentration, kinds);
        Assert.Contains(PlantedAnomalyKind.ExtremeOutlier, kinds);
        Assert.All(Demo.PlantedAnomalies, p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));
    }

    [Fact]
    public void Every_generated_transaction_has_the_fields_the_detectors_group_on()
    {
        Assert.All(Demo.Transactions, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.VendorName));
            Assert.False(string.IsNullOrWhiteSpace(t.Category));
            Assert.False(string.IsNullOrWhiteSpace(t.Department));
            Assert.False(string.IsNullOrWhiteSpace(t.ExternalReference));
            Assert.True(t.Amount > 0m);
        });

        // External references must be unique — they are how an auditor traces
        // a finding back to the source record.
        Assert.Equal(
            Demo.Transactions.Count,
            Demo.Transactions.Select(t => t.ExternalReference).Distinct().Count());
    }

    // -----------------------------------------------------------------
    // Each planted anomaly must actually be detected.
    // -----------------------------------------------------------------

    [Fact]
    public void Planted_threshold_evasion_breaks_dataset_wide_Benford_conformity()
    {
        var report = Analyze();

        Assert.True(report.DatasetBenford.IsAnomalous,
            $"Expected non-conformity; MAD was {report.DatasetBenford.MeanAbsoluteDeviation:F4}.");

        // Contracts priced just under the ceiling all lead with 4.
        Assert.Equal(4, report.DatasetBenford.MostOverRepresentedDigit!.Digit);
    }

    [Fact]
    public void Planted_round_number_vendor_is_flagged_by_per_vendor_Benford()
    {
        var planted = Demo.PlantedAnomalies.Single(p => p.Kind == PlantedAnomalyKind.RoundNumberInvoicing);
        var report = Analyze();

        var finding = report.Findings.SingleOrDefault(f =>
            f.SubjectType == AnomalySubjectType.Vendor && f.SubjectKey == planted.SubjectKey);

        Assert.NotNull(finding);
        Assert.Contains(DetectorKind.Benford, finding.CorroboratingDetectors);
    }

    [Fact]
    public void Planted_concentrated_vendor_is_flagged_by_concentration_analysis()
    {
        var planted = Demo.PlantedAnomalies.Single(p => p.Kind == PlantedAnomalyKind.VendorConcentration);
        var report = Analyze();

        var finding = report.Findings.SingleOrDefault(f =>
            f.SubjectType == AnomalySubjectType.Vendor && f.SubjectKey == planted.SubjectKey);

        Assert.NotNull(finding);
        Assert.Contains(DetectorKind.VendorConcentration, finding.CorroboratingDetectors);
        Assert.True(finding.Severity >= Severity.High);
    }

    [Fact]
    public void Planted_extreme_outliers_are_all_flagged()
    {
        var planted = Demo.PlantedAnomalies.Single(p => p.Kind == PlantedAnomalyKind.ExtremeOutlier);
        var expectedReferences = planted.SubjectKey.Split(", ");

        var report = Analyze();

        var flaggedReferences = report.Findings
            .Where(f => f.SubjectType == AnomalySubjectType.Transaction)
            .SelectMany(f => f.Signals)
            .Select(s => s.SubjectLabel.Split(" — ")[0])
            .ToHashSet();

        Assert.All(expectedReferences, reference =>
            Assert.Contains(reference, flaggedReferences));
    }

    [Fact]
    public void Planted_outliers_rank_among_the_most_severe_transaction_findings()
    {
        var planted = Demo.PlantedAnomalies.Single(p => p.Kind == PlantedAnomalyKind.ExtremeOutlier);
        var expectedReferences = planted.SubjectKey.Split(", ").ToHashSet();

        var report = Analyze();

        var transactionFindings = report.Findings
            .Where(f => f.SubjectType == AnomalySubjectType.Transaction)
            .ToArray();

        // Every planted outlier should reach High or Critical, not be buried
        // among borderline noise.
        var plantedFindings = transactionFindings
            .Where(f => expectedReferences.Contains(f.SubjectLabel.Split(" — ")[0]))
            .ToArray();

        Assert.Equal(expectedReferences.Count, plantedFindings.Length);
        Assert.All(plantedFindings, f => Assert.True(f.Severity >= Severity.High,
            $"{f.SubjectLabel} was only {f.Severity}."));
    }

    // -----------------------------------------------------------------
    // Precision: the engine must stay usable, not flag everything.
    // -----------------------------------------------------------------

    [Fact]
    public void Actionable_findings_stay_a_reviewable_fraction_of_the_dataset()
    {
        var report = Analyze();

        // An auditor cannot work a queue that is a third of the ledger. The
        // engine is a triage tool, so the High/Critical set must stay small.
        var actionableRatio = (double)report.ActionableCount / report.TransactionCount;

        Assert.True(actionableRatio < 0.05,
            $"{report.ActionableCount} actionable findings over {report.TransactionCount} " +
            $"transactions ({actionableRatio:P1}) is too noisy to review.");
    }

    [Fact]
    public void Both_planted_vendors_appear_in_the_top_ranked_vendor_findings()
    {
        var report = Analyze();

        var topVendors = report.Findings
            .Where(f => f.SubjectType == AnomalySubjectType.Vendor)
            .Take(5)
            .Select(f => f.SubjectKey)
            .ToArray();

        var concentrated = Demo.PlantedAnomalies.Single(p => p.Kind == PlantedAnomalyKind.VendorConcentration);
        var roundNumber = Demo.PlantedAnomalies.Single(p => p.Kind == PlantedAnomalyKind.RoundNumberInvoicing);

        Assert.Contains(concentrated.SubjectKey, topVendors);
        Assert.Contains(roundNumber.SubjectKey, topVendors);
    }

    [Fact]
    public void Report_totals_are_internally_consistent()
    {
        var report = Analyze();

        Assert.Equal(Demo.Transactions.Count, report.TransactionCount);
        Assert.Equal(
            report.Findings.Count,
            Enum.GetValues<Severity>().Sum(report.CountAt));
    }
}
