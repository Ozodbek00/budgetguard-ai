using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Outliers;
using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Tests;

public sealed class ZScoreOutlierDetectorTests
{
    /// <summary>
    /// Default settings restricted to a single grouping. Isolating the grouping
    /// keeps each test's expected output derivable by hand — with all three
    /// groupings active, one planted outlier legitimately produces three
    /// findings, which would obscure what is actually being asserted.
    /// </summary>
    private static ZScoreSettings Settings(
        OutlierGrouping grouping = OutlierGrouping.Category,
        double threshold = 3.0,
        int minimumGroupSize = 8,
        OutlierMethod method = OutlierMethod.ClassicZScore,
        AmountTransform transform = AmountTransform.Log10) =>
        new()
        {
            Threshold = threshold,
            MinimumGroupSize = minimumGroupSize,
            Method = method,
            Transform = transform,
            Groupings = [grouping]
        };

    /// <summary>
    /// Fifty tightly clustered amounts. Used where a test needs headroom in the
    /// statistic: in a group of size n a single value's classic z-score cannot
    /// exceed (n-1)/sqrt(n), so a small group compresses every outlier toward
    /// the same score and hides differences in severity.
    /// </summary>
    private static List<ProcurementTransaction> LargeCleanGroup(int count = 50) =>
        Enumerable.Range(0, count)
            .Select(i => TestData.Transaction(96m + i % 10))
            .ToList();

    /// <summary>Twelve tightly clustered amounts — a well-behaved peer group with no outlier.</summary>
    private static List<ProcurementTransaction> CleanGroup(string category = "Construction") =>
        new[] { 100m, 105m, 98m, 102m, 97m, 103m, 99m, 101m, 104m, 96m, 100m, 102m }
            .Select(a => TestData.Transaction(a, category: category))
            .ToList();

    // -----------------------------------------------------------------
    // Clean data must stay quiet.
    // -----------------------------------------------------------------

    [Fact]
    public void Clean_peer_group_produces_no_findings()
    {
        var findings = new ZScoreOutlierDetector(Settings()).Detect(CleanGroup());

        Assert.Empty(findings);
    }

    [Fact]
    public void Group_with_zero_variance_produces_no_findings_and_does_not_divide_by_zero()
    {
        var transactions = Enumerable.Range(0, 20)
            .Select(_ => TestData.Transaction(500_000m))
            .ToList();

        var findings = new ZScoreOutlierDetector(Settings()).Detect(transactions);

        Assert.Empty(findings);
    }

    [Fact]
    public void Groups_smaller_than_the_minimum_are_skipped()
    {
        // Five payments, one of them wildly out of line. Too few to judge:
        // with n=5 the "outlier" is a quarter of the dispersion estimate.
        var transactions = new[] { 100m, 102m, 98m, 101m, 5_000_000m }
            .Select(a => TestData.Transaction(a))
            .ToList();

        var findings = new ZScoreOutlierDetector(Settings(minimumGroupSize: 8)).Detect(transactions);

        Assert.Empty(findings);
    }

    [Fact]
    public void Minimum_group_size_is_configurable()
    {
        var transactions = new[] { 100m, 102m, 98m, 101m, 99m, 5_000_000m }
            .Select(a => TestData.Transaction(a))
            .ToList();

        var findings = new ZScoreOutlierDetector(Settings(minimumGroupSize: 5, threshold: 1.5))
            .Detect(transactions);

        Assert.NotEmpty(findings);
    }

    // -----------------------------------------------------------------
    // Planted outliers must be caught, and identified precisely.
    // -----------------------------------------------------------------

    [Fact]
    public void Planted_extreme_outlier_is_detected_and_correctly_identified()
    {
        var transactions = CleanGroup();
        transactions.Add(TestData.Transaction(50_000m, reference: "PLANTED-1"));

        var findings = new ZScoreOutlierDetector(Settings()).Detect(transactions);

        var finding = Assert.Single(findings);
        Assert.Equal("PLANTED-1", finding.ExternalReference);
        Assert.Equal(50_000m, finding.Amount);
        Assert.True(finding.IsHighOutlier);
        Assert.True(Math.Abs(finding.Score) > 3.0);
        Assert.Equal(OutlierGrouping.Category, finding.Grouping);
        Assert.Equal(13, finding.GroupSize);
    }

