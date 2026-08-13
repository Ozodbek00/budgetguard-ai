using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Benford;
using BudgetGuard.Domain.Detection.Concentration;
using BudgetGuard.Domain.Detection.Outliers;
using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Tests;

public sealed class AnomalyAggregatorTests
{
    private static AnomalyAggregator Aggregator(DetectionSettings? settings = null)
    {
        settings ??= new DetectionSettings();

        return new AnomalyAggregator(
            new BenfordAnalyzer(settings.Benford),
            new ZScoreOutlierDetector(settings.ZScore),
            new VendorConcentrationAnalyzer(settings.VendorConcentration),
            settings);
    }

    /// <summary>
    /// A competitive, Benford-conforming ledger across several vendors and
    /// categories. Nothing here should reach an actionable severity.
    /// </summary>
    private static List<ProcurementTransaction> CleanLedger(int seed = 4242)
    {
        var random = new Random(seed);
        var vendors = Enumerable.Range(0, 12).Select(i => $"Vendor {i:D2}").ToArray();
        var categories = new[] { "Construction", "IT Equipment", "Textbooks", "Catering Services" };
        var departments = new[] { "Ministry A", "Ministry B", "Ministry C" };

        return Enumerable.Range(0, 1_200)
            .Select(_ => TestData.Transaction(
                (decimal)Math.Round(1_000d * Math.Pow(10d, random.NextDouble() * 3d), 2),
                vendors[random.Next(vendors.Length)],
                categories[random.Next(categories.Length)],
                departments[random.Next(departments.Length)]))
            .ToList();
    }

    // -----------------------------------------------------------------
    // The false-positive test. This is the one that matters most: an audit
    // tool that flags honest spending gets switched off.
    // -----------------------------------------------------------------

    [Fact]
    public void Clean_ledger_produces_no_high_or_critical_findings()
    {
        var report = Aggregator().Analyze(CleanLedger(), "clean ledger");

        Assert.Equal(0, report.ActionableCount);
        Assert.False(report.DatasetBenford.IsAnomalous);
    }

    [Fact]
    public void Clean_ledger_still_reports_its_scope_summaries_for_the_vendor_risk_view()
    {
        var report = Aggregator().Analyze(CleanLedger(), "clean ledger");

        Assert.NotEmpty(report.Scopes);
        Assert.All(report.Scopes, s => Assert.False(s.IsHighlyConcentrated));
    }

    // -----------------------------------------------------------------
    // Corroboration: independent detectors agreeing is the strongest
    // statement this report can make.
    // -----------------------------------------------------------------

    /// <summary>
    /// A category where one vendor both dominates the spend and issues
    /// manufactured round-number invoices — so concentration and Benford
    /// should independently implicate the same supplier.
    /// </summary>
    private static List<ProcurementTransaction> LedgerWithDoublyImplicatedVendor()
    {
        var transactions = new List<ProcurementTransaction>();
        var random = new Random(999);

        // Five honest competitors sharing the rest of the category.
        for (var v = 0; v < 5; v++)
        {
            for (var i = 0; i < 20; i++)
            {
                transactions.Add(TestData.Transaction(
                    (decimal)Math.Round(1_000d * Math.Pow(10d, random.NextDouble() * 3d), 2),
                    vendor: $"Honest Vendor {v}",
                    category: "Construction"));
            }
        }

        // The suspect: 60 invoices, every one a round multiple of five million.
        for (var i = 0; i < 60; i++)
        {
            var choices = new[] { 25m, 30m, 50m, 75m, 20m };
            transactions.Add(TestData.Transaction(
                choices[i % choices.Length] * 1_000_000m,
                vendor: "Suspect Vendor",
                category: "Construction"));
        }

        return transactions;
    }

    [Fact]
    public void Vendor_implicated_by_two_independent_detectors_is_reported_with_both_signals()
    {
        var report = Aggregator().Analyze(LedgerWithDoublyImplicatedVendor(), "test ledger");

        var suspect = report.Findings.Single(f =>
            f.SubjectType == AnomalySubjectType.Vendor && f.SubjectKey == "Suspect Vendor");

        Assert.Contains(DetectorKind.Benford, suspect.CorroboratingDetectors);
        Assert.Contains(DetectorKind.VendorConcentration, suspect.CorroboratingDetectors);
        Assert.Equal(Severity.Critical, suspect.Severity);
    }

    [Fact]
    public void Corroborated_finding_outranks_a_singly_flagged_one()
    {
        var report = Aggregator().Analyze(LedgerWithDoublyImplicatedVendor(), "test ledger");

        var suspect = report.Findings.Single(f =>
            f.SubjectType == AnomalySubjectType.Vendor && f.SubjectKey == "Suspect Vendor");

        Assert.Equal(1, report.CorroboratedCount);
        Assert.Equal(suspect.SubjectKey, report.Findings[0].SubjectKey);
    }

