using BudgetGuard.Application.Analysis.Dtos;
using BudgetGuard.Application.Analysis.Services;
using BudgetGuard.Domain.Detection;
using MediatR;

namespace BudgetGuard.Application.Analysis.Queries.GetAnomalyReport;

/// <summary>
/// Runs the detection pipeline over a dataset and returns the ranked anomaly report.
/// </summary>
/// <param name="DatasetId">Dataset to analyse. Null uses the most recent upload.</param>
/// <param name="Category">Optional category filter.</param>
/// <param name="Department">Optional department filter.</param>
/// <param name="MinimumSeverity">Optional lower bound on severity.</param>
public sealed record GetAnomalyReportQuery(
    Guid? DatasetId = null,
    string? Category = null,
    string? Department = null,
    Severity? MinimumSeverity = null) : IRequest<AnomalyReportDto>;

/// <summary>
/// Maps the domain report onto transport DTOs and applies the requested filters.
/// <para>
/// Filtering happens after detection, never before. Excluding a category from
/// the population first would change every peer group, every vendor share and
/// the Benford baseline, so the numbers behind a filtered view would no longer
/// match the numbers behind the unfiltered one. The auditor filters what they
/// are looking at, not what the engine analysed.
/// </para>
/// </summary>
public sealed class GetAnomalyReportQueryHandler(IAnalysisService analysisService)
    : IRequestHandler<GetAnomalyReportQuery, AnomalyReportDto>
{
    public async Task<AnomalyReportDto> Handle(
        GetAnomalyReportQuery request,
        CancellationToken cancellationToken)
    {
        var analysis = await analysisService.AnalyzeAsync(request.DatasetId, cancellationToken);
        var report = analysis.Report;

        var findings = report.Findings.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            findings = findings.Where(f =>
                string.Equals(f.Category, request.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Department))
        {
            findings = findings.Where(f =>
                string.Equals(f.Department, request.Department, StringComparison.OrdinalIgnoreCase));
        }

        if (request.MinimumSeverity is { } minimum)
        {
            findings = findings.Where(f => f.Severity >= minimum);
        }

        var filtered = findings.Select(ToDto).ToArray();

        // Summary counts describe the whole report, not the filtered view, so
        // the header does not change meaning as the auditor narrows the table.
        var summary = new AnomalySummaryDto(
            report.CountAt(Severity.Critical),
            report.CountAt(Severity.High),
            report.CountAt(Severity.Medium),
            report.CountAt(Severity.Low),
            report.CorroboratedCount);

        return new AnomalyReportDto(
            analysis.Dataset.Id,
            analysis.Dataset.Name,
            analysis.Dataset.IsSyntheticDemo,
            report.TransactionCount,
            analysis.GeneratedAtUtc,
            summary,
            filtered,
            DistinctValues(report, f => f.Category),
            DistinctValues(report, f => f.Department));
    }

    private static IReadOnlyList<string> DistinctValues(
        AnomalyReport report,
        Func<AnomalyFinding, string?> selector) =>
        report.Findings
            .Select(selector)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static AnomalyFindingDto ToDto(AnomalyFinding finding) =>
        new(finding.SubjectType.ToString(),
            finding.SubjectKey,
            finding.SubjectLabel,
            finding.RiskScore,
            finding.Severity.ToString(),
            finding.Category,
            finding.Department,
            finding.Amount,
            finding.TransactionDate,
            finding.CorroboratingDetectors.Select(d => d.ToString()).ToArray(),
            finding.Signals
                .Select(s => new AnomalySignalDto(
                    s.Detector.ToString(),
                    s.Score,
                    s.Explanation,
                    s.Evidence))
                .ToArray());
}
