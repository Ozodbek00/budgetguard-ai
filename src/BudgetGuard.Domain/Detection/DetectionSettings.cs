namespace BudgetGuard.Domain.Detection;

/// <summary>
/// Every threshold the detection engine uses, in one place.
/// <para>
/// Bound from the <c>Detection</c> section of appsettings.json. Nothing in the
/// detectors is a hardcoded magic number: an auditor tuning this tool for their
/// own jurisdiction must be able to see and change every decision boundary,
/// and every published finding must be reproducible from a known settings set.
/// </para>
/// </summary>
public sealed class DetectionSettings
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Detection";

    public BenfordSettings Benford { get; set; } = new();
    public ZScoreSettings ZScore { get; set; } = new();
    public VendorConcentrationSettings VendorConcentration { get; set; } = new();
    public ScoringSettings Scoring { get; set; } = new();
}

/// <summary>Thresholds for first-digit (Benford's Law) conformity testing.</summary>
public sealed class BenfordSettings
{
    /// <summary>
    /// Minimum number of usable amounts before a Benford verdict is issued.
    /// <para>
    /// Below roughly 50 records the first-digit histogram is dominated by
    /// sampling noise and will produce confident-looking nonsense. Under this
    /// count the analyser reports <see cref="Benford.BenfordConformity.InsufficientData"/>
    /// rather than guessing.
    /// </para>
    /// </summary>
    public int MinimumSampleSize { get; set; } = 50;

    /// <summary>
    /// Mean Absolute Deviation boundary above which a distribution is called
    /// non-conformant. Default 0.015 follows Nigrini's published first-digit
    /// conformity bands. MAD is the primary verdict because, unlike chi-square,
    /// it does not scale with sample size.
    /// </summary>
    public double MadNonConformityThreshold { get; set; } = 0.015;

    /// <summary>Upper bound of Nigrini's "close conformity" band.</summary>
    public double MadCloseConformityThreshold { get; set; } = 0.006;

    /// <summary>Upper bound of Nigrini's "acceptable conformity" band.</summary>
    public double MadAcceptableConformityThreshold { get; set; } = 0.012;

    /// <summary>
    /// Chi-square critical value for 8 degrees of freedom. Default 15.507 is
    /// the alpha = 0.05 critical value. Reported alongside MAD as a secondary,
    /// sample-size-sensitive signal — never as the sole verdict.
    /// </summary>
    public double ChiSquareCriticalValue { get; set; } = 15.507;

    /// <summary>
    /// Minimum transactions a single vendor needs before per-vendor Benford
    /// analysis runs for them. Higher than the dataset-level minimum is not
    /// required, but the same floor applies.
    /// </summary>
    public int MinimumVendorSampleSize { get; set; } = 50;

    /// <summary>
    /// How many multiples of the sampling-noise floor a deviation must exceed,
    /// on top of the fixed MAD band, before non-conformity is declared.
    /// <para>
    /// Nigrini's bands are calibrated for large populations. On a small sample
    /// the MAD is inflated by chance alone — around 0.024 at 100 records, well
    /// above the 0.015 band — so applying the fixed threshold to a single
    /// vendor's invoices flags essentially every vendor. The analyser therefore
    /// also compares against the deviation expected from sampling noise at that
    /// sample size. Default 2.0. Setting this to 0 restores the pure fixed-band
    /// behaviour.
    /// </para>
    /// </summary>
    public double NoiseFloorMultiple { get; set; } = 2.0;
}

/// <summary>Thresholds for amount-outlier detection within peer groups.</summary>
public sealed class ZScoreSettings
{
    /// <summary>
    /// Number of standard deviations from the group mean beyond which a
    /// transaction is flagged. Default 3.0 corresponds to roughly 0.27% of
    /// observations under a normal distribution.
    /// </summary>
    public double Threshold { get; set; } = 3.0;

    /// <summary>
    /// Threshold used when <see cref="Method"/> is
    /// <see cref="OutlierMethod.ModifiedZScore"/>. Default 3.5 is the value
    /// recommended by Iglewicz and Hoaglin.
    /// </summary>
    public double ModifiedThreshold { get; set; } = 3.5;

    /// <summary>
    /// Which statistic to use. <see cref="OutlierMethod.ModifiedZScore"/> is
    /// median/MAD based and therefore resistant to the outlier inflating the
    /// very dispersion estimate used to judge it (the masking problem).
    /// </summary>
    public OutlierMethod Method { get; set; } = OutlierMethod.ClassicZScore;

    /// <summary>
    /// Minimum group size before outlier detection runs on that group. With
    /// fewer members the mean and standard deviation are too unstable to
    /// support a defensible flag.
    /// </summary>
    public int MinimumGroupSize { get; set; } = 8;

    /// <summary>
    /// Scale on which amounts are compared. Defaults to
    /// <see cref="AmountTransform.Log10"/> because procurement spending is
    /// heavy-tailed: on a raw scale the top few percent of any realistic peer
    /// group exceeds three standard deviations purely because the distribution
    /// is skewed, not because anything is wrong. See
    /// docs/DETECTION_METHODOLOGY.md for the measured effect.
    /// </summary>
    public AmountTransform Transform { get; set; } = AmountTransform.Log10;

