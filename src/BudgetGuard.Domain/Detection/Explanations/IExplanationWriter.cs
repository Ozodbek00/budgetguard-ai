using BudgetGuard.Domain.Detection.Benford;

namespace BudgetGuard.Domain.Detection.Explanations;

/// <summary>Everything the first-digit sentence needs, without the sentence itself.</summary>
/// <param name="PopulationLabel">What was tested — the dataset, or one vendor.</param>
/// <param name="SampleSize">Usable amounts.</param>
/// <param name="MeanAbsoluteDeviation">Measured MAD.</param>
/// <param name="EffectiveThreshold">The MAD this population had to clear.</param>
/// <param name="FixedThreshold">The published band, before any sample-size adjustment.</param>
/// <param name="NoiseFloor">MAD expected from sampling noise alone at this sample size.</param>
/// <param name="ChiSquare">Chi-square statistic.</param>
/// <param name="ChiSquareCriticalValue">Its critical value at 8 degrees of freedom.</param>
/// <param name="Conformity">Verdict band.</param>
/// <param name="Worst">The digit furthest above its expectation.</param>
public sealed record BenfordExplanationContext(
    string PopulationLabel,
    int SampleSize,
    double MeanAbsoluteDeviation,
    double EffectiveThreshold,
    double FixedThreshold,
    double NoiseFloor,
    double ChiSquare,
    double ChiSquareCriticalValue,
    BenfordConformity Conformity,
    BenfordDigitBucket Worst);

/// <summary>Everything the amount-outlier sentence needs.</summary>
/// <param name="Reference">Source-system reference of the flagged payment.</param>
/// <param name="Amount">The flagged amount.</param>
/// <param name="Currency">Its currency code.</param>
/// <param name="VendorName">Supplier paid.</param>
/// <param name="Grouping">Which peer group produced the flag.</param>
/// <param name="GroupKey">That peer group's name.</param>
/// <param name="GroupSize">Members in the peer group.</param>
/// <param name="Method">Classic or modified statistic.</param>
/// <param name="Transform">Raw or logarithmic comparison scale.</param>
/// <param name="Score">Signed test statistic.</param>
/// <param name="Threshold">Absolute value it had to exceed.</param>
/// <param name="Centre">Group centre, in currency.</param>
/// <param name="Dispersion">Group dispersion — currency, or a multiplicative factor under a log scale.</param>
public sealed record OutlierExplanationContext(
    string Reference,
    decimal Amount,
    string Currency,
    string VendorName,
    OutlierGrouping Grouping,
    string GroupKey,
    int GroupSize,
    OutlierMethod Method,
    AmountTransform Transform,
    double Score,
    double Threshold,
    decimal Centre,
    decimal Dispersion);

/// <summary>Everything the vendor-concentration sentence needs.</summary>
/// <param name="VendorName">Supplier.</param>
/// <param name="Scope">Category or department.</param>
/// <param name="ScopeKey">Its name.</param>
/// <param name="Spend">Awarded to this vendor in scope.</param>
/// <param name="ScopeTotalSpend">Total scope spend.</param>
/// <param name="SpendShare">Share of scope spend, 0-1.</param>
/// <param name="ExpectedShare">Even-split benchmark, 1/N.</param>
/// <param name="ExcessMultiple">SpendShare over ExpectedShare.</param>
/// <param name="ContractCount">Contracts held in scope.</param>
/// <param name="ScopeTransactionCount">Contracts in scope.</param>
/// <param name="CountShare">Share of contract count, 0-1.</param>
/// <param name="VendorsInScope">Distinct vendors competing.</param>
/// <param name="ExpectedContractCount">Contracts chance would give them.</param>
/// <param name="ContractCountZScore">Standard deviations above chance.</param>
/// <param name="SpendShareThreshold">Configured absolute bound.</param>
/// <param name="ExpectedShareMultipleThreshold">Configured relative bound.</param>
/// <param name="ContractCountZThreshold">Configured count-significance bound.</param>
public sealed record ConcentrationExplanationContext(
    string VendorName,
    ConcentrationScope Scope,
    string ScopeKey,
    decimal Spend,
    decimal ScopeTotalSpend,
    double SpendShare,
    double ExpectedShare,
    double ExcessMultiple,
    int ContractCount,
    int ScopeTransactionCount,
    double CountShare,
    int VendorsInScope,
    double ExpectedContractCount,
    double ContractCountZScore,
    double SpendShareThreshold,
    double ExpectedShareMultipleThreshold,
    double ContractCountZThreshold);

