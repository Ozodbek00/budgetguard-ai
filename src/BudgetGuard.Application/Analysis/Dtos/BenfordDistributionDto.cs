namespace BudgetGuard.Application.Analysis.Dtos;

/// <summary>Chart-ready observed versus expected frequency for one leading digit.</summary>
/// <param name="Digit">Leading digit, 1-9.</param>
/// <param name="ObservedCount">Amounts beginning with this digit.</param>
/// <param name="ObservedProportion">Observed share, 0-1.</param>
/// <param name="ExpectedCount">Count Benford's Law predicts at this sample size.</param>
/// <param name="ExpectedProportion">log10(1 + 1/d).</param>
/// <param name="DeviationPercentagePoints">Observed minus expected, in percentage points.</param>
/// <param name="ExcessCount">Observed minus expected count.</param>
public sealed record BenfordDigitDto(
    int Digit,
    int ObservedCount,
    double ObservedProportion,
    double ExpectedCount,
    double ExpectedProportion,
    double DeviationPercentagePoints,
    double ExcessCount);

/// <summary>Everything the Benford's Law view renders.</summary>
/// <param name="DatasetId">Dataset analysed.</param>
/// <param name="DatasetName">Its display name.</param>
/// <param name="IsSyntheticDemo">True when this is generated demo data.</param>
/// <param name="PopulationLabel">What was tested.</param>
/// <param name="SampleSize">Usable amounts.</param>
/// <param name="ExcludedCount">Amounts skipped as zero or negative.</param>
/// <param name="MeanAbsoluteDeviation">The primary conformity statistic.</param>
/// <param name="EffectiveMadThreshold">The threshold this sample size had to clear.</param>
/// <param name="FixedMadThreshold">The published band, before any sample-size adjustment.</param>
/// <param name="ChiSquare">Secondary statistic, reported but not decisive.</param>
/// <param name="ChiSquareCriticalValue">Its critical value at 8 degrees of freedom.</param>
/// <param name="Conformity">Verdict band.</param>
/// <param name="IsAnomalous">True when the population failed the conformity test.</param>
/// <param name="Explanation">Plain-language verdict.</param>
/// <param name="Digits">Per-digit rows, digits 1-9 in order.</param>
public sealed record BenfordDistributionDto(
    Guid DatasetId,
    string DatasetName,
    bool IsSyntheticDemo,
    string PopulationLabel,
    int SampleSize,
    int ExcludedCount,
    double MeanAbsoluteDeviation,
    double EffectiveMadThreshold,
    double FixedMadThreshold,
    double ChiSquare,
    double ChiSquareCriticalValue,
    string Conformity,
    bool IsAnomalous,
    string Explanation,
    IReadOnlyList<BenfordDigitDto> Digits);
