namespace BudgetGuard.Domain.Detection;

/// <summary>Which detector produced a signal.</summary>
public enum DetectorKind
{
    Benford = 0,
    ZScoreOutlier = 1,
    VendorConcentration = 2
}

/// <summary>What a finding is about.</summary>
public enum AnomalySubjectType
{
    /// <summary>An individual payment.</summary>
    Transaction = 0,

    /// <summary>A supplier, aggregated across their payments.</summary>
    Vendor = 1,

    /// <summary>The dataset as a whole, or a scope within it.</summary>
    Dataset = 2
}

/// <summary>Triage band derived from a risk score.</summary>
public enum Severity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// One detector's verdict about one subject.
/// <para>
/// A signal always carries its own <see cref="Explanation"/>. That is the
/// product requirement, not a nicety: an auditor must be able to read a flag,
/// understand the arithmetic behind it, and re-derive it by hand from the
/// source data. A score with no sentence attached is exactly the black box
/// this tool exists to replace.
/// </para>
/// </summary>
/// <param name="Detector">Which method fired.</param>
/// <param name="SubjectType">Whether this concerns a transaction, vendor, or the dataset.</param>
/// <param name="SubjectKey">Stable key of the subject — transaction id, vendor name, or scope label.</param>
/// <param name="SubjectLabel">Display label for the subject.</param>
/// <param name="Score">Normalised strength in [0,1]. Comparable across detectors.</param>
/// <param name="Explanation">Plain-language, numbers-included reason a human can verify.</param>
/// <param name="Evidence">Named raw statistics behind the score, for the drill-down view.</param>
public sealed record AnomalySignal(
    DetectorKind Detector,
    AnomalySubjectType SubjectType,
    string SubjectKey,
    string SubjectLabel,
    double Score,
    string Explanation,
    IReadOnlyDictionary<string, string> Evidence)
{
    /// <summary>Creates a signal, clamping the score into [0,1] so detectors cannot skew ranking.</summary>
    public static AnomalySignal Create(
        DetectorKind detector,
        AnomalySubjectType subjectType,
        string subjectKey,
        string subjectLabel,
        double score,
        string explanation,
        IReadOnlyDictionary<string, string>? evidence = null) =>
        new(detector,
            subjectType,
            subjectKey,
            subjectLabel,
            Math.Clamp(double.IsNaN(score) ? 0d : score, 0d, 1d),
            explanation,
            evidence ?? new Dictionary<string, string>());
}