    [Fact]
    public void Corroboration_bonus_is_configurable_and_raises_the_score()
    {
        var withoutBonus = new DetectionSettings();
        withoutBonus.Scoring.CorroborationBonus = 0d;

        var withBonus = new DetectionSettings();
        withBonus.Scoring.CorroborationBonus = 0.15d;

        var ledger = LedgerWithDoublyImplicatedVendor();

        double ScoreFor(DetectionSettings settings) => Aggregator(settings)
            .Analyze(ledger, "test ledger")
            .Findings.Single(f => f.SubjectKey == "Suspect Vendor" && f.SubjectType == AnomalySubjectType.Vendor)
            .RiskScore;

        Assert.True(ScoreFor(withBonus) >= ScoreFor(withoutBonus));
    }

    // -----------------------------------------------------------------
    // Repetition of one method must not masquerade as corroboration.
    // -----------------------------------------------------------------

    [Fact]
    public void One_transaction_flagged_in_several_groupings_counts_as_a_single_detector()
    {
        // The planted payment is an outlier against its vendor, its category
        // and its department, so the z-score detector fires three times on it.
        // That is one anomaly seen three ways, not three anomalies.
        var transactions = Enumerable.Range(0, 30)
            .Select(i => TestData.Transaction(1_000m + i))
            .ToList();
        transactions.Add(TestData.Transaction(900_000m, reference: "PLANTED"));

        var report = Aggregator().Analyze(transactions, "test ledger");

        var finding = report.Findings.Single(f => f.SubjectType == AnomalySubjectType.Transaction);

        Assert.Equal(3, finding.Signals.Count);
        Assert.Single(finding.CorroboratingDetectors);
        Assert.Equal(DetectorKind.ZScoreOutlier, finding.CorroboratingDetectors.Single());

        // Score equals the strongest single signal, with no corroboration bonus.
        Assert.Equal(finding.Signals.Max(s => s.Score), finding.RiskScore, precision: 6);
    }

    // -----------------------------------------------------------------
    // Report shape, ranking and filtering support
    // -----------------------------------------------------------------

    [Fact]
    public void Findings_are_ranked_by_descending_risk_score()
    {
        var report = Aggregator().Analyze(LedgerWithDoublyImplicatedVendor(), "test ledger");

        var scores = report.Findings.Select(f => f.RiskScore).ToArray();
        Assert.Equal(scores.OrderByDescending(s => s), scores);
    }

    [Fact]
    public void Every_finding_carries_at_least_one_verifiable_explanation()
    {
        var report = Aggregator().Analyze(LedgerWithDoublyImplicatedVendor(), "test ledger");

        Assert.NotEmpty(report.Findings);
        Assert.All(report.Findings, f =>
        {
            Assert.NotEmpty(f.Signals);
            Assert.False(string.IsNullOrWhiteSpace(f.PrimaryExplanation));
            Assert.All(f.Signals, s =>
            {
                Assert.False(string.IsNullOrWhiteSpace(s.Explanation));
                Assert.NotEmpty(s.Evidence);
                Assert.InRange(s.Score, 0d, 1d);
            });
        });
    }

    [Fact]
    public void Transaction_findings_carry_the_context_the_report_filters_on()
    {
        var transactions = Enumerable.Range(0, 30)
            .Select(i => TestData.Transaction(1_000m + i, category: "IT Equipment", department: "Ministry of Health"))
            .ToList();
        transactions.Add(TestData.Transaction(
            900_000m, category: "IT Equipment", department: "Ministry of Health",
            reference: "PLANTED", date: new DateOnly(2025, 3, 14)));

        var report = Aggregator().Analyze(transactions, "test ledger");
        var finding = report.Findings.Single(f => f.SubjectType == AnomalySubjectType.Transaction);

        Assert.Equal("IT Equipment", finding.Category);
        Assert.Equal("Ministry of Health", finding.Department);
        Assert.Equal(900_000m, finding.Amount);
        Assert.Equal(new DateOnly(2025, 3, 14), finding.TransactionDate);
    }

    [Fact]
    public void Vendor_findings_carry_the_scope_they_were_flagged_in_so_filters_do_not_hide_them()
    {
        // Regression: a vendor flagged purely for concentration had no category,
        // so filtering the report to "Construction" dropped the very supplier
        // dominating Construction — the filter hid the most important finding.
        var report = Aggregator().Analyze(LedgerWithDoublyImplicatedVendor(), "test ledger");

        var suspect = report.Findings.Single(f =>
            f.SubjectType == AnomalySubjectType.Vendor && f.SubjectKey == "Suspect Vendor");

        Assert.Equal("Construction", suspect.Category);
        Assert.False(string.IsNullOrWhiteSpace(suspect.Department));
    }

