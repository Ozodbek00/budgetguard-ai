using BudgetGuard.Domain.Detection.Explanations;
using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Detection.Outliers;

/// <summary>Finds transactions whose amounts are extreme relative to their peer group.</summary>
public interface IZScoreOutlierDetector
{
    /// <summary>Scans every configured peer grouping and returns the flagged transactions.</summary>
    IReadOnlyList<OutlierFinding> Detect(IReadOnlyList<ProcurementTransaction> transactions);
}

/// <summary>
/// Peer-relative amount outlier detection.
/// <para>
/// <b>Why grouped, not global.</b> A 900 million UZS road contract is not
/// suspicious; a 900 million UZS stationery order is. Comparing every payment
/// against one dataset-wide mean would flag all large-ticket categories and
/// miss the anomalies inside small ones. So amounts are only ever compared
/// against their own peer group — same vendor, same category, or same
/// department — which is the comparison an auditor would make by hand.
/// </para>
/// <para>
/// <b>The masking problem.</b> The classic z-score divides by the standard
/// deviation, but a large outlier inflates that same standard deviation, which
/// shrinks its own z-score and can hide it. Worse, a pair of outliers can mask
/// each other entirely. The modified z-score
/// (0.6745 x (x - median) / MAD, Iglewicz and Hoaglin) uses the median and
/// median absolute deviation, neither of which is moved much by the values
/// being judged. Both are implemented; the method is a setting, and the
/// trade-off is documented in docs/DETECTION_METHODOLOGY.md.
/// </para>
/// <para>
/// <b>Why 3.0.</b> Under a normal distribution roughly 0.27% of observations
/// lie beyond three standard deviations, so on a 10,000-row dataset a threshold
/// of 3.0 yields about 27 flags from chance alone — a reviewable number.
/// Procurement amounts are right-skewed rather than normal, so this is a
/// screening heuristic for human review, not a probability claim.
/// </para>
/// </summary>
public sealed class ZScoreOutlierDetector : IZScoreOutlierDetector
{
    /// <summary>
    /// Consistency constant making the MAD an unbiased estimator of the
    /// standard deviation for normally distributed data: 0.6745 is the
    /// 0.75 quantile of the standard normal.
    /// </summary>
    private const double MadConsistencyConstant = 0.6745;

    /// <summary>
    /// Fallback constant used when every deviation from the median is zero
    /// (a heavily tied group). Scales the mean absolute deviation onto the
    /// same footing as the MAD.
    /// </summary>
    private const double MeanAbsoluteDeviationConstant = 1.253314;

    private readonly ZScoreSettings _settings;
    private readonly IExplanationWriter _writer;

    /// <param name="settings">Thresholds. See <see cref="ZScoreSettings"/>.</param>
    /// <param name="writer">Language the finding sentence is written in. Defaults to English.</param>
    public ZScoreOutlierDetector(ZScoreSettings settings, IExplanationWriter? writer = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _writer = writer ?? new EnglishExplanationWriter();
    }

    /// <inheritdoc />
    public IReadOnlyList<OutlierFinding> Detect(IReadOnlyList<ProcurementTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var findings = new List<OutlierFinding>();

        foreach (var grouping in _settings.Groupings.Distinct())
        {
            foreach (var group in transactions
                         .GroupBy(t => KeyFor(t, grouping), StringComparer.OrdinalIgnoreCase))
            {
                var members = group.ToArray();

                // Too small a peer group cannot support a defensible flag: with
                // a handful of payments the mean and spread are dominated by
                // whichever value we are trying to judge.
                if (members.Length < _settings.MinimumGroupSize)
                {
                    continue;
                }

                // Under a log comparison, zero and negative amounts have no
                // logarithm. They are dropped from the peer group rather than
                // being coerced, and re-checked against the size guard.
                var comparable = _settings.Transform == AmountTransform.Log10
                    ? members.Where(m => m.Amount > 0m).ToArray()
                    : members;

                if (comparable.Length < _settings.MinimumGroupSize)
                {
                    continue;
                }

                findings.AddRange(_settings.Method == OutlierMethod.ModifiedZScore
                    ? DetectModified(comparable, grouping, group.Key)
                    : DetectClassic(comparable, grouping, group.Key));
            }
        }

        return findings
            .OrderByDescending(f => Math.Abs(f.Score))
            .ToArray();
    }

    /// <summary>
    /// Projects an amount onto the comparison scale. Under
    /// <see cref="AmountTransform.Log10"/> a "distance from the mean" becomes a
    /// ratio rather than a difference, which is the meaningful notion of
    /// distance for money.
    /// </summary>
    private double Project(decimal amount) =>
        _settings.Transform == AmountTransform.Log10
            ? Math.Log10((double)amount)
            : (double)amount;

