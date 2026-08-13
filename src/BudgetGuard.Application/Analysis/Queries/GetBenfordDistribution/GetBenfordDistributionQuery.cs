using BudgetGuard.Application.Analysis.Dtos;
using BudgetGuard.Application.Analysis.Services;
using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Benford;
using MediatR;
using Microsoft.Extensions.Options;

namespace BudgetGuard.Application.Analysis.Queries.GetBenfordDistribution;

/// <summary>
/// Returns chart-ready expected versus actual first-digit distribution for a dataset.
/// </summary>
/// <param name="DatasetId">Dataset to analyse. Null uses the most recent upload.</param>
/// <param name="VendorName">
/// Optional: restrict the test to one vendor's invoices. A dataset-wide failure
/// says something is wrong somewhere; testing a single supplier says where.
/// </param>
public sealed record GetBenfordDistributionQuery(
    Guid? DatasetId = null,
    string? VendorName = null) : IRequest<BenfordDistributionDto>;

/// <inheritdoc cref="GetBenfordDistributionQuery" />
public sealed class GetBenfordDistributionQueryHandler(
    IAnalysisService analysisService,
    IDatasetRepository repository,
    IBenfordAnalyzer benfordAnalyzer,
    IOptions<DetectionSettings> settings)
    : IRequestHandler<GetBenfordDistributionQuery, BenfordDistributionDto>
{
    public async Task<BenfordDistributionDto> Handle(
        GetBenfordDistributionQuery request,
        CancellationToken cancellationToken)
    {
        var analysis = await analysisService.AnalyzeAsync(request.DatasetId, cancellationToken);

        // The dataset-wide result is already computed as part of the run; only a
        // per-vendor request needs the analyser again.
        var result = string.IsNullOrWhiteSpace(request.VendorName)
            ? analysis.Report.DatasetBenford
            : await AnalyzeVendorAsync(analysis, request.VendorName, cancellationToken);

        return new BenfordDistributionDto(
            analysis.Dataset.Id,
            analysis.Dataset.Name,
            analysis.Dataset.IsSyntheticDemo,
            result.PopulationLabel,
            result.SampleSize,
            result.ExcludedCount,
            result.MeanAbsoluteDeviation,
            result.EffectiveMadThreshold,
            settings.Value.Benford.MadNonConformityThreshold,
            result.ChiSquare,
            result.ChiSquareCriticalValue,
            result.Conformity.ToString(),
            result.IsAnomalous,
            result.Explanation,
            result.Digits
                .Select(d => new BenfordDigitDto(
                    d.Digit,
                    d.ObservedCount,
                    d.ObservedProportion,
                    d.ExpectedCount,
                    d.ExpectedProportion,
                    d.DeviationPercentagePoints,
                    d.ExcessCount))
                .ToArray());
    }

    /// <summary>
    /// Runs the first-digit test over one vendor's individual invoice amounts.
    /// <para>
    /// Deliberately the raw transaction amounts, not any per-scope totals:
    /// Benford's Law describes the digits of the individual values a process
    /// produces, and aggregating them first would destroy exactly the signal
    /// being tested.
    /// </para>
    /// </summary>
    private async Task<BenfordResult> AnalyzeVendorAsync(
        DatasetAnalysis analysis,
        string vendorName,
        CancellationToken cancellationToken)
    {
        var transactions = await repository.GetTransactionsAsync(analysis.Dataset.Id, cancellationToken);

        var amounts = transactions
            .Where(t => string.Equals(t.VendorName.Trim(), vendorName, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Amount)
            .ToArray();

        return benfordAnalyzer.Analyze(amounts, $"vendor \"{vendorName}\"");
    }
}