/// <summary>Everything the scope-competitiveness sentence needs.</summary>
/// <param name="Scope">Category or department.</param>
/// <param name="ScopeKey">Its name.</param>
/// <param name="VendorCount">Distinct vendors competing.</param>
/// <param name="Hhi">Herfindahl-Hirschman Index, 0-10,000.</param>
/// <param name="HighlyConcentratedThreshold">Bound above which a market is called concentrated.</param>
public sealed record ScopeExplanationContext(
    ConcentrationScope Scope,
    string ScopeKey,
    int VendorCount,
    double Hhi,
    double HighlyConcentratedThreshold);

/// <summary>
/// Turns a detector's numbers into the sentence an auditor reads.
/// <para>
/// <b>Why this is an abstraction rather than string interpolation in the
/// detectors.</b> The explanation is the product: a flag an auditor cannot
/// re-derive is worthless. That makes the sentence part of the deliverable in
/// every language the tool is used in — and in Uzbekistan that means Uzbek and
/// Russian as well as English.
/// </para>
/// <para>
/// Localisation normally arrives via <c>IStringLocalizer</c>, which is an
/// ASP.NET Core dependency and therefore forbidden here: this project has no
/// package references, which is what keeps the detection algorithms testable in
/// isolation. So translation is expressed as an interface with one
/// implementation per language, all of them plain classes over plain types.
/// </para>
/// <para>
/// Each implementation owns its <b>number formatting as well as its wording</b>.
/// That is deliberate: a Russian reader expects 16 446 676 399 and 31,1%, an
/// English one 16,446,676,399 and 31.1%. Leaving formatting to the ambient
/// culture would also make the English output depend on the machine's locale,
/// which would have made the explanation assertions in the test suite pass or
/// fail according to where they ran.
/// </para>
/// </summary>
public interface IExplanationWriter
{
    /// <summary>BCP-47 tag this writer produces, e.g. "en", "uz", "ru".</summary>
    string LanguageTag { get; }

    /// <summary>The full first-digit verdict sentence.</summary>
    string Benford(BenfordExplanationContext context);

    /// <summary>The "not enough data to judge" sentence.</summary>
    string BenfordInsufficientData(string populationLabel, int sampleSize, int minimumSampleSize);

    /// <summary>The amount-outlier sentence.</summary>
    string Outlier(OutlierExplanationContext context);

    /// <summary>The vendor-concentration sentence.</summary>
    string Concentration(ConcentrationExplanationContext context);

    /// <summary>The scope-competitiveness sentence.</summary>
    string Scope(ScopeExplanationContext context);

    /// <summary>Localised label for a detector, used in evidence tables and the report UI.</summary>
    string DetectorName(DetectorKind detector);

    /// <summary>Localised names for the keys in a signal's evidence dictionary.</summary>
    string EvidenceLabel(EvidenceKey key);
}

/// <summary>
/// Stable identifiers for the named statistics attached to a signal.
/// <para>
/// The evidence dictionary used to be keyed by English display strings, which
/// made those strings simultaneously a UI label and a data key — so translating
/// the label would have silently changed the API's JSON shape.
/// </para>
/// </summary>
public enum EvidenceKey
{
    SampleSize,
    ExcludedAmounts,
    MeanAbsoluteDeviation,
    ThresholdApplied,
    Conformity,
    ChiSquare,
    ChiSquareCriticalValue,
    Method,
    Grouping,
    PeerGroup,
    PeerGroupSize,
    TestStatistic,
    Threshold,
    GroupCentre,
    GroupDispersion,
    Scope,
    VendorSpend,
    ScopeSpend,
    SpendShare,
    EvenSplitExpectation,
    ExcessMultiple,
    ContractsHeld,
    ContractsExpectedByChance,
    ContractCountExcess,
    VendorsInScope
}
