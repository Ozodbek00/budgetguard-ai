using System.Collections.Concurrent;
using BudgetGuard.Application.Common.Exceptions;
using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Explanations;
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
    /// <param name="datasetId">Dataset the analysis belongs to.</param>
    /// <param name="languageTag">
    /// Language the explanations were written in. Part of the key because the
    /// cached report contains rendered sentences: without it, the first visitor
    /// would fix the language for everyone who followed.
    /// </param>
    /// <param name="analysis">The cached analysis, when present.</param>
    bool TryGet(Guid datasetId, string languageTag, out DatasetAnalysis analysis);

    void Set(Guid datasetId, string languageTag, DatasetAnalysis analysis);
}

/// <summary>Bounded in-memory analysis cache. Evicts the oldest entry when full.</summary>
public sealed class AnalysisCache : IAnalysisCache
{
    /// <summary>
    /// Deliberately small. This exists to stop three page views re-running one
    /// pipeline, not to be a general-purpose cache, and an unbounded dictionary
    /// on a long-running demo instance would simply be a leak.
    /// </summary>
    /// <summary>
    /// Deliberately small, and sized per dataset-and-language pair: three
    /// languages means one dataset can occupy three slots.
    /// </summary>
    private const int Capacity = 12;

    private readonly ConcurrentDictionary<(Guid, string), DatasetAnalysis> _entries = new();

    public bool TryGet(Guid datasetId, string languageTag, out DatasetAnalysis analysis) =>
        _entries.TryGetValue((datasetId, languageTag), out analysis!);

    public void Set(Guid datasetId, string languageTag, DatasetAnalysis analysis)
    {
        var key = (datasetId, languageTag);

        if (_entries.Count >= Capacity && !_entries.ContainsKey(key))
        {
            var oldest = _entries
                .OrderBy(e => e.Value.GeneratedAtUtc)
                .Select(e => e.Key)
                .FirstOrDefault();

            if (oldest != default)
            {
                _entries.TryRemove(oldest, out _);
            }
        }

        _entries[key] = analysis;
    }
}

/// <inheritdoc cref="IAnalysisService" />
public sealed class AnalysisService(
    IDatasetRepository repository,
    IAnomalyAggregator aggregator,
    IAnalysisCache cache,
    IExplanationWriter explanationWriter,
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

        var language = explanationWriter.LanguageTag;

        if (cache.TryGet(dataset.Id, language, out var cached))
        {
            return cached;
        }

        var transactions = await repository.GetTransactionsAsync(dataset.Id, cancellationToken);
        var report = aggregator.Analyze(transactions, dataset.Name);

        var analysis = new DatasetAnalysis(dataset, report, timeProvider.GetUtcNow());
        cache.Set(dataset.Id, language, analysis);

        return analysis;
    }
}
