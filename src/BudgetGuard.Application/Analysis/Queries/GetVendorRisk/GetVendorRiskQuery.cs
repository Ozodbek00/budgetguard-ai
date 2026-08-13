using BudgetGuard.Application.Analysis.Dtos;
using BudgetGuard.Application.Analysis.Services;
using BudgetGuard.Domain.Detection.Concentration;
using MediatR;

namespace BudgetGuard.Application.Analysis.Queries.GetVendorRisk;

/// <summary>Returns the supplier concentration view for a dataset.</summary>
/// <param name="DatasetId">Dataset to analyse. Null uses the most recent upload.</param>
/// <param name="ScopeType">Optional filter: "Category" or "Department".</param>
/// <param name="FlaggedOnly">When true, returns only vendors that breached a threshold.</param>
public sealed record GetVendorRiskQuery(
    Guid? DatasetId = null,
    string? ScopeType = null,
    bool FlaggedOnly = false) : IRequest<VendorRiskDto>;

/// <summary>
/// Builds the vendor risk table.
/// <para>
/// Returns every vendor in every analysed scope, not only the flagged ones. A
/// concentration figure is only interpretable against the field it sits in —
/// "34% of the category" means one thing among 4 suppliers and another among
/// 40 — so the auditor needs to see the whole distribution to judge a flag,
/// and needs to be able to sort a competitive-looking scope for themselves.
/// </para>
/// </summary>
public sealed class GetVendorRiskQueryHandler(IAnalysisService analysisService)
    : IRequestHandler<GetVendorRiskQuery, VendorRiskDto>
{
    public async Task<VendorRiskDto> Handle(
        GetVendorRiskQuery request,
        CancellationToken cancellationToken)
    {
        var analysis = await analysisService.AnalyzeAsync(request.DatasetId, cancellationToken);
        var concentration = analysis.Report.Concentration;

        // Index the flagged findings so each vendor row can carry its reason.
        var flagged = concentration.Findings.ToDictionary(
            f => (f.Scope, f.ScopeKey, f.VendorName),
            f => f);

        var scopes = concentration.Scopes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.ScopeType))
        {
            scopes = scopes.Where(s =>
                string.Equals(s.Scope.ToString(), request.ScopeType, StringComparison.OrdinalIgnoreCase));
        }

        var scopeList = scopes.ToArray();

        var vendorRows = scopeList
            .SelectMany(scope => scope.Vendors.Select(vendor =>
            {
                flagged.TryGetValue((scope.Scope, scope.ScopeKey, vendor.VendorName), out var finding);

                return new VendorRiskRowDto(
                    vendor.VendorName,
                    scope.Scope.ToString(),
                    scope.ScopeKey,
                    vendor.Spend,
                    vendor.SpendShare,
                    scope.ExpectedShare,
                    vendor.SpendShare / scope.ExpectedShare,
                    vendor.ContractCount,
                    vendor.CountShare,
                    scope.TransactionCount * scope.ExpectedShare,
                    finding?.ContractCountZScore,
                    finding is not null,
                    finding?.Explanation);
            }))
            .Where(row => !request.FlaggedOnly || row.IsFlagged)
            .OrderByDescending(row => row.IsFlagged)
            .ThenByDescending(row => row.SpendShare)
            .ToArray();

        var scopeRows = scopeList
            .Select(ToScopeRow)
            .OrderByDescending(s => s.HerfindahlHirschmanIndex)
            .ToArray();

        return new VendorRiskDto(
            analysis.Dataset.Id,
            analysis.Dataset.Name,
            analysis.Dataset.IsSyntheticDemo,
            scopeRows,
            vendorRows,
            concentration.Scopes
                .Select(s => s.Scope.ToString())
                .Distinct()
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray());
    }

    private static ScopeRiskRowDto ToScopeRow(ScopeConcentrationSummary scope)
    {
        var top = scope.Vendors.Count > 0 ? scope.Vendors[0] : null;

        return new ScopeRiskRowDto(
            scope.Scope.ToString(),
            scope.ScopeKey,
            scope.VendorCount,
            scope.TransactionCount,
            scope.TotalSpend,
            scope.HerfindahlHirschmanIndex,
            scope.VendorCount == 0 ? 0d : 10_000d / scope.VendorCount,
            scope.IsHighlyConcentrated,
            top?.VendorName ?? string.Empty,
            top?.SpendShare ?? 0d,
            scope.Explanation);
    }
}
