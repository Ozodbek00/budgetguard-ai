using System.Text;
using BudgetGuard.Application.Analysis.Queries.GetAnomalyReport;
using BudgetGuard.Application.Analysis.Services;
using BudgetGuard.Application.Datasets.Commands.UploadDataset;
using BudgetGuard.Domain.Demo;
using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Benford;
using BudgetGuard.Domain.Detection.Concentration;
using BudgetGuard.Domain.Detection.Outliers;
using BudgetGuard.Domain.Entities;
using BudgetGuard.Infrastructure.Files;
using BudgetGuard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ValidationException = BudgetGuard.Application.Common.Exceptions.ValidationException;

namespace BudgetGuard.Application.Tests;

/// <summary>
/// Handler tests over the real persistence stack.
/// <para>
/// These run against SQLite in memory rather than the EF in-memory provider, so
/// they exercise the actual provider behaviour. That matters here: two bugs in
/// this project were provider-specific — SQLite cannot ORDER BY a
/// DateTimeOffset, and it will not create the directory holding the database
/// file. The in-memory provider would have hidden both.
/// </para>
/// </summary>
public sealed class UploadAndReportTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BudgetGuardDbContext _context;
    private readonly DatasetRepository _repository;

    public UploadAndReportTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BudgetGuardDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BudgetGuardDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new DatasetRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private UploadDatasetCommandHandler UploadHandler() =>
        new(new DatasetFileParser(), _repository,
            NullLogger<UploadDatasetCommandHandler>.Instance);

    private GetAnomalyReportQueryHandler ReportHandler()
    {
        var settings = new DetectionSettings();

        var aggregator = new AnomalyAggregator(
            new BenfordAnalyzer(settings.Benford),
            new ZScoreOutlierDetector(settings.ZScore),
            new VendorConcentrationAnalyzer(settings.VendorConcentration),
            settings);

        var analysisService = new AnalysisService(
            _repository, aggregator, new AnalysisCache(), TimeProvider.System);

        return new GetAnomalyReportQueryHandler(analysisService);
    }

    private static Stream Csv(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    private async Task<Guid> SeedDemoAsync()
    {
        var generated = new SyntheticDatasetGenerator().Generate();

        var dataset = new Dataset
        {
            Name = "Demo",
            SourceFileName = "synthetic-generator",
            IsSyntheticDemo = true,
            RowCount = generated.Transactions.Count,
            Transactions = generated.Transactions.ToList()
        };

        foreach (var transaction in dataset.Transactions)
        {
            transaction.DatasetId = dataset.Id;
        }

        await _repository.AddAsync(dataset);
        return dataset.Id;
    }

    // -----------------------------------------------------------------
    // Upload
    // -----------------------------------------------------------------

    [Fact]
    public async Task Upload_persists_the_dataset_and_its_transactions()
    {
        const string csv = """
            TransactionDate,Amount,VendorName,Category,Department
            2025-03-14,1000,Vendor A,Construction,Ministry A
            2025-03-15,2000,Vendor B,Construction,Ministry A
            """;

        var result = await UploadHandler().Handle(
            new UploadDatasetCommand(Csv(csv), "spending.csv"), default);

        Assert.Equal(2, result.RowsAccepted);
        Assert.Equal(0, result.RowsSkipped);

        var stored = await _repository.GetAsync(result.DatasetId);
        Assert.NotNull(stored);
        Assert.Equal("spending", stored.Name);
        Assert.Equal("spending.csv", stored.SourceFileName);
        Assert.False(stored.IsSyntheticDemo);

        var transactions = await _repository.GetTransactionsAsync(result.DatasetId);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal(result.DatasetId, t.DatasetId));
    }

    [Fact]
    public async Task Upload_uses_the_supplied_name_when_given_one()
    {
        const string csv = """
            TransactionDate,Amount,VendorName,Category,Department
            2025-03-14,1000,Vendor A,Construction,Ministry A
            """;

        var result = await UploadHandler().Handle(
            new UploadDatasetCommand(Csv(csv), "spending.csv", "Q1 2025 health spending"), default);

        Assert.Equal("Q1 2025 health spending", result.Name);
    }

    [Fact]
    public async Task Upload_reports_skipped_rows_rather_than_hiding_them()
    {
        const string csv = """
            TransactionDate,Amount,VendorName,Category,Department
            2025-03-14,1000,Vendor A,Construction,Ministry A
            bad-date,2000,Vendor B,Construction,Ministry A
            """;

        var result = await UploadHandler().Handle(
            new UploadDatasetCommand(Csv(csv), "messy.csv"), default);

        Assert.Equal(1, result.RowsAccepted);
        Assert.Equal(1, result.RowsSkipped);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task Schema_failures_surface_as_validation_errors_not_server_errors()
    {
        // A missing column is the user's problem to fix, so it must come back as
        // something the upload screen can render next to the file picker.
        const string csv = """
            TransactionDate,Amount,VendorName
            2025-03-14,1000,Vendor A
            """;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            UploadHandler().Handle(new UploadDatasetCommand(Csv(csv), "bad.csv"), default));

        Assert.Contains(exception.AllMessages, m => m.Contains("Department"));
    }

    // -----------------------------------------------------------------
    // Repository behaviour that was provider-specific
    // -----------------------------------------------------------------

    [Fact]
    public async Task Datasets_are_listed_newest_first()
    {
        // Regression: ordering by DateTimeOffset threw on SQLite, so this whole
        // path returned a 500 until timestamps were stored as UTC ticks.
        for (var i = 0; i < 3; i++)
        {
            await _repository.AddAsync(new Dataset
            {
                Name = $"Dataset {i}",
                SourceFileName = "x.csv",
                UploadedAtUtc = DateTimeOffset.UtcNow.AddMinutes(i)
            });
        }

        var datasets = await _repository.ListAsync();

        Assert.Equal(3, datasets.Count);
        Assert.Equal("Dataset 2", datasets[0].Name);
        Assert.Equal("Dataset 0", datasets[2].Name);

        var mostRecent = await _repository.GetMostRecentAsync();
        Assert.Equal("Dataset 2", mostRecent!.Name);
    }

    [Fact]
    public async Task Upload_timestamps_survive_the_round_trip_to_the_database()
    {
        var when = new DateTimeOffset(2026, 8, 13, 14, 30, 15, TimeSpan.Zero);

        var dataset = await _repository.AddAsync(new Dataset
        {
            Name = "Timed", SourceFileName = "x.csv", UploadedAtUtc = when
        });

        var reloaded = await _repository.GetAsync(dataset.Id);

        Assert.Equal(when, reloaded!.UploadedAtUtc);
    }

    [Fact]
    public async Task Decimal_amounts_round_trip_exactly()
    {
        // Benford reads the literal leading digit, so a value that comes back
        // even slightly changed could be assigned to a different digit bucket.
        const string csv = """
            TransactionDate,Amount,VendorName,Category,Department
            2025-03-14,0.30,Vendor A,Construction,Ministry A
            2025-03-15,12345678.99,Vendor B,Construction,Ministry A
            2025-03-16,0.000071,Vendor C,Construction,Ministry A
            """;

        var result = await UploadHandler().Handle(
            new UploadDatasetCommand(Csv(csv), "precise.csv"), default);

        var amounts = (await _repository.GetTransactionsAsync(result.DatasetId))
            .Select(t => t.Amount)
            .OrderBy(a => a)
            .ToArray();

        Assert.Equal(0.000071m, amounts[0]);
        Assert.Equal(0.30m, amounts[1]);
        Assert.Equal(12345678.99m, amounts[2]);

        Assert.Equal(7, BenfordAnalyzer.LeadingDigit(amounts[0]));
        Assert.Equal(3, BenfordAnalyzer.LeadingDigit(amounts[1]));
        Assert.Equal(1, BenfordAnalyzer.LeadingDigit(amounts[2]));
    }

    [Fact]
    public async Task Finding_the_demo_dataset_ignores_uploaded_ones()
    {
        await _repository.AddAsync(new Dataset { Name = "Real", SourceFileName = "real.csv" });
        var demoId = await SeedDemoAsync();

        var found = await _repository.FindDemoDatasetAsync();

        Assert.Equal(demoId, found!.Id);
    }

    // -----------------------------------------------------------------
    // Report query
    // -----------------------------------------------------------------

    [Fact]
    public async Task Report_returns_ranked_findings_for_the_demo_dataset()
    {
        var datasetId = await SeedDemoAsync();

        var report = await ReportHandler().Handle(new GetAnomalyReportQuery(datasetId), default);

        Assert.True(report.IsSyntheticDemo);
        Assert.NotEmpty(report.Findings);
        Assert.True(report.Summary.Critical >= 2);

        var scores = report.Findings.Select(f => f.RiskScore).ToArray();
        Assert.Equal(scores.OrderByDescending(s => s), scores);

        Assert.All(report.Findings, f =>
        {
            Assert.NotEmpty(f.Signals);
            Assert.False(string.IsNullOrWhiteSpace(f.PrimaryExplanation));
        });
    }

    [Fact]
    public async Task Report_falls_back_to_the_most_recent_dataset_when_none_is_specified()
    {
        await SeedDemoAsync();

        var report = await ReportHandler().Handle(new GetAnomalyReportQuery(), default);

        Assert.True(report.TransactionCount > 0);
    }

    [Fact]
    public async Task Missing_dataset_produces_a_not_found_rather_than_an_empty_report()
    {
        // An empty table where the dataset does not exist would read as
        // "no anomalies found", which is the worst possible wrong answer here.
        await Assert.ThrowsAsync<BudgetGuard.Application.Common.Exceptions.NotFoundException>(() =>
            ReportHandler().Handle(new GetAnomalyReportQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Severity_filter_narrows_the_rows_returned()
    {
        var datasetId = await SeedDemoAsync();
        var handler = ReportHandler();

        var all = await handler.Handle(new GetAnomalyReportQuery(datasetId), default);
        var critical = await handler.Handle(
            new GetAnomalyReportQuery(datasetId, MinimumSeverity: Severity.Critical), default);

        Assert.True(critical.Findings.Count < all.Findings.Count);
        Assert.All(critical.Findings, f => Assert.Equal(nameof(Severity.Critical), f.Severity));
    }

    [Fact]
    public async Task Filters_narrow_the_rows_but_never_the_population_the_statistics_used()
    {
        // This is the property that makes a filtered view quotable: the summary
        // and transaction count must keep describing the whole dataset, or the
        // numbers behind a filtered report would not match the unfiltered one.
        var datasetId = await SeedDemoAsync();
        var handler = ReportHandler();

        var all = await handler.Handle(new GetAnomalyReportQuery(datasetId), default);

        var filtered = await handler.Handle(
            new GetAnomalyReportQuery(datasetId, Category: "Construction"), default);

        Assert.Equal(all.TransactionCount, filtered.TransactionCount);
        Assert.Equal(all.Summary.Critical, filtered.Summary.Critical);
        Assert.Equal(all.Summary.Total, filtered.Summary.Total);
        Assert.True(filtered.Findings.Count <= all.Findings.Count);
    }

    [Fact]
    public async Task Category_filter_matches_case_insensitively()
    {
        var datasetId = await SeedDemoAsync();
        var handler = ReportHandler();

        var exact = await handler.Handle(
            new GetAnomalyReportQuery(datasetId, Category: "Construction"), default);
        var lower = await handler.Handle(
            new GetAnomalyReportQuery(datasetId, Category: "construction"), default);

        Assert.Equal(exact.Findings.Count, lower.Findings.Count);
    }

    [Fact]
    public async Task Repeated_queries_reuse_one_detection_run()
    {
        var datasetId = await SeedDemoAsync();
        var handler = ReportHandler();

        var first = await handler.Handle(new GetAnomalyReportQuery(datasetId), default);
        var second = await handler.Handle(new GetAnomalyReportQuery(datasetId), default);

        // Same run, so the generation timestamp is identical rather than merely
        // close. Datasets are immutable, so this cannot serve stale results.
        Assert.Equal(first.GeneratedAtUtc, second.GeneratedAtUtc);
    }
}