    [Fact]
    public void Unusually_small_payments_are_flagged_as_well_as_large_ones()
    {
        // Contract splitting shows up as suspiciously small payments, so the
        // test is two-tailed by design.
        var transactions = Enumerable.Range(0, 30)
            .Select(i => TestData.Transaction(1_000_000m + i * 1_000m))
            .ToList();
        transactions.Add(TestData.Transaction(1m, reference: "TINY-1"));

        var findings = new ZScoreOutlierDetector(Settings(threshold: 3.0)).Detect(transactions);

        var finding = Assert.Single(findings);
        Assert.Equal("TINY-1", finding.ExternalReference);
        Assert.False(finding.IsHighOutlier);
    }

    [Fact]
    public void Raising_the_threshold_removes_a_borderline_flag()
    {
        var transactions = CleanGroup();
        transactions.Add(TestData.Transaction(130m, reference: "BORDERLINE"));

        var atThree = new ZScoreOutlierDetector(Settings(threshold: 3.0)).Detect(transactions);
        var atSix = new ZScoreOutlierDetector(Settings(threshold: 6.0)).Detect(transactions);

        Assert.NotEmpty(atThree);
        Assert.Empty(atSix);
    }

    // -----------------------------------------------------------------
    // Grouping semantics: an amount is only ever judged against its peers.
    // -----------------------------------------------------------------

    [Fact]
    public void An_amount_normal_for_its_own_category_is_not_flagged_against_a_richer_one()
    {
        // Road works run in the millions; stationery runs in the hundreds.
        // Judged dataset-wide, every stationery row would look like an
        // outlier. Judged against its own category, none should.
        var transactions = new List<ProcurementTransaction>();
        transactions.AddRange(Enumerable.Range(0, 20)
            .Select(i => TestData.Transaction(8_000_000m + i * 50_000m, category: "Road Maintenance")));
        transactions.AddRange(Enumerable.Range(0, 20)
            .Select(i => TestData.Transaction(400m + i * 10m, category: "Office Supplies")));

        var findings = new ZScoreOutlierDetector(Settings(OutlierGrouping.Category)).Detect(transactions);

        Assert.Empty(findings);
    }

    [Fact]
    public void Each_configured_grouping_is_evaluated_independently()
    {
        // One vendor's payments are uniform (nothing odd for that vendor), but
        // the whole vendor sits far above the category norm. Vendor grouping
        // should stay quiet; category grouping should fire.
        var transactions = new List<ProcurementTransaction>();
        transactions.AddRange(Enumerable.Range(0, 30)
            .Select(i => TestData.Transaction(1_000m + i, vendor: "Normal Vendor")));
        transactions.Add(TestData.Transaction(900_000m, vendor: "Odd Vendor", reference: "ODD-1"));

        var byVendor = new ZScoreOutlierDetector(Settings(OutlierGrouping.Vendor)).Detect(transactions);
        var byCategory = new ZScoreOutlierDetector(Settings(OutlierGrouping.Category)).Detect(transactions);

        Assert.Empty(byVendor);
        Assert.Equal("ODD-1", Assert.Single(byCategory).ExternalReference);
    }

    [Fact]
    public void Grouping_keys_are_matched_case_insensitively_and_ignore_surrounding_whitespace()
    {
        // Real procurement exports are not clean. "construction" and
        // " Construction " must land in the same peer group, or the group-size
        // guard silently suppresses every finding.
        var transactions = new List<ProcurementTransaction>();
        transactions.AddRange(Enumerable.Range(0, 6)
            .Select(i => TestData.Transaction(100m + i, category: "Construction")));
        transactions.AddRange(Enumerable.Range(0, 6)
            .Select(i => TestData.Transaction(100m + i, category: " construction ")));
        transactions.Add(TestData.Transaction(90_000m, category: "CONSTRUCTION", reference: "MIXED-CASE"));

        var findings = new ZScoreOutlierDetector(Settings()).Detect(transactions);

        Assert.Equal("MIXED-CASE", Assert.Single(findings).ExternalReference);
    }

    // -----------------------------------------------------------------
    // Classic versus robust: the masking problem, demonstrated.
    // -----------------------------------------------------------------

    [Fact]
    public void Classic_z_score_can_be_masked_by_a_pair_of_outliers_while_the_modified_statistic_catches_them()
    {
        // Eight payments of 100 and two of 1,000. The two large values inflate
        // the standard deviation they are then measured against, pulling their
        // own z-scores down to about 1.9 — under the threshold. The modified
        // z-score uses the median, which they cannot move, and flags both.
        var amounts = new[] { 100m, 100m, 100m, 100m, 100m, 100m, 100m, 100m, 1_000m, 1_000m };
        var transactions = amounts.Select(a => TestData.Transaction(a)).ToList();

        var classic = new ZScoreOutlierDetector(Settings(method: OutlierMethod.ClassicZScore))
            .Detect(transactions);
        var modified = new ZScoreOutlierDetector(Settings(method: OutlierMethod.ModifiedZScore))
            .Detect(transactions);

        Assert.Empty(classic);
        Assert.Equal(2, modified.Count);
        Assert.All(modified, f => Assert.Equal(1_000m, f.Amount));
        Assert.All(modified, f => Assert.Equal(OutlierMethod.ModifiedZScore, f.Method));
    }

