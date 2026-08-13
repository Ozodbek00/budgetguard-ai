using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Detection.Concentration;

/// <summary>Measures how spend is distributed across suppliers within each competitive scope.</summary>
public interface IVendorConcentrationAnalyzer
{
    /// <summary>Analyses supplier concentration across every configured scope.</summary>
    VendorConcentrationResult Analyze(IReadOnlyList<ProcurementTransaction> transactions);
}

/// <summary>
/// Supplier concentration analysis.
/// <para>
/// <b>What it looks for.</b> Procurement fraud rarely shows up in one payment;
/// it shows up as a pattern of awards. A vendor that repeatedly wins an
/// outsized share of one category's budget is the classic signature of bid
/// rigging, a tailored tender specification, or an undisclosed relationship
/// between a supplier and the officials awarding the work.
/// </para>
/// <para>
/// <b>Two thresholds, deliberately.</b> An absolute share bound (default 30%)
/// catches obvious dominance. But 30% means very different things in a scope
/// with 3 vendors and a scope with 50. So a scale-aware test runs alongside it:
/// the even-split expectation is 1/N, and a vendor taking more than a
/// configured multiple of that (default 3x) is flagged even if their absolute
/// share is modest. Either test firing raises the flag.
/// </para>
/// <para>
/// <b>Scope-level concentration.</b> Separately from any single vendor, each
/// scope gets a Herfindahl-Hirschman Index — the sum of squared percentage
/// market shares, from near 0 (perfect competition) to 10,000 (monopoly).
/// Above 2,500 competition authorities classify a market as highly
/// concentrated. A category that scores badly here is worth reviewing as a
/// procurement process even when no individual vendor crosses a threshold.
/// </para>
/// <para>
/// <b>Known false-positive risk.</b> Legitimate concentration is common: some
/// categories genuinely have one national supplier, and framework agreements
/// deliberately consolidate spend. This detector produces leads, not
/// conclusions, which is why every finding states the arithmetic instead of
/// just asserting risk.
/// </para>
/// </summary>
public sealed class VendorConcentrationAnalyzer : IVendorConcentrationAnalyzer
{
    private readonly VendorConcentrationSettings _settings;

