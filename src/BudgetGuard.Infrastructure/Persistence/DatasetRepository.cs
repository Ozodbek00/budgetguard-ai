using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetGuard.Infrastructure.Persistence;

/// <inheritdoc cref="IDatasetRepository" />
public sealed class DatasetRepository(BudgetGuardDbContext context) : IDatasetRepository
{
    public async Task<Dataset> AddAsync(Dataset dataset, CancellationToken cancellationToken = default)
    {
        context.Datasets.Add(dataset);
        await context.SaveChangesAsync(cancellationToken);

        return dataset;
    }

    public async Task<Dataset?> GetAsync(Guid datasetId, CancellationToken cancellationToken = default) =>
        await context.Datasets
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == datasetId, cancellationToken);

    public async Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.Datasets
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Loads a dataset's transactions for analysis.
    /// <para>
    /// Materialises the whole set deliberately. Every detector needs the full
    /// population — a Benford distribution, a group's standard deviation and a
    /// vendor's share of spend are all properties of the entire dataset, so
    /// there is nothing to stream or paginate. At demo scale this is a single
    /// indexed read of a few thousand rows.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ProcurementTransaction>> GetTransactionsAsync(
        Guid datasetId,
        CancellationToken cancellationToken = default) =>
        await context.Transactions
            .AsNoTracking()
            .Where(t => t.DatasetId == datasetId)
            .ToListAsync(cancellationToken);

    public async Task<Dataset?> GetMostRecentAsync(CancellationToken cancellationToken = default) =>
        await context.Datasets
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Dataset?> FindDemoDatasetAsync(CancellationToken cancellationToken = default) =>
        await context.Datasets
            .AsNoTracking()
            .Where(d => d.IsSyntheticDemo)
            .OrderByDescending(d => d.UploadedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
}