    /// <summary>
    /// Brings a statistic computed on the comparison scale back into currency
    /// so the explanation quotes numbers an auditor recognises. Under a log
    /// comparison the back-transformed centre is the geometric mean.
    /// </summary>
    private decimal Restore(double projected) =>
        _settings.Transform == AmountTransform.Log10
            ? (decimal)Math.Pow(10d, projected)
            : (decimal)projected;

    private static string KeyFor(ProcurementTransaction transaction, OutlierGrouping grouping) =>
        grouping switch
        {
            OutlierGrouping.Vendor => transaction.VendorName.Trim(),
            OutlierGrouping.Category => transaction.Category.Trim(),
            OutlierGrouping.Department => transaction.Department.Trim(),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, "Unknown grouping.")
        };

    private IEnumerable<OutlierFinding> DetectClassic(
        ProcurementTransaction[] members,
        OutlierGrouping grouping,
        string groupKey)
    {
        var amounts = members.Select(m => Project(m.Amount)).ToArray();
        var mean = amounts.Average();

        // Sample standard deviation (n-1). The group is a sample of that
        // vendor's or category's spending behaviour, not the whole population.
        var variance = amounts.Sum(a => (a - mean) * (a - mean)) / (amounts.Length - 1);
        var stdDev = Math.Sqrt(variance);

        if (stdDev <= 0d || double.IsNaN(stdDev))
        {
            // Every amount identical: no dispersion, so nothing can be an
            // outlier. (Identical repeated amounts are their own red flag, but
            // that is a duplicate-payment test, not this one.)
            yield break;
        }

        foreach (var member in members)
        {
            var z = (Project(member.Amount) - mean) / stdDev;

            if (Math.Abs(z) <= _settings.Threshold)
            {
                continue;
            }

            yield return BuildFinding(
                member,
                grouping,
                groupKey,
                members.Length,
                OutlierMethod.ClassicZScore,
                z,
                _settings.Threshold,
                Restore(mean),
                Restore(stdDev));
        }
    }

    private IEnumerable<OutlierFinding> DetectModified(
        ProcurementTransaction[] members,
        OutlierGrouping grouping,
        string groupKey)
    {
        var amounts = members.Select(m => Project(m.Amount)).ToArray();
        var median = Median(amounts);

        var absoluteDeviations = amounts.Select(a => Math.Abs(a - median)).ToArray();
        var mad = Median(absoluteDeviations);

        double scale;
        double denominator;

        if (mad > 0d)
        {
            scale = MadConsistencyConstant;
            denominator = mad;
        }
        else
        {
            // More than half the group shares one amount, so the MAD is zero and
            // the statistic would divide by zero. Fall back to the mean absolute
            // deviation, which is non-zero as long as any value differs.
            var meanAbsoluteDeviation = absoluteDeviations.Average();

            if (meanAbsoluteDeviation <= 0d)
            {
                yield break;
            }

            scale = 1d / MeanAbsoluteDeviationConstant;
            denominator = meanAbsoluteDeviation;
        }

        foreach (var member in members)
        {
            var m = scale * (Project(member.Amount) - median) / denominator;

            if (Math.Abs(m) <= _settings.ModifiedThreshold)
            {
                continue;
            }

            yield return BuildFinding(
                member,
                grouping,
                groupKey,
                members.Length,
                OutlierMethod.ModifiedZScore,
                m,
                _settings.ModifiedThreshold,
                Restore(median),
                Restore(mad));
        }
    }

    /// <summary>Median of a sample. Does not mutate the caller's array.</summary>
    internal static double Median(double[] values)
    {
        if (values.Length == 0)
        {
            return 0d;
        }

        var sorted = values.ToArray();
        Array.Sort(sorted);

        var mid = sorted.Length / 2;

        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2d;
    }

    private OutlierFinding BuildFinding(
        ProcurementTransaction transaction,
        OutlierGrouping grouping,
        string groupKey,
        int groupSize,
        OutlierMethod method,
        double score,
        double threshold,
        decimal centre,
        decimal dispersion)
    {
        // Normalisation: exactly at the threshold scores 0.5, twice the
        // threshold saturates at 1.0. Monotone in |score| and comparable with
        // the other detectors' [0,1] outputs.
        var normalised = Math.Clamp(Math.Abs(score) / (2d * threshold), 0d, 1d);

        var explanation = _writer.Outlier(new OutlierExplanationContext(
            transaction.ExternalReference,
            transaction.Amount,
            transaction.Currency,
            transaction.VendorName,
            grouping,
            groupKey,
            groupSize,
            method,
            _settings.Transform,
            score,
            threshold,
            centre,
            dispersion));


        return new OutlierFinding(
            transaction.Id,
            transaction.ExternalReference,
            transaction.VendorName,
            transaction.Category,
            transaction.Department,
            transaction.Amount,
            transaction.TransactionDate,
            grouping,
            groupKey,
            groupSize,
            method,
            _settings.Transform,
            score,
            threshold,
            centre,
            dispersion,
            normalised,
            explanation);
    }
}
