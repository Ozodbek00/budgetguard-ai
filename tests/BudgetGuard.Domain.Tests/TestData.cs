using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Tests;

/// <summary>
/// Builders for constructed test data.
/// <para>
/// Every detector test builds its population explicitly so the expected result
/// is derivable by hand. Where randomness is used it is always seeded, because
/// a detection test that passes intermittently is worse than no test.
/// </para>
/// </summary>
internal static class TestData
{
    internal static ProcurementTransaction Transaction(
        decimal amount,
        string vendor = "Vendor A",
        string category = "Construction",
        string department = "Department A",
        string? reference = null,
        DateOnly? date = null) =>
        new()
        {
            ExternalReference = reference ?? $"REF-{Guid.NewGuid().ToString()[..8]}",
            Amount = amount,
            VendorName = vendor,
            Category = category,
            Department = department,
            TransactionDate = date ?? new DateOnly(2025, 6, 1),
            Description = "Test transaction"
        };

    /// <summary>
    /// Builds <paramref name="count"/> transactions whose amounts follow
    /// Benford's Law, using 10^u with a uniform exponent and a fixed seed.
    /// </summary>
    internal static List<ProcurementTransaction> BenfordConforming(
        int count,
        int seed = 12345,
        string vendor = "Vendor A",
        string category = "Category A",
        string department = "Department A")
    {
        var random = new Random(seed);
        var transactions = new List<ProcurementTransaction>(count);

        for (var i = 0; i < count; i++)
        {
            var amount = (decimal)Math.Round(1000d * Math.Pow(10d, random.NextDouble() * 3d), 2);
            transactions.Add(Transaction(amount, vendor, category, department));
        }

        return transactions;
    }

    /// <summary>
    /// Builds amounts whose first digits match Benford's Law almost exactly, by
    /// construction rather than by sampling. Used where a test must assert a
    /// clean verdict with no dependence on sampling luck.
    /// </summary>
    internal static List<decimal> ExactBenfordAmounts(int totalCount)
    {
        var amounts = new List<decimal>(totalCount);

        for (var digit = 1; digit <= 9; digit++)
        {
            var expected = (int)Math.Round(totalCount * Math.Log10(1d + 1d / digit));

            for (var i = 0; i < expected; i++)
            {
                // digit*1000 .. digit*1000+999 all lead with `digit`.
                amounts.Add(digit * 1000m + i % 1000);
            }
        }

        return amounts;
    }

    /// <summary>Amounts spread evenly across all nine leading digits — a distribution Benford forbids.</summary>
    internal static List<decimal> UniformDigitAmounts(int perDigit)
    {
        var amounts = new List<decimal>(perDigit * 9);

        for (var digit = 1; digit <= 9; digit++)
        {
            for (var i = 0; i < perDigit; i++)
            {
                amounts.Add(digit * 1000m + i % 1000);
            }
        }

        return amounts;
    }
}
