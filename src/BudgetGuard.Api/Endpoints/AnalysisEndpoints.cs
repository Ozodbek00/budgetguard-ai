using BudgetGuard.Application.Analysis.Dtos;
using BudgetGuard.Application.Analysis.Queries.GetAnomalyReport;
using BudgetGuard.Application.Analysis.Queries.GetBenfordDistribution;
using BudgetGuard.Application.Analysis.Queries.GetVendorRisk;
using BudgetGuard.Domain.Detection;
using MediatR;

namespace BudgetGuard.Api.Endpoints;

/// <summary>Detection and reporting endpoints.</summary>
public static class AnalysisEndpoints
{
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analysis").WithTags("Analysis");

        group.MapGet("/report", async (
                    Guid? datasetId,
                    string? category,
                    string? department,
                    Severity? minimumSeverity,
                    ISender sender,
                    CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new GetAnomalyReportQuery(datasetId, category, department, minimumSeverity), ct)))
            .WithName("GetAnomalyReport")
            .WithSummary("Ranked anomaly report for a dataset.")
            .WithDescription(
                "Runs Benford, peer-relative outlier and vendor concentration analysis, then " +
                "merges the signals into one ranked list. Each finding carries the explanations " +
                "behind it. Filters narrow the returned rows only — they never change the " +
                "population the statistics were computed over. Omit datasetId to use the most " +
                "recent upload.")
            .Produces<AnomalyReportDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/benford", async (
                    Guid? datasetId,
                    string? vendorName,
                    ISender sender,
                    CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new GetBenfordDistributionQuery(datasetId, vendorName), ct)))
            .WithName("GetBenfordDistribution")
            .WithSummary("Expected versus actual first-digit distribution.")
            .WithDescription(
                "Chart-ready per-digit data plus the conformity verdict. Supply vendorName to " +
                "test one supplier's invoices rather than the whole dataset.")
            .Produces<BenfordDistributionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/vendor-risk", async (
                    Guid? datasetId,
                    string? scopeType,
                    bool? flaggedOnly,
                    ISender sender,
                    CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new GetVendorRiskQuery(datasetId, scopeType, flaggedOnly ?? false), ct)))
            .WithName("GetVendorRisk")
            .WithSummary("Supplier concentration per category and department.")
            .WithDescription(
                "Returns every vendor's share of each scope, not only flagged ones, plus the " +
                "Herfindahl-Hirschman Index for each scope. scopeType accepts Category or Department.")
            .Produces<VendorRiskDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
