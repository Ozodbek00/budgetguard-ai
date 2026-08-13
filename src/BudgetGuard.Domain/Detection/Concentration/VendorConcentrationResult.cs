namespace BudgetGuard.Domain.Detection.Concentration;

/// <summary>One vendor's slice of one competitive scope. The row behind the vendor risk table.</summary>
/// <param name="VendorName">Supplier.</param>
/// <param name="Spend">Total awarded to this vendor within the scope.</param>
/// <param name="ContractCount">Transactions awarded to this vendor within the scope.</param>
/// <param name="SpendShare">Share of scope spend, 0-1.</param>
/// <param name="CountShare">Share of scope transaction count, 0-1.</param>
public sealed record VendorShare(
    string VendorName,
    decimal Spend,
    int ContractCount,
    double SpendShare,
    double CountShare);

/// <summary>
/// Competitive health of a single scope (one category or one department),
/// independent of any individual vendor.
/// </summary>
/// <param name="Scope">Category or department.</param>
/// <param name="ScopeKey">The category or department name.</param>
/// <param name="VendorCount">Distinct vendors competing in the scope.</param>
/// <param name="TotalSpend">Total spend in the scope.</param>
/// <param name="TransactionCount">Transactions in the scope.</param>
/// <param name="HerfindahlHirschmanIndex">Sum of squared percentage shares, 0-10,000.</param>
/// <param name="ExpectedShare">1 / <paramref name="VendorCount"/> — the even-split benchmark.</param>
/// <param name="IsHighlyConcentrated">True when HHI exceeds the configured bound.</param>
/// <param name="Vendors">Every vendor in the scope, largest share first.</param>
/// <param name="Explanation">Plain-language summary of the scope's competitiveness.</param>
public sealed record ScopeConcentrationSummary(
    ConcentrationScope Scope,
    string ScopeKey,
    int VendorCount,
    decimal TotalSpend,
    int TransactionCount,
    double HerfindahlHirschmanIndex,
    double ExpectedShare,
    bool IsHighlyConcentrated,
    IReadOnlyList<VendorShare> Vendors,
    string Explanation);

/// <summary>A vendor flagged for taking an implausible share of a scope.</summary>
/// <param name="VendorName">Supplier.</param>
/// <param name="Scope">Category or department.</param>
/// <param name="ScopeKey">The category or department name.</param>
/// <param name="Spend">Spend awarded to this vendor in the scope.</param>
/// <param name="ScopeTotalSpend">Total scope spend.</param>
/// <param name="SpendShare">Share of scope spend, 0-1.</param>
/// <param name="ExpectedShare">Even-split benchmark, 1/N vendors.</param>
/// <param name="ExcessMultiple">SpendShare divided by ExpectedShare.</param>
/// <param name="ContractCount">Transactions awarded to this vendor in scope.</param>
/// <param name="ScopeTransactionCount">Total transactions in scope.</param>
/// <param name="CountShare">Share of scope transaction count, 0-1.</param>
/// <param name="VendorsInScope">Distinct vendors competing in the scope.</param>
/// <param name="ExpectedContractCount">Contracts random allocation would give this vendor, T/N.</param>
/// <param name="ContractCountZScore">
/// How many standard deviations the vendor's contract count sits above random
/// allocation. Distinguishes market capture from a single very large award.
/// </param>
/// <param name="NormalisedScore">Strength in [0,1], comparable with other detectors.</param>
/// <param name="Explanation">Plain-language, arithmetic-included reason.</param>
public sealed record VendorConcentrationFinding(
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
    double NormalisedScore,
    string Explanation);

/// <summary>Everything the concentration analyser produced for one dataset.</summary>
/// <param name="Findings">Flagged vendor/scope pairs, strongest first.</param>
/// <param name="Scopes">Every analysed scope, including competitive ones, for the vendor risk view.</param>
public sealed record VendorConcentrationResult(
    IReadOnlyList<VendorConcentrationFinding> Findings,
    IReadOnlyList<ScopeConcentrationSummary> Scopes);
