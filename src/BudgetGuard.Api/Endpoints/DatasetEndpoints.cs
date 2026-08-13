using BudgetGuard.Application.Datasets.Commands.LoadDemoDataset;
using BudgetGuard.Application.Datasets.Commands.UploadDataset;
using BudgetGuard.Application.Datasets.Queries.GetDatasets;
using MediatR;

namespace BudgetGuard.Api.Endpoints;

/// <summary>
/// Dataset ingest endpoints.
/// <para>
/// Every endpoint does exactly three things: bind the request, send a MediatR
/// message, return the result. There is no business logic here by design — the
/// same commands run unchanged from Blazor, so the two front ends cannot drift
/// into behaving differently. See docs/CODING_STANDARDS.md.
/// </para>
/// </summary>
public static class DatasetEndpoints
{
    public static IEndpointRouteBuilder MapDatasetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/datasets").WithTags("Datasets");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetDatasetsQuery(), ct)))
            .WithName("ListDatasets")
            .WithSummary("Lists stored datasets, most recently uploaded first.")
            .Produces<IReadOnlyList<DatasetSummaryDto>>();

        group.MapPost("/", async (
                    IFormFile file,
                    string? name,
                    ISender sender,
                    CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();

                var result = await sender.Send(
                    new UploadDatasetCommand(stream, file.FileName, name), ct);

                return Results.Created($"/api/datasets/{result.DatasetId}", result);
            })
            .WithName("UploadDataset")
            .WithSummary("Uploads a CSV or Excel procurement dataset.")
            .WithDescription(
                "Required columns: TransactionDate, Amount, VendorName, Category, Department. " +
                "Optional: ExternalReference, Currency, Description. Common alternative column " +
                "names are accepted; see docs/DATA_MODEL.md.")
            .DisableAntiforgery()
            .Produces<UploadDatasetResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPost("/demo", async (
                    bool? regenerate,
                    ISender sender,
                    CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new LoadDemoDatasetCommand(regenerate ?? false), ct)))
            .WithName("LoadDemoDataset")
            .WithSummary("Generates the built-in synthetic demo dataset.")
            .WithDescription(
                "The demo dataset is entirely fictional and contains deliberately planted " +
                "anomalies. It is not real government procurement data and must never be " +
                "presented as such.")
            .Produces<LoadDemoDatasetResult>();

        return app;
    }
}
