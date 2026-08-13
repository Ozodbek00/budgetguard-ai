using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using ValidationException = BudgetGuard.Application.Common.Exceptions.ValidationException;

namespace BudgetGuard.Application.Datasets.Commands.UploadDataset;

/// <summary>Result of ingesting an uploaded file.</summary>
/// <param name="DatasetId">The stored dataset.</param>
/// <param name="Name">Its display name.</param>
/// <param name="RowsAccepted">Transactions persisted.</param>
/// <param name="RowsSkipped">Rows that could not be parsed and were left out.</param>
/// <param name="Warnings">Per-row problems, so the auditor knows what was dropped.</param>
public sealed record UploadDatasetResult(
    Guid DatasetId,
    string Name,
    int RowsAccepted,
    int RowsSkipped,
    IReadOnlyList<string> Warnings);

/// <summary>Parses, validates and persists an uploaded procurement dataset.</summary>
/// <param name="Content">The uploaded file's content.</param>
/// <param name="FileName">Original filename; its extension selects CSV or Excel handling.</param>
/// <param name="DatasetName">Optional display name. Defaults to the filename.</param>
public sealed record UploadDatasetCommand(
    Stream Content,
    string FileName,
    string? DatasetName = null) : IRequest<UploadDatasetResult>;

/// <summary>Validates the upload request itself, before any file is read.</summary>
public sealed class UploadDatasetCommandValidator : AbstractValidator<UploadDatasetCommand>
{
    /// <summary>
    /// Extensions accepted. Kept in step with the parser's own list, which is
    /// asserted by a test so the two cannot drift apart.
    /// </summary>
    private static readonly string[] AllowedExtensions = [".csv", ".xlsx"];

    public UploadDatasetCommandValidator()
    {
        RuleFor(c => c.Content)
            .NotNull().WithMessage("No file content was provided.");

        RuleFor(c => c.FileName)
            .NotEmpty().WithMessage("A filename is required.")
            .Must(HasAllowedExtension)
            .WithMessage($"Only {string.Join(" and ", AllowedExtensions)} files are supported.");

        RuleFor(c => c.DatasetName)
            .MaximumLength(200)
            .WithMessage("Dataset name must be 200 characters or fewer.");
    }

    private static bool HasAllowedExtension(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        AllowedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}

/// <inheritdoc cref="UploadDatasetCommand" />
public sealed class UploadDatasetCommandHandler(
    IDatasetFileParser parser,
    IDatasetRepository repository,
    ILogger<UploadDatasetCommandHandler> logger)
    : IRequestHandler<UploadDatasetCommand, UploadDatasetResult>
{
    public async Task<UploadDatasetResult> Handle(
        UploadDatasetCommand request,
        CancellationToken cancellationToken)
    {
        var parsed = await parser.ParseAsync(request.Content, request.FileName, cancellationToken);

        if (!parsed.IsSuccess)
        {
            // Schema problems are the user's to fix, so they come back as
            // validation errors rather than a 500 — the upload screen renders
            // them next to the file picker.
            var errors = parsed.Errors.Count > 0
                ? parsed.Errors.ToArray()
                : ["The file contained no readable transaction rows."];

            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(UploadDatasetCommand.Content)] = errors
            });
        }

        var dataset = new Dataset
        {
            Name = string.IsNullOrWhiteSpace(request.DatasetName)
                ? Path.GetFileNameWithoutExtension(request.FileName)
                : request.DatasetName.Trim(),
            SourceFileName = Path.GetFileName(request.FileName),
            IsSyntheticDemo = false,
            RowCount = parsed.Transactions.Count,
            Transactions = parsed.Transactions.ToList()
        };

        foreach (var transaction in dataset.Transactions)
        {
            transaction.DatasetId = dataset.Id;
        }

        await repository.AddAsync(dataset, cancellationToken);

        logger.LogInformation(
            "Ingested dataset {DatasetId} from {FileName}: {Accepted} rows accepted, {Skipped} skipped.",
            dataset.Id, dataset.SourceFileName, parsed.Transactions.Count, parsed.SkippedRowCount);

        return new UploadDatasetResult(
            dataset.Id,
            dataset.Name,
            parsed.Transactions.Count,
            parsed.SkippedRowCount,
            parsed.RowWarnings);
    }
}
