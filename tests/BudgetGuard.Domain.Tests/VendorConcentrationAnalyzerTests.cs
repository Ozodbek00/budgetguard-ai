using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Concentration;
using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Tests;

public sealed class VendorConcentrationAnalyzerTests
{
    private static VendorConcentrationSettings Settings(
        double spendShareThreshold = 0.30,
        double expectedShareMultiple = 3.0,
        int minimumVendors = 3,
        int minimumTransactions = 10) =>
        new()
        {
            SpendShareThreshold = spendShareThreshold,
            ExpectedShareMultiple = expectedShareMultiple,
            MinimumVendorsInScope = minimumVendors,
            MinimumTransactionsInScope = minimumTransactions,
            Scopes = [ConcentrationScope.Category]
        };

    /// <summary>
    /// Builds a category where <paramref name="vendorCount"/> vendors each hold
    /// <paramref name="contractsEach"/> contracts of equal value — a perfectly
    /// competitive scope.
    /// </summary>
    private static List<ProcurementTransaction> EvenlySplitCategory(
        int vendorCount = 10,
        int contractsEach = 4,
        decimal amount = 1_000_000m,
        string category = "Construction")
    {
        var transactions = new List<ProcurementTransaction>();

        for (var v = 0; v < vendorCount; v++)
        {
            for (var c = 0; c < contractsEach; c++)
            {
                transactions.Add(TestData.Transaction(amount, vendor: $"Vendor {v:D2}", category: category));
            }
        }

        return transactions;
    }

    // -----------------------------------------------------------------
    // Competitive markets must not be flagged.
    // -----------------------------------------------------------------

