using BudgetGuard.Application.Common.Interfaces;
using BudgetGuard.Infrastructure.Files;
using BudgetGuard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetGuard.Infrastructure;

/// <summary>Composition for the infrastructure layer.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the database, repositories and file parsing.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BudgetGuard")
                               ?? "Data Source=budgetguard.db";

        services.AddDbContext<BudgetGuardDbContext>(options =>
            options.UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly(typeof(BudgetGuardDbContext).Assembly.FullName)));

        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddSingleton<IDatasetFileParser, DatasetFileParser>();

        return services;
    }

    /// <summary>
    /// Applies pending migrations at startup.
    /// <para>
    /// Appropriate here because the database is a single SQLite file owned by
    /// this one application — there is no second writer to race with, and the
    /// alternative on a container platform is a release step the deployment
    /// does not otherwise need. It would not be appropriate against a shared
    /// server database with multiple instances rolling.
    /// </para>
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BudgetGuardDbContext>();

        EnsureDatabaseDirectoryExists(context.Database.GetConnectionString());

        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Creates the directory holding the SQLite file if it is missing.
    /// <para>
    /// SQLite will create the database file but not the folder above it, so a
    /// connection string like <c>Data Source=data/budgetguard.db</c> fails on a
    /// clean checkout and inside a fresh container with "unable to open database
    /// file" — an error that says nothing about the actual cause.
    /// </para>
    /// </summary>
    private static void EnsureDatabaseDirectoryExists(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
