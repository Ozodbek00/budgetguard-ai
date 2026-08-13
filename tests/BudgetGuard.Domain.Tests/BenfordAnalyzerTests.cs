using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Benford;

namespace BudgetGuard.Domain.Tests;

public sealed class BenfordAnalyzerTests
{
    private static BenfordAnalyzer Analyzer(BenfordSettings? settings = null) =>
        new(settings ?? new BenfordSettings());

    // -----------------------------------------------------------------
    // Leading digit extraction — the single most correctness-critical
    // primitive in the analyser. Everything downstream is arithmetic on
    // top of these digits.
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(1234.56, 1)]
    [InlineData(9.99, 9)]
    [InlineData(5, 5)]
    [InlineData(100000, 1)]
    [InlineData(0.0045, 4)]
    [InlineData(0.9, 9)]
    [InlineData(0.00000071, 7)]
    [InlineData(80000000, 8)]
    [InlineData(0.30, 3)]
    public void LeadingDigit_returns_first_significant_digit(decimal amount, int expected) =>
        Assert.Equal(expected, BenfordAnalyzer.LeadingDigit(amount));

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    [InlineData(-0.25)]
    public void LeadingDigit_returns_null_for_non_positive_amounts(decimal amount) =>
        Assert.Null(BenfordAnalyzer.LeadingDigit(amount));

    [Fact]
    public void LeadingDigit_is_unaffected_by_trailing_zeros_in_the_decimal_scale()
    {
        // 3.0m and 3.00m are equal but carry different scale; the digit must not change.
        Assert.Equal(3, BenfordAnalyzer.LeadingDigit(3.0m));
        Assert.Equal(3, BenfordAnalyzer.LeadingDigit(3.00m));
        Assert.Equal(3, BenfordAnalyzer.LeadingDigit(3.000000m));
    }

    // -----------------------------------------------------------------
    // Expected distribution
    // -----------------------------------------------------------------

    [Fact]
    public void Expected_proportions_match_the_Benford_law_and_sum_to_one()
    {
        Assert.Equal(0.30103, BenfordAnalyzer.ExpectedProportion(1), precision: 5);
        Assert.Equal(0.17609, BenfordAnalyzer.ExpectedProportion(2), precision: 5);
        Assert.Equal(0.04576, BenfordAnalyzer.ExpectedProportion(9), precision: 5);

        var total = Enumerable.Range(1, 9).Sum(BenfordAnalyzer.ExpectedProportion);
        Assert.Equal(1.0, total, precision: 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Expected_proportion_rejects_digits_outside_one_to_nine(int digit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BenfordAnalyzer.ExpectedProportion(digit));

    // -----------------------------------------------------------------
    // Clean data must NOT be flagged. A forensic tool that cries wolf on
    // honest ledgers is worse than useless — auditors stop reading it.
    // -----------------------------------------------------------------

    [Fact]
    public void Exactly_Benford_distributed_amounts_are_reported_as_close_conformity()
    {
        var result = Analyzer().Analyze(TestData.ExactBenfordAmounts(10_000), "clean dataset");

        Assert.Equal(BenfordConformity.Close, result.Conformity);
        Assert.False(result.IsAnomalous);
        Assert.True(result.MeanAbsoluteDeviation < 0.006,
            $"MAD {result.MeanAbsoluteDeviation:F5} should be inside the close-conformity band.");
    }

    [Fact]
    public void Randomly_sampled_Benford_data_is_not_flagged_as_non_conformant()
    {
        var amounts = TestData.BenfordConforming(5_000).Select(t => t.Amount);

        var result = Analyzer().Analyze(amounts, "sampled clean dataset");

        Assert.False(result.IsAnomalous);
        Assert.NotEqual(BenfordConformity.InsufficientData, result.Conformity);
    }

    // -----------------------------------------------------------------
    // Planted manipulations must be caught.
    // -----------------------------------------------------------------

    [Fact]
    public void Uniformly_distributed_first_digits_are_flagged_as_non_conformant()
    {
        // Every digit equally likely is precisely what Benford's Law says does
        // not happen in natural financial data.
        var result = Analyzer().Analyze(TestData.UniformDigitAmounts(200), "uniform dataset");

        Assert.Equal(BenfordConformity.NonConformant, result.Conformity);
        Assert.True(result.IsAnomalous);
        Assert.True(result.MeanAbsoluteDeviation > 0.05);
    }

    [Fact]
    public void Amounts_clustered_below_an_approval_ceiling_are_flagged()
    {
        // 300 clean amounts plus 200 contracts priced just under a 50,000,000
        // sign-off limit — the classic threshold-evasion signature.
        var amounts = TestData.ExactBenfordAmounts(300).ToList();
        amounts.AddRange(Enumerable.Range(0, 200)
            .Select(i => 40_000_000m + i * 45_000m));

        var result = Analyzer().Analyze(amounts, "threshold evasion dataset");

        Assert.True(result.IsAnomalous);

        var digitFour = result.Digits.Single(d => d.Digit == 4);
        Assert.True(digitFour.ObservedProportion > digitFour.ExpectedProportion * 2,
            "Digit 4 should be heavily over-represented by threshold evasion.");
        Assert.Equal(4, result.MostOverRepresentedDigit!.Digit);
    }

    [Fact]
    public void Round_number_invoicing_is_flagged()
    {
        // A vendor whose invoices are all round multiples of five million.
        var choices = new[] { 25m, 30m, 50m, 75m, 20m };
        var amounts = Enumerable.Range(0, 300)
            .Select(i => choices[i % choices.Length] * 1_000_000m)
            .ToList();

        var result = Analyzer().Analyze(amounts, "round-number vendor");

        Assert.True(result.IsAnomalous);
        Assert.Equal(0, result.Digits.Single(d => d.Digit == 1).ObservedCount);
    }

    // -----------------------------------------------------------------
    // Guard rails
    // -----------------------------------------------------------------

    [Fact]
    public void Samples_below_the_minimum_size_produce_no_verdict()
    {
        var result = Analyzer().Analyze(TestData.ExactBenfordAmounts(20), "tiny dataset");

        Assert.Equal(BenfordConformity.InsufficientData, result.Conformity);
        Assert.False(result.IsAnomalous);
        Assert.Contains("at least 50", result.Explanation);
    }

    [Fact]
    public void Minimum_sample_size_is_configurable_and_not_hardcoded()
    {
        var amounts = TestData.ExactBenfordAmounts(20);

        var permissive = Analyzer(new BenfordSettings { MinimumSampleSize = 10 })
            .Analyze(amounts, "tiny dataset");

        Assert.NotEqual(BenfordConformity.InsufficientData, permissive.Conformity);
    }

    [Fact]
    public void Non_positive_amounts_are_excluded_and_counted_rather_than_silently_dropped()
    {
        var amounts = TestData.ExactBenfordAmounts(200).ToList();
        amounts.AddRange([0m, -100m, -2500m, 0m]);

        var result = Analyzer().Analyze(amounts, "dataset with credits");

        Assert.Equal(4, result.ExcludedCount);
        Assert.Equal(amounts.Count - 4, result.SampleSize);
    }

    [Fact]
    public void Empty_input_reports_insufficient_data_rather_than_throwing()
    {
        var result = Analyzer().Analyze([], "empty dataset");

        Assert.Equal(BenfordConformity.InsufficientData, result.Conformity);
        Assert.Equal(0, result.SampleSize);
        Assert.Equal(9, result.Digits.Count);
    }

    [Fact]
    public void Analyze_rejects_a_null_sequence() =>
        Assert.Throws<ArgumentNullException>(() => Analyzer().Analyze(null!, "x"));

    // -----------------------------------------------------------------
    // Output shape — the chart and the explanation are the product.
    // -----------------------------------------------------------------

    [Fact]
    public void Result_always_carries_nine_digit_buckets_in_ascending_order()
    {
        var result = Analyzer().Analyze(TestData.ExactBenfordAmounts(500), "dataset");

        Assert.Equal(9, result.Digits.Count);
        Assert.Equal(Enumerable.Range(1, 9), result.Digits.Select(d => d.Digit));
        Assert.Equal(result.SampleSize, result.Digits.Sum(d => d.ObservedCount));
        Assert.Equal(1.0, result.Digits.Sum(d => d.ObservedProportion), precision: 6);
    }

    [Fact]
    public void Explanation_states_the_measured_deviation_and_the_threshold_it_was_judged_against()
    {
        var result = Analyzer().Analyze(TestData.UniformDigitAmounts(200), "Q3 spending");

        Assert.Contains("Q3 spending", result.Explanation);
        Assert.Contains("Mean Absolute Deviation", result.Explanation);
        Assert.Contains("0.015", result.Explanation);
        Assert.Contains("Chi-square", result.Explanation);
        Assert.Contains("does not follow Benford's Law", result.Explanation);
    }

    [Fact]
    public void Chi_square_is_reported_but_does_not_drive_the_verdict()
    {
        // A large clean sample: chi-square is sensitive to sample size and may
        // fire, but the MAD verdict must stay conformant. This is exactly why
        // MAD is the primary test.
        var result = Analyzer().Analyze(TestData.ExactBenfordAmounts(50_000), "large clean dataset");

        Assert.False(result.IsAnomalous);
        Assert.True(result.ChiSquare >= 0);
    }

    [Fact]
    public void Excess_count_reports_how_many_extra_invoices_lead_with_a_digit()
    {
        var amounts = Enumerable.Repeat(5_000_000m, 100)
            .Concat(TestData.ExactBenfordAmounts(100))
            .ToList();

        var result = Analyzer().Analyze(amounts, "dataset");
        var digitFive = result.Digits.Single(d => d.Digit == 5);

        Assert.True(digitFive.ExcessCount > 80,
            "100 planted amounts leading with 5 should show up as a large excess over expectation.");
    }
}