    [Fact]
    public void Modified_z_score_handles_a_group_where_the_median_absolute_deviation_is_zero()
    {
        // Twenty identical amounts plus one outlier: MAD is exactly zero, so
        // the statistic would divide by zero without the fallback.
        var transactions = Enumerable.Range(0, 20)
            .Select(_ => TestData.Transaction(1_000m))
            .ToList();
        transactions.Add(TestData.Transaction(9_000_000m, reference: "SPIKE"));

        var findings = new ZScoreOutlierDetector(Settings(method: OutlierMethod.ModifiedZScore))
            .Detect(transactions);

        Assert.Equal("SPIKE", Assert.Single(findings).ExternalReference);
    }

    [Fact]
    public void Modified_z_score_does_not_flag_a_clean_group()
    {
        var findings = new ZScoreOutlierDetector(Settings(method: OutlierMethod.ModifiedZScore))
            .Detect(CleanGroup());

        Assert.Empty(findings);
    }

    [Fact]
    public void Median_of_an_even_length_sample_averages_the_two_central_values()
    {
        Assert.Equal(3.0, ZScoreOutlierDetector.Median([1d, 2d, 4d, 6d]));
        Assert.Equal(4.0, ZScoreOutlierDetector.Median([4d, 1d, 9d, 2d, 7d]));
        Assert.Equal(0.0, ZScoreOutlierDetector.Median([]));
    }

    // -----------------------------------------------------------------
    // Scoring and explanation
    // -----------------------------------------------------------------

    [Fact]
    public void Normalised_score_is_bounded_and_rises_with_the_test_statistic()
    {
        // Compared in separate runs: two outliers in one group would inflate a
        // shared standard deviation and mask each other, which is the subject
        // of a different test rather than this one.
        static double ScoreFor(decimal plantedAmount)
        {
            var transactions = LargeCleanGroup();
            transactions.Add(TestData.Transaction(plantedAmount, reference: "PLANTED"));

            return new ZScoreOutlierDetector(Settings())
                .Detect(transactions)
                .Single(f => f.ExternalReference == "PLANTED")
                .NormalisedScore;
        }

        var mild = ScoreFor(115m);
        var severe = ScoreFor(150m);

        Assert.InRange(mild, 0d, 1d);
        Assert.InRange(severe, 0d, 1d);
        Assert.True(severe > mild, $"Severe scored {severe:F3}, mild scored {mild:F3}.");
    }

    [Fact]
    public void Normalised_score_saturates_at_one_and_never_exceeds_it()
    {
        var transactions = LargeCleanGroup();
        transactions.Add(TestData.Transaction(200m, reference: "PLANTED"));

        var finding = Assert.Single(
            new ZScoreOutlierDetector(Settings()).Detect(transactions),
            f => f.ExternalReference == "PLANTED");

        Assert.Equal(1d, finding.NormalisedScore);
    }

    [Fact]
    public void Findings_are_returned_strongest_first()
    {
        var transactions = CleanGroup();
        transactions.Add(TestData.Transaction(500m, reference: "MILD"));
        transactions.Add(TestData.Transaction(500_000m, reference: "SEVERE"));

        var findings = new ZScoreOutlierDetector(Settings()).Detect(transactions);

        Assert.Equal("SEVERE", findings[0].ExternalReference);
    }

    [Fact]
    public void Explanation_contains_the_arithmetic_an_auditor_needs_to_recheck_the_flag()
    {
        var transactions = CleanGroup("Construction");
        transactions.Add(TestData.Transaction(50_000m, category: "Construction", reference: "BG-00042"));

        var finding = Assert.Single(new ZScoreOutlierDetector(Settings()).Detect(transactions));

        Assert.Contains("BG-00042", finding.Explanation);
        Assert.Contains("standard deviations above", finding.Explanation);
        Assert.Contains("Construction", finding.Explanation);
        Assert.Contains("geometric mean", finding.Explanation);
        Assert.Contains("13 payments", finding.Explanation);
        Assert.Contains("threshold is 3.0", finding.Explanation);
        Assert.Contains("logarithmic scale", finding.Explanation);
    }