    [Fact]
    public void Evenly_split_category_produces_no_findings()
    {
        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(EvenlySplitCategory());

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Evenly_split_category_is_not_reported_as_highly_concentrated()
    {
        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(EvenlySplitCategory(vendorCount: 10));

        var scope = Assert.Single(result.Scopes);
        Assert.False(scope.IsHighlyConcentrated);

        // Ten vendors sharing evenly gives HHI = 10 * (10%)^2 = 1,000.
        Assert.Equal(1_000d, scope.HerfindahlHirschmanIndex, precision: 6);
        Assert.Equal(0.1d, scope.ExpectedShare, precision: 10);
    }

    [Fact]
    public void Mild_variation_between_vendors_stays_below_the_flag_thresholds()
    {
        var transactions = new List<ProcurementTransaction>();

        for (var v = 0; v < 8; v++)
        {
            // Shares range from about 8% to about 17% — uneven, but ordinary.
            var contracts = 4 + v % 3;
            for (var c = 0; c < contracts; c++)
            {
                transactions.Add(TestData.Transaction(1_000_000m, vendor: $"Vendor {v:D2}"));
            }
        }

        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(transactions);

        Assert.Empty(result.Findings);
    }

    // -----------------------------------------------------------------
    // Planted concentration must be caught, by either threshold.
    // -----------------------------------------------------------------

    [Fact]
    public void Vendor_taking_a_dominant_share_is_flagged_by_the_absolute_threshold()
    {
        var transactions = EvenlySplitCategory(vendorCount: 5, contractsEach: 4, amount: 1_000_000m);

        // Dominant vendor: 20,000,000 against 20,000,000 spread across the rest.
        transactions.AddRange(Enumerable.Range(0, 10)
            .Select(_ => TestData.Transaction(2_000_000m, vendor: "Dominant Vendor")));

        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(transactions);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("Dominant Vendor", finding.VendorName);
        Assert.Equal(0.5d, finding.SpendShare, precision: 6);
        Assert.Equal(6, finding.VendorsInScope);
    }

    [Fact]
    public void Vendor_below_the_absolute_threshold_is_still_flagged_when_the_field_is_crowded()
    {
        // 20 vendors means an even split is 5%. A vendor on 20% is under the
        // 30% absolute bound but is taking four times its expected share —
        // exactly the case a fixed percentage threshold would miss.
        var transactions = EvenlySplitCategory(vendorCount: 19, contractsEach: 4, amount: 1_000_000m);
        transactions.AddRange(Enumerable.Range(0, 16)
            .Select(_ => TestData.Transaction(1_187_500m, vendor: "Quietly Dominant")));

        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(transactions);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("Quietly Dominant", finding.VendorName);
        Assert.True(finding.SpendShare < 0.30, "Should sit below the absolute threshold.");
        Assert.True(finding.ExcessMultiple > 3.0, "Should exceed the even-split multiple.");
    }

    [Fact]
    public void Excess_multiple_is_the_share_divided_by_the_even_split_expectation()
    {
        var transactions = EvenlySplitCategory(vendorCount: 5, contractsEach: 4, amount: 1_000_000m);
        transactions.AddRange(Enumerable.Range(0, 10)
            .Select(_ => TestData.Transaction(2_000_000m, vendor: "Dominant Vendor")));

        var finding = Assert.Single(new VendorConcentrationAnalyzer(Settings()).Analyze(transactions).Findings);

        // Six vendors, so an even split is 1/6. A 50% share is 3x that.
        Assert.Equal(1d / 6d, finding.ExpectedShare, precision: 10);
        Assert.Equal(3.0d, finding.ExcessMultiple, precision: 6);
    }

    [Fact]
    public void Thresholds_are_configurable_and_not_hardcoded()
    {
        var transactions = EvenlySplitCategory(vendorCount: 5, contractsEach: 4, amount: 1_000_000m);
        transactions.AddRange(Enumerable.Range(0, 10)
            .Select(_ => TestData.Transaction(2_000_000m, vendor: "Dominant Vendor")));

        var permissive = new VendorConcentrationAnalyzer(
            Settings(spendShareThreshold: 0.90, expectedShareMultiple: 10.0)).Analyze(transactions);

        Assert.Empty(permissive.Findings);
    }

    // -----------------------------------------------------------------
    // Market capture versus one big contract. Conflating these was the
    // largest source of false positives in this detector: any genuinely
    // extreme payment also hands its vendor a huge share of scope spend.
    // -----------------------------------------------------------------

    [Fact]
    public void Vendor_holding_one_enormous_contract_is_not_called_concentrated()
    {
        // Twenty vendors with four ordinary contracts each, and one of them
        // also received a single payment worth more than the rest combined.
        // That is an amount outlier — the z-score detector's job — not evidence
        // that this vendor has captured the category.
        var transactions = EvenlySplitCategory(vendorCount: 20, contractsEach: 4, amount: 1_000_000m);
        transactions.Add(TestData.Transaction(200_000_000m, vendor: "Vendor 07"));

        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(transactions);

        var vendor = Assert.Single(result.Scopes).Vendors.Single(v => v.VendorName == "Vendor 07");
        Assert.True(vendor.SpendShare > 0.70, "The single award does dominate scope spend.");
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Vendor_winning_repeatedly_is_still_flagged()
    {
        // Same dominant spend share, but earned across many awards. This is the
        // pattern the detector exists to surface.
        var transactions = EvenlySplitCategory(vendorCount: 20, contractsEach: 4, amount: 1_000_000m);
        transactions.AddRange(Enumerable.Range(0, 40)
            .Select(_ => TestData.Transaction(5_000_000m, vendor: "Serial Winner", category: "Construction")));

        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(transactions);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("Serial Winner", finding.VendorName);
        Assert.True(finding.ContractCountZScore > 2.0);
    }

    [Fact]
    public void Contract_count_significance_threshold_is_configurable()
    {
        var transactions = EvenlySplitCategory(vendorCount: 20, contractsEach: 4, amount: 1_000_000m);
        transactions.Add(TestData.Transaction(200_000_000m, vendor: "Vendor 07"));

        var settings = Settings();
        settings.ContractCountZThreshold = -100d;
        settings.MinimumContractsForConcentration = 1;

        // With the corroborating test disabled the single award is flagged
        // again, which is what the default is there to prevent.
        Assert.NotEmpty(new VendorConcentrationAnalyzer(settings).Analyze(transactions).Findings);
    }

    [Fact]
    public void Expected_contract_count_is_the_scope_total_divided_by_vendor_count()
    {
        var transactions = EvenlySplitCategory(vendorCount: 20, contractsEach: 4, amount: 1_000_000m);
        transactions.AddRange(Enumerable.Range(0, 40)
            .Select(_ => TestData.Transaction(5_000_000m, vendor: "Serial Winner", category: "Construction")));

        var finding = Assert.Single(new VendorConcentrationAnalyzer(Settings()).Analyze(transactions).Findings);

        // 120 contracts across 21 vendors.
        Assert.Equal(120d / 21d, finding.ExpectedContractCount, precision: 6);
        Assert.Equal(40, finding.ContractCount);
    }

    // -----------------------------------------------------------------
    // Guard rails against nonsense findings.
    // -----------------------------------------------------------------

    [Fact]
    public void Scope_with_too_few_vendors_is_not_analysed()
    {
        // Two vendors means someone necessarily holds a large share. Saying so
        // would be arithmetic, not evidence.
        var transactions = new List<ProcurementTransaction>();
        transactions.AddRange(Enumerable.Range(0, 15).Select(_ => TestData.Transaction(1_000_000m, vendor: "A")));
        transactions.AddRange(Enumerable.Range(0, 3).Select(_ => TestData.Transaction(1_000_000m, vendor: "B")));

        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(transactions);

        Assert.Empty(result.Findings);
        Assert.Empty(result.Scopes);
    }

    [Fact]
    public void Scope_with_too_few_transactions_is_not_analysed()
    {
        var transactions = new List<ProcurementTransaction>
        {
            TestData.Transaction(9_000_000m, vendor: "A"),
            TestData.Transaction(500_000m, vendor: "B"),
            TestData.Transaction(500_000m, vendor: "C")
        };

        var result = new VendorConcentrationAnalyzer(Settings(minimumTransactions: 10)).Analyze(transactions);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Non_positive_amounts_are_excluded_so_credits_cannot_distort_shares()
    {
        var transactions = EvenlySplitCategory(vendorCount: 10, contractsEach: 4);
        transactions.Add(TestData.Transaction(-5_000_000m, vendor: "Vendor 00"));
        transactions.Add(TestData.Transaction(0m, vendor: "Vendor 01"));

        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(transactions);

        Assert.Empty(result.Findings);
        Assert.Equal(40_000_000m, Assert.Single(result.Scopes).TotalSpend);
    }

    [Fact]
    public void Vendor_names_are_matched_case_insensitively()
    {
        var transactions = EvenlySplitCategory(vendorCount: 5, contractsEach: 4, amount: 1_000_000m);
        transactions.AddRange(Enumerable.Range(0, 5)
            .Select(_ => TestData.Transaction(2_000_000m, vendor: "Dominant Vendor")));
        transactions.AddRange(Enumerable.Range(0, 5)
            .Select(_ => TestData.Transaction(2_000_000m, vendor: "DOMINANT VENDOR")));

        var result = new VendorConcentrationAnalyzer(Settings()).Analyze(transactions);

        // The two spellings must merge into one vendor holding 50%, not two on 25%.
        var finding = Assert.Single(result.Findings);
        Assert.Equal(0.5d, finding.SpendShare, precision: 6);
    }

    [Fact]
    public void Analyze_rejects_a_null_collection() =>
        Assert.Throws<ArgumentNullException>(() =>
            new VendorConcentrationAnalyzer(Settings()).Analyze(null!));

    [Fact]
    public void Empty_input_produces_an_empty_result()
    {
        var result = new VendorConcentrationAnalyzer(Settings()).Analyze([]);

        Assert.Empty(result.Findings);
        Assert.Empty(result.Scopes);
    }

    // -----------------------------------------------------------------
    // Herfindahl-Hirschman Index
    // -----------------------------------------------------------------

    [Fact]
    public void Hhi_reaches_the_monopoly_ceiling_when_one_vendor_holds_everything()
    {
        var transactions = new List<ProcurementTransaction>();
        transactions.AddRange(Enumerable.Range(0, 12).Select(_ => TestData.Transaction(1_000_000m, vendor: "Monopolist")));
        // Two token competitors so the scope passes the minimum-vendor guard.
        transactions.Add(TestData.Transaction(0.01m, vendor: "Token A"));
        transactions.Add(TestData.Transaction(0.01m, vendor: "Token B"));

        var scope = Assert.Single(new VendorConcentrationAnalyzer(Settings()).Analyze(transactions).Scopes);

        Assert.True(scope.IsHighlyConcentrated);
        Assert.True(scope.HerfindahlHirschmanIndex > 9_900);
    }

    [Fact]
    public void Hhi_threshold_is_configurable()
    {
        var settings = Settings();
        settings.HighlyConcentratedHhi = 500;

        var scope = Assert.Single(new VendorConcentrationAnalyzer(settings)
            .Analyze(EvenlySplitCategory(vendorCount: 10)).Scopes);

        // HHI of 1,000 is competitive by the default bound but not by this one.
        Assert.True(scope.IsHighlyConcentrated);
    }

    [Fact]
    public void Scope_summary_lists_every_vendor_largest_share_first()
    {
        var transactions = EvenlySplitCategory(vendorCount: 5, contractsEach: 4, amount: 1_000_000m);
        transactions.AddRange(Enumerable.Range(0, 10)
            .Select(_ => TestData.Transaction(2_000_000m, vendor: "Dominant Vendor")));

        var scope = Assert.Single(new VendorConcentrationAnalyzer(Settings()).Analyze(transactions).Scopes);

        Assert.Equal(6, scope.Vendors.Count);
        Assert.Equal("Dominant Vendor", scope.Vendors[0].VendorName);
        Assert.Equal(1.0d, scope.Vendors.Sum(v => v.SpendShare), precision: 6);
        Assert.Equal(1.0d, scope.Vendors.Sum(v => v.CountShare), precision: 6);
    }

    // -----------------------------------------------------------------
    // Explanations
    // -----------------------------------------------------------------

    [Fact]
    public void Vendor_explanation_states_share_expectation_and_thresholds()
    {
        var transactions = EvenlySplitCategory(vendorCount: 5, contractsEach: 4, amount: 1_000_000m);
        transactions.AddRange(Enumerable.Range(0, 10)
            .Select(_ => TestData.Transaction(2_000_000m, vendor: "Alfa Qurilish")));

        var finding = Assert.Single(new VendorConcentrationAnalyzer(Settings()).Analyze(transactions).Findings);

        Assert.Contains("Alfa Qurilish", finding.Explanation);
        Assert.Contains("50.0%", finding.Explanation);
        Assert.Contains("category \"Construction\"", finding.Explanation);
        Assert.Contains("16.7%", finding.Explanation);
        Assert.Contains("3.0x the even-split expectation", finding.Explanation);
        Assert.Contains("contracts", finding.Explanation);
    }

    [Fact]
    public void Scope_explanation_states_the_index_and_the_even_split_benchmark()
    {
        var scope = Assert.Single(new VendorConcentrationAnalyzer(Settings())
            .Analyze(EvenlySplitCategory(vendorCount: 10)).Scopes);

        Assert.Contains("Herfindahl-Hirschman Index of 1,000", scope.Explanation);
        Assert.Contains("10 vendors", scope.Explanation);
        Assert.Contains("would score 1,000", scope.Explanation);
    }

    [Fact]
    public void Normalised_score_is_bounded_and_higher_for_worse_concentration()
    {
        var mild = EvenlySplitCategory(vendorCount: 5, contractsEach: 4, amount: 1_000_000m);
        mild.AddRange(Enumerable.Range(0, 10).Select(_ => TestData.Transaction(2_000_000m, vendor: "Dominant")));

        var severe = EvenlySplitCategory(vendorCount: 5, contractsEach: 4, amount: 1_000_000m);
        severe.AddRange(Enumerable.Range(0, 10).Select(_ => TestData.Transaction(20_000_000m, vendor: "Dominant")));

        var analyzer = new VendorConcentrationAnalyzer(Settings());
        var mildScore = analyzer.Analyze(mild).Findings.Single().NormalisedScore;
        var severeScore = analyzer.Analyze(severe).Findings.Single().NormalisedScore;

        Assert.InRange(mildScore, 0d, 1d);
        Assert.InRange(severeScore, 0d, 1d);
        Assert.True(severeScore > mildScore);
    }
}
