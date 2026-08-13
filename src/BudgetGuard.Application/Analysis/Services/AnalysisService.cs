using System.Collections.Concurrent;
using BudgetGuard.Application.Common.Exceptions;
using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Application.Analysis.Services;

/// <summary>A dataset together with the detection run performed over it.</summary>
/// <param name="Dataset">The analysed dataset's metadata.</param>
/// <param name="Report">The detection engine's output.</param>
/// <param name="GeneratedAtUtc">When the run happened.</param>
public sealed record DatasetAnalysis(
    Dataset Dataset,
    AnomalyReport Report,
    DateTimeOffset GeneratedAtUtc);

/// <summary>Loads a dataset and runs the detection pipeline over it.</summary>
public interface IAnalysisService
{
    /// <summary>
    /// Analyses a dataset, or the most recently uploaded one when
    /// <paramref name="datasetId"/> is null.
    /// </summary>
    Task<DatasetAnalysis> AnalyzeAsync(Guid? datasetId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Caches completed analyses.
/// <para>
/// Datasets are immutable once uploaded, so a report for a given dataset can
/// never go stale — caching it is not a consistency risk, and it matters
/// because the anomaly report, the Benford chart and the vendor risk view are
/// three views of one detection run. Without this, moving between them would
/// re-run the whole pipeline each time.
/// </para>
/// </summary>
public interface IAnalysisCache
{
    bool TryGet(Guid datasetId, out DatasetAnalysis analysis);

    void Set(Guid datasetId, DatasetAnalysis analysis);
}

/// <summary>Bounded in-memory analysis cache. Evicts the oldest entry when full.</summary>
public sealed class AnalysisCache : IAnalysisCache
{
    /// <summary>
    /// Deliberately small. This exists to stop three page views re-running one
    /// pipeline, not to be a general-purpose cache, and an unbounded dictionary
    /// on a long-running demo instance would simply be a leak.
    /// </summary>
    private const int Capacity = 8;

    private readonly ConcurrentDictionary<Guid, DatasetAnalysis> _entries = new();

    public bool TryGet(Guid datasetId, out DatasetAnalysis analysis) =>
        _entries.TryGetValue(datasetId, out analysis!);

    public void Set(Guid datasetId, DatasetAnalysis analysis)
    {
        if (_entries.Count >= Capacity && !_entries.ContainsKey(datasetId))
        {
            var oldest = _entries
                .OrderBy(e => e.Value.GeneratedAtUtc)
                .Select(e => e.Key)
                .FirstOrDefault();

            if (oldest != Guid.Empty)
            {
                _entries.TryRemove(oldest, out _);
            }
        }

        _entries[datasetId] = analysis;
    }
}

/// <inheritdoc cref="IAnalysisService" />
public sealed class AnalysisService(
    IDatasetRepository repository,
    IAnomalyAggregator aggregator,
    IAnalysisCache cache,
    TimeProvider timeProvider) : IAnalysisService
{
    /// <inheritdoc />
    public async Task<DatasetAnalysis> AnalyzeAsync(
        Guid? datasetId,
        CancellationToken cancellationToken = default)
    {
        var dataset = datasetId is { } id
            ? await repository.GetAsync(id, cancellationToken)
              ?? throw new NotFoundException(nameof(Dataset), id)
            : await repository.GetMostRecentAsync(cancellationToken)
              ?? throw new NotFoundException(nameof(Dataset), "most recent");

        if (cache.TryGet(dataset.Id, out var cached))
        {
            return cached;
        }

        var transactions = await repository.GetTransactionsAsync(dataset.Id, cancellationToken);
        var report = aggregator.Analyze(transactions, dataset.Name);

        var analysis = new DatasetAnalysis(dataset, report, timeProvider.GetUtcNow());
        cache.Set(dataset.Id, analysis);

        return analysis;
    }
}