    [Fact]
    public void Raw_scale_explanation_quotes_a_currency_mean_and_standard_deviation()
    {
        var transactions = CleanGroup();
        transactions.Add(TestData.Transaction(50_000m, reference: "BG-00042"));

        var finding = Assert.Single(new ZScoreOutlierDetector(
            Settings(transform: AmountTransform.None)).Detect(transactions));

        Assert.Contains("the mean for category", finding.Explanation);
        Assert.DoesNotContain("logarithmic scale", finding.Explanation);
    }

    // -----------------------------------------------------------------
    // Comparison scale. Spending is heavily right-skewed, so which scale
    // amounts are compared on decides how many false positives an auditor
    // has to wade through.
    // -----------------------------------------------------------------

    /// <summary>
    /// A peer group spanning three orders of magnitude with no planted anomaly
    /// — a realistic shape for procurement spending, where a category contains
    /// both small consumable orders and large capital contracts.
    /// </summary>
    private static List<ProcurementTransaction> HeavyTailedCleanGroup(int count = 400, int seed = 20260813)
    {
        var random = new Random(seed);

        return Enumerable.Range(0, count)
            .Select(_ => TestData.Transaction(
                (decimal)Math.Round(1_000d * Math.Pow(10d, random.NextDouble() * 3d), 2)))
            .ToList();
    }

    [Fact]
    public void Raw_scale_comparison_flags_the_upper_tail_of_clean_skewed_spending()
    {
        // Documents the defect that motivates the default. Nothing here is
        // anomalous, but on a raw scale the largest ordinary contracts sit
        // several standard deviations above a mean that the tail itself drags
        // upward, so they get flagged.
        var findings = new ZScoreOutlierDetector(Settings(transform: AmountTransform.None))
            .Detect(HeavyTailedCleanGroup());

        Assert.NotEmpty(findings);
    }

    [Fact]
    public void Log_scale_comparison_stays_silent_on_the_same_clean_skewed_spending()
    {
        var findings = new ZScoreOutlierDetector(Settings(transform: AmountTransform.Log10))
            .Detect(HeavyTailedCleanGroup());

        Assert.Empty(findings);
    }

    [Fact]
    public void Log_scale_comparison_still_catches_a_genuine_order_of_magnitude_outlier()
    {
        // Suppressing the false positives must not cost the true positive: a
        // payment far above the entire range is still caught.
        var transactions = HeavyTailedCleanGroup();
        transactions.Add(TestData.Transaction(5_000_000_000m, reference: "PLANTED"));

        var findings = new ZScoreOutlierDetector(Settings(transform: AmountTransform.Log10))
            .Detect(transactions);

        Assert.Equal("PLANTED", Assert.Single(findings).ExternalReference);
    }

    [Fact]
    public void Log_scale_comparison_drops_non_positive_amounts_that_have_no_logarithm()
    {
        var transactions = CleanGroup();
        transactions.Add(TestData.Transaction(0m, reference: "ZERO"));
        transactions.Add(TestData.Transaction(-4_000m, reference: "CREDIT"));
        transactions.Add(TestData.Transaction(50_000m, reference: "PLANTED"));

        var findings = new ZScoreOutlierDetector(Settings(transform: AmountTransform.Log10))
            .Detect(transactions);

        Assert.Equal("PLANTED", Assert.Single(findings).ExternalReference);
    }

    [Fact]
    public void Transform_is_recorded_on_the_finding_so_the_report_can_state_the_basis()
    {
        var transactions = CleanGroup();
        transactions.Add(TestData.Transaction(50_000m, reference: "PLANTED"));

        var logFinding = Assert.Single(new ZScoreOutlierDetector(
            Settings(transform: AmountTransform.Log10)).Detect(transactions));
        var rawFinding = Assert.Single(new ZScoreOutlierDetector(
            Settings(transform: AmountTransform.None)).Detect(transactions));

        Assert.Equal(AmountTransform.Log10, logFinding.Transform);
        Assert.Equal(AmountTransform.None, rawFinding.Transform);
    }

    [Fact]
    public void Detect_rejects_a_null_collection() =>
        Assert.Throws<ArgumentNullException>(() =>
            new ZScoreOutlierDetector(Settings()).Detect(null!));

    [Fact]
    public void Empty_input_produces_no_findings() =>
        Assert.Empty(new ZScoreOutlierDetector(Settings()).Detect([]));
}
