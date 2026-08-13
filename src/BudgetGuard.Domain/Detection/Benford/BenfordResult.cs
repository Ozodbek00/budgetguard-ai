namespace BudgetGuard.Domain.Detection.Benford;

/// <summary>Nigrini's first-digit conformity bands, plus an explicit "not enough data" verdict.</summary>
public enum BenfordConformity
{
    /// <summary>Fewer usable amounts than <see cref="BenfordSettings.MinimumSampleSize"/>. No verdict issued.</summary>
    InsufficientData = 0,

    /// <summary>MAD below the close-conformity bound. Indistinguishable from natural data.</summary>
    Close = 1,

    /// <summary>MAD within the acceptable band. Normal for real-world spending.</summary>
    Acceptable = 2,

    /// <summary>MAD in the grey zone. Worth a look, not worth an accusation.</summary>
    MarginallyAcceptable = 3,

    /// <summary>MAD above the non-conformity bound. The distribution is not behaving naturally.</summary>
    NonConformant = 4
}

/// <summary>
/// Observed versus expected frequency for a single leading digit.
/// Chart-ready: the UI plots <see cref="ObservedProportion"/> against
/// <see cref="ExpectedProportion"/> directly.
/// </summary>
/// <param name="Digit">Leading digit, 1-9.</param>
/// <param name="ObservedCount">How many amounts began with this digit.</param>
/// <param name="ObservedProportion">Observed share of the sample, 0-1.</param>
/// <param name="ExpectedCount">Count Benford's Law predicts for this sample size.</param>
/// <param name="ExpectedProportion">log10(1 + 1/d).</param>
public sealed record BenfordDigitBucket(
    int Digit,
    int ObservedCount,
    double ObservedProportion,
    double ExpectedCount,
    double ExpectedProportion)
{
    /// <summary>Observed minus expected, in percentage points. Positive means over-represented.</summary>
    public double DeviationPercentagePoints => (ObservedProportion - ExpectedProportion) * 100d;

    /// <summary>Observed count minus expected count. The "extra invoices" number an auditor can act on.</summary>
    public double ExcessCount => ObservedCount - ExpectedCount;
}

/// <summary>The full outcome of a first-digit conformity test over one population of amounts.</summary>
/// <param name="SampleSize">Usable amounts tested.</param>
/// <param name="ExcludedCount">Amounts skipped because they were zero or negative.</param>
/// <param name="Digits">Per-digit observed vs expected, digits 1-9 in order.</param>
/// <param name="MeanAbsoluteDeviation">Mean |observed - expected| proportion across the nine digits.</param>
/// <param name="ChiSquare">Pearson chi-square statistic, 8 degrees of freedom.</param>
/// <param name="ChiSquareCriticalValue">The critical value it was compared against.</param>
/// <param name="Conformity">Verdict band.</param>
/// <param name="Explanation">Plain-language summary an auditor can verify by hand.</param>
/// <param name="PopulationLabel">What was tested — the dataset, or a single vendor.</param>
/// <param name="EffectiveMadThreshold">
/// The MAD this population actually had to exceed: the larger of the fixed
/// conformity band and the sampling-noise floor for this sample size.
/// </param>
public sealed record BenfordResult(
    int SampleSize,
    int ExcludedCount,
    IReadOnlyList<BenfordDigitBucket> Digits,
    double MeanAbsoluteDeviation,
    double ChiSquare,
    double ChiSquareCriticalValue,
    BenfordConformity Conformity,
    string Explanation,
    string PopulationLabel,
    double EffectiveMadThreshold)
{
    /// <summary>True when the population failed the MAD conformity test.</summary>
    public bool IsAnomalous => Conformity == BenfordConformity.NonConformant;

    /// <summary>
    /// True when chi-square exceeds its critical value. Reported for
    /// completeness but never used alone: chi-square grows with sample size and
    /// will reject almost any large real-world dataset.
    /// </summary>
    public bool ChiSquareRejectsConformity => ChiSquare > ChiSquareCriticalValue;

    /// <summary>The digit furthest above its Benford expectation — usually where the manipulation is.</summary>
    public BenfordDigitBucket? MostOverRepresentedDigit =>
        Digits.Count == 0 ? null : Digits.MaxBy(d => d.DeviationPercentagePoints);
}
