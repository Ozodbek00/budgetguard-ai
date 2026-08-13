using BudgetGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetGuard.Infrastructure.Persistence;

/// <summary>
/// EF Core context over SQLite.
/// <para>
/// The domain entities carry no EF attributes — mapping lives here, in
/// <see cref="OnModelCreating"/>, so the domain stays free of persistence
/// concerns. See docs/adr/0002-sqlite-over-postgres-for-mvp.md for why SQLite.
/// </para>
/// </summary>
public sealed class BudgetGuardDbContext(DbContextOptions<BudgetGuardDbContext> options)
    : DbContext(options)
{
    public DbSet<Dataset> Datasets => Set<Dataset>();

    public DbSet<ProcurementTransaction> Transactions => Set<ProcurementTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dataset>(entity =>
        {
            entity.ToTable("Datasets");
            entity.HasKey(d => d.Id);

            entity.Property(d => d.Name).IsRequired().HasMaxLength(200);
            entity.Property(d => d.SourceFileName).IsRequired().HasMaxLength(400);
            // SQLite has no native date type and cannot ORDER BY a
            // DateTimeOffset, which every "newest dataset first" query needs.
            // Storing UTC ticks makes the ordering exact rather than
            // lexicographic-by-accident. The offset is not preserved, which is
            // correct: this value is always UTC, as its name says.
            entity.Property(d => d.UploadedAtUtc)
                .IsRequired()
                .HasConversion(
                    value => value.UtcTicks,
                    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
            entity.Property(d => d.IsSyntheticDemo).IsRequired();
            entity.Property(d => d.RowCount).IsRequired();

            entity.HasMany(d => d.Transactions)
                .WithOne()
                .HasForeignKey(t => t.DatasetId)
                .OnDelete(DeleteBehavior.Cascade);

            // Datasets are listed newest-first on every screen.
            entity.HasIndex(d => d.UploadedAtUtc);
        });

        modelBuilder.Entity<ProcurementTransaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.ExternalReference).IsRequired().HasMaxLength(100);
            entity.Property(t => t.TransactionDate).IsRequired();
            entity.Property(t => t.Currency).IsRequired().HasMaxLength(10);
            entity.Property(t => t.VendorName).IsRequired().HasMaxLength(300);
            entity.Property(t => t.Category).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Department).IsRequired().HasMaxLength(300);
            entity.Property(t => t.Description).HasMaxLength(1000);

            // The SQLite provider stores decimal as TEXT, preserving the exact
            // digits written. That matters here beyond the usual money argument:
            // Benford analysis reads the literal leading digit, so a value
            // round-tripped through binary floating point could come back with a
            // different first digit than it went in with.
            // (The provider does this by default; it is stated explicitly here
            // because a future move to a provider that maps decimal to a binary
            // type would need the Benford implications reconsidered.)
            entity.Property(t => t.Amount).IsRequired();

            // Every analysis loads one dataset's transactions in full.
            entity.HasIndex(t => t.DatasetId);
        });
    }
}
