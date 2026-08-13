using BudgetGuard.Application.Common.Interfaces;
using MediatR;

namespace BudgetGuard.Application.Datasets.Queries.GetDatasets;

/// <summary>A dataset as shown in the picker.</summary>
/// <param name="Id">Dataset identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="SourceFileName">Original filename, for provenance.</param>
/// <param name="UploadedAtUtc">When it was ingested.</param>
/// <param name="RowCount">Transactions it contains.</param>
/// <param name="IsSyntheticDemo">True when this is generated demo data, not real spending.</param>
public sealed record DatasetSummaryDto(
    Guid Id,
    string Name,
    string SourceFileName,
    DateTimeOffset UploadedAtUtc,
    int RowCount,
    bool IsSyntheticDemo);

/// <summary>Lists stored datasets, most recent first.</summary>
public sealed record GetDatasetsQuery : IRequest<IReadOnlyList<DatasetSummaryDto>>;

/// <inheritdoc cref="GetDatasetsQuery" />
public sealed class GetDatasetsQueryHandler(IDatasetRepository repository)
    : IRequestHandler<GetDatasetsQuery, IReadOnlyList<DatasetSummaryDto>>
{
    public async Task<IReadOnlyList<DatasetSummaryDto>> Handle(
        GetDatasetsQuery request,
        CancellationToken cancellationToken)
    {
        var datasets = await repository.ListAsync(cancellationToken);

        return datasets
            .Select(d => new DatasetSummaryDto(
                d.Id, d.Name, d.SourceFileName, d.UploadedAtUtc, d.RowCount, d.IsSyntheticDemo))
            .ToArray();
    }
}