    /// <summary>Peer groupings each transaction is compared within.</summary>
    public List<OutlierGrouping> Groupings { get; set; } =
        [OutlierGrouping.Vendor, OutlierGrouping.Category, OutlierGrouping.Department];
}

/// <summary>Thresholds for supplier-concentration analysis.</summary>
public sealed class VendorConcentrationSettings
{
    /// <summary>
    /// Absolute share of scope spend above which a vendor is flagged
    /// regardless of how many competitors exist. Default 0.30.
    /// </summary>
    public double SpendShareThreshold { get; set; } = 0.30;

    /// <summary>
    /// Multiple of the even-split expectation (1/N vendors) above which a
    /// vendor is flagged. Default 3.0 means "taking triple what an even split
    /// would give you". This is the scale-aware companion to the absolute
    /// threshold: in a scope with 40 vendors, 15% is already extraordinary.
    /// </summary>
    public double ExpectedShareMultiple { get; set; } = 3.0;

    /// <summary>
    /// How many standard deviations a vendor's contract count must exceed
    /// random allocation by, in addition to breaching a spend threshold.
    /// <para>
    /// Under an even award process a vendor's number of contracts in a scope
    /// follows Binomial(T, 1/N), so the expected count is T/N with standard
    /// deviation sqrt(T x 1/N x (1 - 1/N)). Requiring a significant excess here
    /// separates the two very different situations that a raw spend share
    /// conflates: a supplier that keeps winning work (market capture, the thing
    /// this detector is for) versus a supplier that won one enormous contract
    /// (an amount outlier, which the z-score detector already reports). Without
    /// this test, every large one-off payment also brands its vendor as
    /// concentrated.
    /// </para>
    /// </summary>
    public double ContractCountZThreshold { get; set; } = 2.0;

    /// <summary>
    /// Absolute floor on contracts held before a concentration flag is
    /// possible, so a vendor with one or two awards is never described as
    /// dominating a market however large those awards were.
    /// </summary>
    public int MinimumContractsForConcentration { get; set; } = 3;

    /// <summary>Minimum distinct vendors in a scope before it is analysed.</summary>
    public int MinimumVendorsInScope { get; set; } = 3;

    /// <summary>Minimum transactions in a scope before it is analysed.</summary>
    public int MinimumTransactionsInScope { get; set; } = 10;

    /// <summary>
    /// Herfindahl-Hirschman Index above which the scope itself (not any single
    /// vendor) is reported as a concentrated market. Default 2500 matches the
    /// US DOJ/FTC Horizontal Merger Guidelines "highly concentrated" boundary.
    /// </summary>
    public double HighlyConcentratedHhi { get; set; } = 2500;

    /// <summary>Scopes within which competition is assessed.</summary>
    public List<ConcentrationScope> Scopes { get; set; } =
        [ConcentrationScope.Category, ConcentrationScope.Department];
}

/// <summary>How individual detector signals combine into one ranked risk score.</summary>
public sealed class ScoringSettings
{
    /// <summary>Weight applied to first-digit conformity signals.</summary>
    public double BenfordWeight { get; set; } = 1.0;

    /// <summary>Weight applied to amount-outlier signals.</summary>
    public double ZScoreWeight { get; set; } = 1.0;

    /// <summary>Weight applied to vendor-concentration signals.</summary>
    public double VendorConcentrationWeight { get; set; } = 1.2;

    /// <summary>
    /// Bonus applied per additional independent detector implicating the same
    /// subject. Two methods that share no inputs agreeing on a vendor is
    /// materially stronger evidence than either alone, so corroboration is
    /// rewarded — but capped, so it can never manufacture a flag on its own.
    /// </summary>
    public double CorroborationBonus { get; set; } = 0.15;

    /// <summary>Risk score at or above which a finding is Critical.</summary>
    public double CriticalThreshold { get; set; } = 0.75;

    /// <summary>Risk score at or above which a finding is High.</summary>
    public double HighThreshold { get; set; } = 0.50;

    /// <summary>Risk score at or above which a finding is Medium.</summary>
    public double MediumThreshold { get; set; } = 0.25;
}

/// <summary>The scale on which amounts are compared against their peers.</summary>
public enum AmountTransform
{
    /// <summary>Compare raw currency amounts. Intuitive, but skew-sensitive.</summary>
    None = 0,

    /// <summary>
    /// Compare base-10 logarithms, so a flag means "unusual by a factor",
    /// not "unusual by an absolute sum". Appropriate for money, which is
    /// generated multiplicatively and is approximately log-normal.
    /// </summary>
    Log10 = 1
}

/// <summary>Statistic used to judge how far an amount sits from its peers.</summary>
public enum OutlierMethod
{
    /// <summary>Mean and sample standard deviation. Familiar, but the outlier inflates its own denominator.</summary>
    ClassicZScore = 0,

    /// <summary>Median and median absolute deviation. Robust to the values being tested.</summary>
    ModifiedZScore = 1
}

/// <summary>The peer group a transaction's amount is compared against.</summary>
public enum OutlierGrouping
{
    Vendor = 0,
    Category = 1,
    Department = 2
}

/// <summary>The competitive arena within which vendor share is measured.</summary>
public enum ConcentrationScope
{
    Category = 0,
    Department = 1
}