    [Fact]
    public void Every_vendor_finding_has_a_category_even_without_an_outlier_signal()
    {
        var report = Aggregator().Analyze(LedgerWithDoublyImplicatedVendor(), "test ledger");

        Assert.All(
            report.Findings.Where(f => f.SubjectType == AnomalySubjectType.Vendor),
            f => Assert.False(string.IsNullOrWhiteSpace(f.Category)));
    }

    [Fact]
    public void Risk_scores_never_leave_the_zero_to_one_range()
    {
        var report = Aggregator().Analyze(LedgerWithDoublyImplicatedVendor(), "test ledger");

        Assert.All(report.Findings, f => Assert.InRange(f.RiskScore, 0d, 1d));
    }

    [Theory]
    [InlineData(0.80, Severity.Critical)]
    [InlineData(0.75, Severity.Critical)]
    [InlineData(0.60, Severity.High)]
    [InlineData(0.50, Severity.High)]
    [InlineData(0.30, Severity.Medium)]
    [InlineData(0.25, Severity.Medium)]
    [InlineData(0.10, Severity.Low)]
    public void Severity_bands_follow_the_configured_thresholds(double share, Severity expected)
    {
        // Drive the aggregator to an exactly known risk score using a single
        // concentration signal. The target vendor holds 50% of scope spend and
        // the absolute threshold is 25%, so its normalised score saturates at
        // 1.0; the detector weight then sets the final score directly.
        var settings = new DetectionSettings();
        settings.Scoring.CorroborationBonus = 0d;
        settings.Scoring.VendorConcentrationWeight = share;
        settings.VendorConcentration.SpendShareThreshold = 0.25;

        // Disable the relative test so only the absolute one contributes.
        settings.VendorConcentration.ExpectedShareMultiple = 1_000_000d;

        // Target holds 50% of spend across 20 of the 30 contracts, so it clears
        // both the spend threshold and the contract-count significance test.
        var transactions = new List<ProcurementTransaction>();
        transactions.AddRange(Enumerable.Range(0, 20)
            .Select(_ => TestData.Transaction(25_000m, vendor: "Target")));
        transactions.AddRange(Enumerable.Range(0, 5)
            .Select(_ => TestData.Transaction(50_000m, vendor: "Other A")));
        transactions.AddRange(Enumerable.Range(0, 5)
            .Select(_ => TestData.Transaction(50_000m, vendor: "Other B")));

        var report = Aggregator(settings).Analyze(transactions, "test ledger");
        var target = report.Findings.Single(f => f.SubjectKey == "Target");

        Assert.Equal(share, target.RiskScore, precision: 6);
        Assert.Equal(expected, target.Severity);
    }

    [Fact]
    public void Dataset_level_Benford_failure_is_reported_as_a_dataset_finding()
    {
        // Enough uniform-digit amounts to fail the dataset-wide test while
        // being spread thin enough across vendors to avoid vendor-level flags.
        var transactions = TestData.UniformDigitAmounts(40)
            .Select((a, i) => TestData.Transaction(a, vendor: $"Vendor {i % 30:D2}"))
            .ToList();

        var report = Aggregator().Analyze(transactions, "manipulated ledger");

        Assert.True(report.DatasetBenford.IsAnomalous);
        Assert.Contains(report.Findings, f => f.SubjectType == AnomalySubjectType.Dataset);
    }

    [Fact]
    public void Dataset_Benford_result_is_always_present_even_when_conformant()
    {
        var report = Aggregator().Analyze(CleanLedger(), "clean ledger");

        Assert.NotNull(report.DatasetBenford);
        Assert.Equal(9, report.DatasetBenford.Digits.Count);
        Assert.DoesNotContain(report.Findings, f => f.SubjectType == AnomalySubjectType.Dataset);
    }

    [Fact]
    public void Empty_dataset_produces_an_empty_report_rather_than_throwing()
    {
        var report = Aggregator().Analyze([], "empty");

        Assert.Empty(report.Findings);
        Assert.Equal(0, report.TransactionCount);
        Assert.Equal(BenfordConformity.InsufficientData, report.DatasetBenford.Conformity);
    }

    [Fact]
    public void Analyze_rejects_a_null_collection() =>
        Assert.Throws<ArgumentNullException>(() => Aggregator().Analyze(null!, "x"));
}
