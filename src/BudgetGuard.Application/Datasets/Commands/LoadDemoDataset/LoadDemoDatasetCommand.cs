using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Domain.Demo;
using BudgetGuard.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BudgetGuard.Application.Datasets.Commands.LoadDemoDataset;

/// <summary>Result of loading the built-in demo dataset.</summary>
/// <param name="DatasetId">The demo dataset.</param>
/// <param name="Name">Its display name.</param>
/// <param name="RowCount">Transactions generated.</param>
/// <param name="AlreadyExisted">True when an existing demo dataset was reused.</param>
/// <param name="PlantedAnomalies">What was deliberately planted, for the walkthrough.</param>
public sealed record LoadDemoDatasetResult(
    Guid DatasetId,
    string Name,
    int RowCount,
    bool AlreadyExisted,
    IReadOnlyList<string> PlantedAnomalies);

/// <summary>
/// Generates and stores the synthetic demo dataset — the fast path for a
/// reviewer who wants to see the tool work without supplying their own data.
/// </summary>
/// <param name="ForceRegenerate">Generate a fresh copy even if one already exists.</param>
public sealed record LoadDemoDatasetCommand(bool ForceRegenerate = false)
    : IRequest<LoadDemoDatasetResult>;

/// <inheritdoc cref="LoadDemoDatasetCommand" />
public sealed class LoadDemoDatasetCommandHandler(
    IDatasetRepository repository,
    SyntheticDatasetGenerator generator,
    ILogger<LoadDemoDatasetCommandHandler> logger)
    : IRequestHandler<LoadDemoDatasetCommand, LoadDemoDatasetResult>
{
    /// <summary>
    /// Name carries the synthetic warning in the text itself, so it stays
    /// attached to the data everywhere it is shown — dataset pickers, report
    /// headers, exported views — and not only where a badge was remembered.
    /// </summary>
    private const string DemoDatasetName = "Demo dataset (synthetic — not real government data)";

    public async Task<LoadDemoDatasetResult> Handle(
        LoadDemoDatasetCommand request,
        CancellationToken cancellationToken)
    {
        var generated = generator.Generate();

        var plantedDescriptions = generated.PlantedAnomalies
            .Select(p => $"{p.Kind}: {p.Description}")
            .ToArray();

        if (!request.ForceRegenerate)
        {
            var existing = await repository.FindDemoDatasetAsync(cancellationToken);

            if (existing is not null)
            {
                return new LoadDemoDatasetResult(
                    existing.Id, existing.Name, existing.RowCount,
                    AlreadyExisted: true, plantedDescriptions);
            }
        }

        var dataset = new Dataset
        {
            Name = DemoDatasetName,
            SourceFileName = "synthetic-generator",
            IsSyntheticDemo = true,
            RowCount = generated.Transactions.Count,
            Transactions = generated.Transactions.ToList()
        };

        foreach (var transaction in dataset.Transactions)
        {
            transaction.DatasetId = dataset.Id;
        }

        await repository.AddAsync(dataset, cancellationToken);

        logger.LogInformation(
            "Generated synthetic demo dataset {DatasetId} with {RowCount} transactions and {Planted} planted anomalies.",
            dataset.Id, dataset.RowCount, generated.PlantedAnomalies.Count);

        return new LoadDemoDatasetResult(
            dataset.Id, dataset.Name, dataset.RowCount,
            AlreadyExisted: false, plantedDescriptions);
    }
}
