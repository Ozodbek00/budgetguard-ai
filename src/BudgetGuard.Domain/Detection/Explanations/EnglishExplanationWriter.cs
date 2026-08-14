using System.Globalization;
using BudgetGuard.Domain.Detection.Benford;

namespace BudgetGuard.Domain.Detection.Explanations;

/// <summary>
/// English explanations. The reference rendering: the API serves these, the
/// test suite asserts against them, and the other languages are translations of
/// these sentences.
/// <para>
/// Formats against <see cref="CultureInfo.InvariantCulture"/> rather than the
/// ambient culture, so output does not change with the locale of the machine or
/// container the analysis happens to run on.
/// </para>
/// </summary>
public sealed class EnglishExplanationWriter : IExplanationWriter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <inheritdoc />
    public string LanguageTag => "en";

    private static string N0(decimal v) => v.ToString("N0", Culture);
    private static string N0(double v) => v.ToString("N0", Culture);
    private static string N0(int v) => v.ToString("N0", Culture);
    private static string N1(decimal v) => v.ToString("N1", Culture);
    private static string N1(double v) => v.ToString("N1", Culture);
    private static string F1(double v) => v.ToString("F1", Culture);
    private static string F3(double v) => v.ToString("F3", Culture);
    private static string F4(double v) => v.ToString("F4", Culture);

    private static string Percent(double proportion, int decimals = 1) =>
        (proportion * 100d).ToString($"F{decimals}", Culture) + "%";

    /// <inheritdoc />
    public string Benford(BenfordExplanationContext c)
    {
        var verdict = c.Conformity switch
        {
            BenfordConformity.Close =>
                "closely follows Benford's Law — consistent with naturally occurring spending",
            BenfordConformity.Acceptable =>
                "acceptably follows Benford's Law — no digit-level concern",
            BenfordConformity.MarginallyAcceptable =>
                "is marginally outside Benford conformity — worth reviewing, not conclusive",
            _ =>
                "does not follow Benford's Law, which is consistent with amounts having been " +
                "manufactured, rounded, or split rather than arising naturally"
        };

        var thresholdBasis = c.EffectiveThreshold > c.FixedThreshold
            ? $"{F4(c.EffectiveThreshold)}, raised above the standard {F3(c.FixedThreshold)} " +
              $"band because a sample of only {N0(c.SampleSize)} amounts would show a deviation of about " +
              $"{F4(c.NoiseFloor)} by chance alone"
            : F4(c.EffectiveThreshold);

        var headline =
            $"The first-digit distribution for {c.PopulationLabel} {verdict}. " +
            $"Mean Absolute Deviation is {F4(c.MeanAbsoluteDeviation)} against a non-conformity threshold of " +
            $"{thresholdBasis} (measured over {N0(c.SampleSize)} amounts).";

        var digitDetail =
            $" The largest single deviation is digit {c.Worst.Digit}: it leads " +
            $"{Percent(c.Worst.ObservedProportion)} of amounts where Benford's Law predicts " +
            $"{Percent(c.Worst.ExpectedProportion)} — {N0(c.Worst.ObservedCount)} observed against " +
            $"{N0(c.Worst.ExpectedCount)} expected, an excess of {N0(c.Worst.ExcessCount)}.";

        var chiDetail =
            $" Chi-square is {F1(c.ChiSquare)} against a critical value of " +
            $"{F3(c.ChiSquareCriticalValue)} at 8 degrees of freedom" +
            (c.ChiSquare > c.ChiSquareCriticalValue
                ? " (also rejects conformity)."
                : " (does not reject conformity).");

        return headline + digitDetail + chiDetail;
    }

    /// <inheritdoc />
    public string BenfordInsufficientData(string populationLabel, int sampleSize, int minimumSampleSize) =>
        $"Only {N0(sampleSize)} usable amount(s) available for {populationLabel}; " +
        $"at least {N0(minimumSampleSize)} are required before a first-digit " +
        "verdict is statistically meaningful. No conclusion drawn.";

    /// <inheritdoc />
    public string Outlier(OutlierExplanationContext c)
    {
        var isLog = c.Transform == AmountTransform.Log10;
        var isClassic = c.Method == OutlierMethod.ClassicZScore;

        var unit = isClassic ? "standard deviations" : "modified z-score units";
        var direction = c.Score > 0 ? "above" : "below";
        var group = GroupingLabel(c.Grouping);

        var opening =
            $"Payment {c.Reference} of {N0(c.Amount)} {c.Currency} " +
            $"to \"{c.VendorName}\" is {F1(Math.Abs(c.Score))} {unit} {direction} ";

        var basis = (isClassic, isLog) switch
        {
            (true, true) =>
                $"the typical payment for {group} \"{c.GroupKey}\" " +
                $"(geometric mean {N0(c.Centre)} {c.Currency}; one standard deviation is a " +
                $"factor of {N1(c.Dispersion)}x, across {N0(c.GroupSize)} payments)",

            (true, false) =>
                $"the mean for {group} \"{c.GroupKey}\" " +
                $"(mean {N0(c.Centre)}, standard deviation {N0(c.Dispersion)} across {N0(c.GroupSize)} payments)",

            (false, true) =>
                $"the median payment for {group} \"{c.GroupKey}\" " +
                $"(median {N0(c.Centre)} {c.Currency}; median absolute deviation is a factor of " +
                $"{N1(c.Dispersion)}x, across {N0(c.GroupSize)} payments)",

            _ =>
                $"the median for {group} \"{c.GroupKey}\" " +
                $"(median {N0(c.Centre)}, median absolute deviation {N0(c.Dispersion)} across {N0(c.GroupSize)} payments)"
        };

        var closing = $". The flag threshold is {F1(c.Threshold)} {unit}.";

        if (isLog)
        {
            closing +=
                " Amounts are compared on a logarithmic scale, so this measures how many times larger " +
                "or smaller the payment is than its peers rather than by how many units — the correct " +
                "comparison for spending, which is heavily right-skewed.";
        }

        if (!isClassic)
        {
            closing +=
                " This statistic is built on the median rather than the mean, so a large payment cannot " +
                "hide behind the dispersion it creates.";
        }

        return opening + basis + closing;
    }

    /// <inheritdoc />
    public string Concentration(ConcentrationExplanationContext c)
    {
        var scope = ScopeLabel(c.Scope);

        return
            $"\"{c.VendorName}\" received {Percent(c.SpendShare)} of all spend in " +
            $"{scope} \"{c.ScopeKey}\" ({N0(c.Spend)} of {N0(c.ScopeTotalSpend)}), " +
            $"versus an expected {Percent(c.ExpectedShare)} if the {N0(c.VendorsInScope)} vendors " +
            $"competing in that {scope} shared it evenly — {F1(c.ExcessMultiple)}x the even-split " +
            $"expectation. This is not a single large award: they hold {N0(c.ContractCount)} of " +
            $"{N0(c.ScopeTransactionCount)} contracts ({Percent(c.CountShare)}) where an even " +
            $"award process would give them about {N1(c.ExpectedContractCount)}, an excess of " +
            $"{F1(c.ContractCountZScore)} standard deviations. The flag thresholds are " +
            $"{Percent(c.SpendShareThreshold, 0)} of spend or " +
            $"{F1(c.ExpectedShareMultipleThreshold)}x the even-split expectation, together with a contract " +
            $"count at least {F1(c.ContractCountZThreshold)} standard deviations above chance.";
    }

    /// <inheritdoc />
    public string Scope(ScopeExplanationContext c)
    {
        var scope = ScopeLabel(c.Scope);
        var evenSplitHhi = c.VendorCount == 0 ? 0d : 10_000d / c.VendorCount;

        var verdict = c.Hhi > c.HighlyConcentratedThreshold
            ? $"Above {N0(c.HighlyConcentratedThreshold)} is classed as a highly concentrated " +
              "market under the US DOJ/FTC Horizontal Merger Guidelines"
            : $"This is below the {N0(c.HighlyConcentratedThreshold)} threshold at which a market " +
              "is classed as highly concentrated";

        return
            $"{char.ToUpperInvariant(scope[0])}{scope[1..]} \"{c.ScopeKey}\" " +
            $"has a Herfindahl-Hirschman Index of {N0(c.Hhi)} across {N0(c.VendorCount)} vendors. " +
            $"{verdict}; an even split among {N0(c.VendorCount)} vendors would score {N0(evenSplitHhi)}.";
    }

    /// <inheritdoc />
    public string DetectorName(DetectorKind detector) => detector switch
    {
        DetectorKind.Benford => "Benford's Law",
        DetectorKind.ZScoreOutlier => "Amount outlier",
        DetectorKind.VendorConcentration => "Vendor concentration",
        _ => detector.ToString()
    };

    /// <inheritdoc />
    public string EvidenceLabel(EvidenceKey key) => key switch
    {
        EvidenceKey.SampleSize => "Sample size",
        EvidenceKey.ExcludedAmounts => "Excluded (zero/negative)",
        EvidenceKey.MeanAbsoluteDeviation => "Mean absolute deviation",
        EvidenceKey.ThresholdApplied => "Threshold applied",
        EvidenceKey.Conformity => "Conformity",
        EvidenceKey.ChiSquare => "Chi-square",
        EvidenceKey.ChiSquareCriticalValue => "Chi-square critical value",
        EvidenceKey.Method => "Method",
        EvidenceKey.Grouping => "Grouping",
        EvidenceKey.PeerGroup => "Peer group",
        EvidenceKey.PeerGroupSize => "Peer group size",
        EvidenceKey.TestStatistic => "Test statistic",
        EvidenceKey.Threshold => "Threshold",
        EvidenceKey.GroupCentre => "Group centre",
        EvidenceKey.GroupDispersion => "Group dispersion",
        EvidenceKey.Scope => "Scope",
        EvidenceKey.VendorSpend => "Vendor spend",
        EvidenceKey.ScopeSpend => "Scope spend",
        EvidenceKey.SpendShare => "Spend share",
        EvidenceKey.EvenSplitExpectation => "Even-split expectation",
        EvidenceKey.ExcessMultiple => "Excess multiple",
        EvidenceKey.ContractsHeld => "Contracts held",
        EvidenceKey.ContractsExpectedByChance => "Contracts expected by chance",
        EvidenceKey.ContractCountExcess => "Contract count excess",
        EvidenceKey.VendorsInScope => "Vendors in scope",
        _ => key.ToString()
    };

    private static string GroupingLabel(OutlierGrouping grouping) => grouping switch
    {
        OutlierGrouping.Vendor => "vendor",
        OutlierGrouping.Category => "category",
        _ => "department"
    };

    private static string ScopeLabel(ConcentrationScope scope) =>
        scope == ConcentrationScope.Category ? "category" : "department";
}
