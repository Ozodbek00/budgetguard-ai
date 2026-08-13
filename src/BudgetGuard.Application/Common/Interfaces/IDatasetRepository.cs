using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Application.Common.Interfaces;

/// <summary>
/// Persistence for datasets and their transactions.
/// <para>
/// Declared here, implemented in Infrastructure. The Application layer states
/// what it needs from storage; it never learns that storage is EF Core over
/// SQLite, which is what keeps handlers testable and the database swappable.
/// </para>
/// </summary>
public interface IDatasetRepository
{
    /// <summary>Persists a dataset and all of its transactions.</summary>
    Task<Dataset> AddAsync(Dataset dataset, CancellationToken cancellationToken = default);

    /// <summary>Loads a dataset's metadata, without its transactions.</summary>
    Task<Dataset?> GetAsync(Guid datasetId, CancellationToken cancellationToken = default);

    /// <summary>Every dataset, most recently uploaded first.</summary>
    Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads every transaction in a dataset, for analysis.</summary>
    Task<IReadOnlyList<ProcurementTransaction>> GetTransactionsAsync(
        Guid datasetId,
        CancellationToken cancellationToken = default);

    /// <summary>The most recently uploaded dataset, or null when none exist.</summary>
    Task<Dataset?> GetMostRecentAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds an existing demo dataset so repeated demo loads reuse one copy.</summary>
    Task<Dataset?> FindDemoDatasetAsync(CancellationToken cancellationToken = default);
}