    public VendorConcentrationAnalyzer(VendorConcentrationSettings settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <inheritdoc />
    public VendorConcentrationResult Analyze(IReadOnlyList<ProcurementTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var findings = new List<VendorConcentrationFinding>();
        var summaries = new List<ScopeConcentrationSummary>();

        foreach (var scope in _settings.Scopes.Distinct())
        {
            foreach (var scopeGroup in transactions
                         .Where(t => t.Amount > 0m)
                         .GroupBy(t => KeyFor(t, scope), StringComparer.OrdinalIgnoreCase))
            {
                var members = scopeGroup.ToArray();
                var totalSpend = members.Sum(t => t.Amount);

                var vendorGroups = members
                    .GroupBy(t => t.VendorName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                // A scope with two suppliers and six payments cannot support a
                // concentration claim — "one vendor has 60%" is meaningless there.
                if (vendorGroups.Length < _settings.MinimumVendorsInScope ||
                    members.Length < _settings.MinimumTransactionsInScope ||
                    totalSpend <= 0m)
                {
                    continue;
                }

                var vendorShares = vendorGroups
                    .Select(v =>
                    {
                        var spend = v.Sum(t => t.Amount);
                        return new VendorShare(
                            v.Key,
                            spend,
                            v.Count(),
                            (double)(spend / totalSpend),
                            (double)v.Count() / members.Length);
                    })
                    .OrderByDescending(v => v.SpendShare)
                    .ToArray();

                var expectedShare = 1d / vendorShares.Length;

                // HHI convention: shares expressed as percentages, then squared,
                // giving a 0-10,000 scale comparable with published guidelines.
                var hhi = vendorShares.Sum(v => Math.Pow(v.SpendShare * 100d, 2));

                summaries.Add(new ScopeConcentrationSummary(
                    scope,
                    scopeGroup.Key,
                    vendorShares.Length,
                    totalSpend,
                    members.Length,
                    hhi,
                    expectedShare,
                    hhi > _settings.HighlyConcentratedHhi,
                    vendorShares,
                    BuildScopeExplanation(scope, scopeGroup.Key, vendorShares.Length, hhi)));

                // Under an even award process, contracts land on a vendor as
                // Binomial(T, 1/N). These are that distribution's moments.
                var expectedContractCount = members.Length * expectedShare;
                var contractCountStdDev =
                    Math.Sqrt(members.Length * expectedShare * (1d - expectedShare));

                foreach (var vendor in vendorShares)
                {
                    var excessMultiple = vendor.SpendShare / expectedShare;

                    var breachesAbsolute = vendor.SpendShare > _settings.SpendShareThreshold;
                    var breachesRelative = excessMultiple > _settings.ExpectedShareMultiple;

                    if (!breachesAbsolute && !breachesRelative)
                    {
                        continue;
                    }

                    var contractCountZ = contractCountStdDev > 0d
                        ? (vendor.ContractCount - expectedContractCount) / contractCountStdDev
                        : 0d;

                    // Winning a lot of money is not the same as winning a lot of
                    // work. Require both before calling it concentration.
                    if (contractCountZ <= _settings.ContractCountZThreshold ||
                        vendor.ContractCount < _settings.MinimumContractsForConcentration)
                    {
                        continue;
                    }

                    findings.Add(new VendorConcentrationFinding(
                        vendor.VendorName,
                        scope,
                        scopeGroup.Key,
                        vendor.Spend,
                        totalSpend,
                        vendor.SpendShare,
                        expectedShare,
                        excessMultiple,
                        vendor.ContractCount,
                        members.Length,
                        vendor.CountShare,
                        vendorShares.Length,
                        expectedContractCount,
                        contractCountZ,
                        NormaliseScore(vendor.SpendShare, excessMultiple),
                        BuildVendorExplanation(
                            vendor, scope, scopeGroup.Key, totalSpend,
                            members.Length, vendorShares.Length, expectedShare, excessMultiple,
                            expectedContractCount, contractCountZ)));
                }
            }
        }

        return new VendorConcentrationResult(
            findings.OrderByDescending(f => f.NormalisedScore).ToArray(),
            summaries.OrderByDescending(s => s.HerfindahlHirschmanIndex).ToArray());
    }

    /// <summary>
    /// Maps a concentration onto [0,1] so it can be ranked against outlier and
    /// Benford signals. Each of the two flag conditions is scaled so that
    /// sitting exactly on its threshold scores 0.5 and doubling it saturates at
    /// 1.0; the stronger of the two wins, so a vendor is never under-rated
    /// because it tripped the other test.
    /// </summary>
    private double NormaliseScore(double spendShare, double excessMultiple)
    {
        var byAbsoluteShare = spendShare / (2d * _settings.SpendShareThreshold);

        var byRelativeMultiple = _settings.ExpectedShareMultiple > 1d
            ? (excessMultiple - 1d) / (2d * (_settings.ExpectedShareMultiple - 1d))
            : excessMultiple / 2d;

        return Math.Clamp(Math.Max(byAbsoluteShare, byRelativeMultiple), 0d, 1d);
    }

    private static string KeyFor(ProcurementTransaction transaction, ConcentrationScope scope) =>
        scope switch
        {
            ConcentrationScope.Category => transaction.Category.Trim(),
            ConcentrationScope.Department => transaction.Department.Trim(),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown scope.")
        };

    private static string ScopeLabel(ConcentrationScope scope) =>
        scope == ConcentrationScope.Category ? "category" : "department";

    private string BuildVendorExplanation(
        VendorShare vendor,
        ConcentrationScope scope,
        string scopeKey,
        decimal totalSpend,
        int transactionCount,
        int vendorCount,
        double expectedShare,
        double excessMultiple,
        double expectedContractCount,
        double contractCountZ)
    {
        return
            $"\"{vendor.VendorName}\" received {DetectionFormat.Percent(vendor.SpendShare)} of all spend in " +
            $"{ScopeLabel(scope)} \"{scopeKey}\" ({vendor.Spend:N0} of {totalSpend:N0}), " +
            $"versus an expected {DetectionFormat.Percent(expectedShare)} if the {vendorCount:N0} vendors " +
            $"competing in that {ScopeLabel(scope)} shared it evenly — {excessMultiple:F1}x the even-split " +
            $"expectation. This is not a single large award: they hold {vendor.ContractCount:N0} of " +
            $"{transactionCount:N0} contracts ({DetectionFormat.Percent(vendor.CountShare)}) where an even " +
            $"award process would give them about {expectedContractCount:N1}, an excess of " +
            $"{contractCountZ:F1} standard deviations. The flag thresholds are " +
            $"{DetectionFormat.Percent(_settings.SpendShareThreshold, 0)} of spend or " +
            $"{_settings.ExpectedShareMultiple:F1}x the even-split expectation, together with a contract " +
            $"count at least {_settings.ContractCountZThreshold:F1} standard deviations above chance.";
    }

    private string BuildScopeExplanation(
        ConcentrationScope scope,
        string scopeKey,
        int vendorCount,
        double hhi)
    {
        var evenSplitHhi = 10_000d / vendorCount;

        var verdict = hhi > _settings.HighlyConcentratedHhi
            ? $"Above {_settings.HighlyConcentratedHhi:N0} is classed as a highly concentrated " +
              "market under the US DOJ/FTC Horizontal Merger Guidelines"
            : $"This is below the {_settings.HighlyConcentratedHhi:N0} threshold at which a market " +
              "is classed as highly concentrated";

        return
            $"{char.ToUpperInvariant(ScopeLabel(scope)[0])}{ScopeLabel(scope)[1..]} \"{scopeKey}\" " +
            $"has a Herfindahl-Hirschman Index of {hhi:N0} across {vendorCount:N0} vendors. " +
            $"{verdict}; an even split among {vendorCount:N0} vendors would score {evenSplitHhi:N0}.";
    }
}
