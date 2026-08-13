using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Application.Datasets.Commands.LoadDemoDataset;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BudgetGuard.Application.Datasets;

/// <summary>Generates the demo dataset at startup when the database is empty.</summary>
public static class DemoSeeder
{
    /// <summary>Configuration key controlling startup seeding.</summary>
    public const string SeedOnStartupKey = "Demo:SeedOnStartup";

    /// <summary>
    /// Seeds the synthetic demo dataset if no dataset exists yet.
    /// <para>
    /// The deployed demo runs on ephemeral container storage, so without this a
    /// reviewer opening the public URL after a restart would land on an empty
    /// tool and have to know to press a button before anything worked. Seeding
    /// only when the database is empty means it never overwrites real uploaded
    /// data, and the whole behaviour can be turned off with
    /// <c>Demo:SeedOnStartup=false</c> for any deployment holding genuine
    /// procurement records.
    /// </para>
    /// </summary>
    public static async Task SeedDemoDataIfEmptyAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue(SeedOnStartupKey, defaultValue: true))
        {
            return;
        }

        using var scope = services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IDatasetRepository>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DemoSeeder));

        if (await repository.GetMostRecentAsync(cancellationToken) is not null)
        {
            return;
        }

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new LoadDemoDatasetCommand(), cancellationToken);

        logger.LogInformation(
            "Seeded synthetic demo dataset {DatasetId} ({RowCount} transactions) into an empty database.",
            result.DatasetId, result.RowCount);
    }
}
