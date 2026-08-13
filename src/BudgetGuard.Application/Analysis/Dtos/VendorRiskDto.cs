namespace BudgetGuard.Application.Analysis.Dtos;

/// <summary>One vendor's position within one competitive scope. A sortable table row.</summary>
/// <param name="VendorName">Supplier.</param>
/// <param name="ScopeType">Category or Department.</param>
/// <param name="ScopeKey">The category or department name.</param>
/// <param name="Spend">Awarded to this vendor within the scope.</param>
/// <param name="SpendShare">Share of scope spend, 0-1.</param>
/// <param name="ExpectedShare">Even-split benchmark, 1/N vendors.</param>
/// <param name="ExcessMultiple">SpendShare divided by ExpectedShare.</param>
/// <param name="ContractCount">Contracts held in the scope.</param>
/// <param name="ContractShare">Share of scope contract count, 0-1.</param>
/// <param name="ExpectedContractCount">Contracts an even award process would give them.</param>
/// <param name="ContractCountZScore">Standard deviations above chance, or null when not flagged.</param>
/// <param name="IsFlagged">True when this vendor breached the concentration thresholds.</param>
/// <param name="Explanation">The reason, when flagged.</param>
public sealed record VendorRiskRowDto(
    string VendorName,
    string ScopeType,
    string ScopeKey,
    decimal Spend,
    double SpendShare,
    double ExpectedShare,
    double ExcessMultiple,
    int ContractCount,
    double ContractShare,
    double ExpectedContractCount,
    double? ContractCountZScore,
    bool IsFlagged,
    string? Explanation);

/// <summary>Competitive health of one category or department.</summary>
/// <param name="ScopeType">Category or Department.</param>
/// <param name="ScopeKey">Its name.</param>
/// <param name="VendorCount">Distinct vendors competing.</param>
/// <param name="TransactionCount">Contracts in the scope.</param>
/// <param name="TotalSpend">Total spend in the scope.</param>
/// <param name="HerfindahlHirschmanIndex">Sum of squared percentage shares, 0-10,000.</param>
/// <param name="EvenSplitHhi">What a perfectly even split among these vendors would score.</param>
/// <param name="IsHighlyConcentrated">True when the index exceeds the configured bound.</param>
/// <param name="TopVendorName">Largest supplier in the scope.</param>
/// <param name="TopVendorShare">That supplier's share of scope spend.</param>
/// <param name="Explanation">Plain-language read on the scope's competitiveness.</param>
public sealed record ScopeRiskRowDto(
    string ScopeType,
    string ScopeKey,
    int VendorCount,
    int TransactionCount,
    decimal TotalSpend,
    double HerfindahlHirschmanIndex,
    double EvenSplitHhi,
    bool IsHighlyConcentrated,
    string TopVendorName,
    double TopVendorShare,
    string Explanation);

/// <summary>Everything the vendor risk view renders.</summary>
/// <param name="DatasetId">Dataset analysed.</param>
/// <param name="DatasetName">Its display name.</param>
/// <param name="IsSyntheticDemo">True when this is generated demo data.</param>
/// <param name="Scopes">Scope competitiveness rows, most concentrated first.</param>
/// <param name="Vendors">Vendor rows across all scopes, flagged ones first.</param>
/// <param name="ScopeTypes">Distinct scope types present, for filtering.</param>
public sealed record VendorRiskDto(
    Guid DatasetId,
    string DatasetName,
    bool IsSyntheticDemo,
    IReadOnlyList<ScopeRiskRowDto> Scopes,
    IReadOnlyList<VendorRiskRowDto> Vendors,
    IReadOnlyList<string> ScopeTypes);
