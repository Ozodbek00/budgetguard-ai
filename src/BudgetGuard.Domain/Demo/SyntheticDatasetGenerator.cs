using BudgetGuard.Domain.Entities;

namespace BudgetGuard.Domain.Demo;

/// <summary>
/// Builds a realistic-looking procurement dataset with known manipulations planted in it.
/// <para>
/// <b>This produces fictional data.</b> Every vendor, department and amount is
/// invented. It exists so the platform can be demonstrated and, more
/// importantly, so the detection engine can be scored against ground truth: the
/// generator records exactly what it planted, and the test suite asserts the
/// detectors find those things and stay quiet on the clean rows.
/// </para>
/// <para>
/// <b>How the clean baseline is built.</b> Ordinary amounts are drawn as
/// 10^u for u uniform over several orders of magnitude. That is the standard
/// construction for Benford-conforming data: a uniform mantissa in log space
/// produces exactly the log10(1 + 1/d) first-digit law. Using it means the
/// clean portion of the dataset genuinely behaves like natural spending rather
/// than merely looking plausible, so a false positive on it is a real failure.
/// </para>
/// </summary>
public sealed class SyntheticDatasetGenerator
{
    private static readonly string[] Categories =
    [
        "Construction", "Medical Supplies", "IT Equipment", "Office Supplies",
        "Vehicle Fleet", "Catering Services", "Road Maintenance", "Textbooks"
    ];

    private static readonly string[] Departments =
    [
        "Ministry of Health", "Ministry of Education", "Ministry of Transport",
        "Tashkent City Administration", "Samarkand Regional Administration"
    ];

    /// <summary>Fictional supplier names used for the clean baseline.</summary>
    private static readonly string[] Vendors =
    [
        "Oltin Yo'l Ta'minot", "Navoiy Savdo Guruh", "Chirchiq Sanoat MChJ",
        "Buxoro Logistika", "Zarafshon Tibbiyot", "Farg'ona Qurilish Servis",
        "Andijon Texnika", "Nurafshon Ta'minot", "Sirdaryo Transport MChJ",
        "Qashqadaryo Materiallar", "Xorazm Ofis Yechimlari", "Jizzax Injiniring",
        "Surxon Oziq-ovqat", "Toshkent Raqamli Tizimlar", "Amudaryo Yetkazib Berish",
        "Registon Ta'lim Nashri", "Chorvoq Energetika", "Zomin Avtomobil Servis"
    ];

    private const string ConcentratedVendor = "Alfa Qurilish Invest";
    private const string RoundNumberVendor = "Zamon Ta'minot Guruh";
    private const string ConcentratedCategory = "Construction";

    /// <summary>Approval ceiling the threshold-evasion rows are engineered to sit just under.</summary>
    private const decimal ApprovalCeiling = 50_000_000m;

    /// <summary>Generates a demo dataset and the ground truth describing what was planted in it.</summary>
    /// <param name="options">Generation knobs. Defaults produce the standard demo dataset.</param>
    public SyntheticDataset Generate(SyntheticDataOptions? options = null)
    {
        options ??= new SyntheticDataOptions();

        var random = new Random(options.Seed);
        var transactions = new List<ProcurementTransaction>();
        var planted = new List<PlantedAnomaly>();
        var reference = 1;

        string NextReference() => $"BG-{reference++:D5}";

        DateOnly RandomDate()
        {
            var span = options.EndDate.DayNumber - options.StartDate.DayNumber;
            return options.StartDate.AddDays(random.Next(span + 1));
        }

        // ---------------------------------------------------------------
        // 1. Clean baseline: Benford-conforming amounts spread over vendors,
        //    categories and departments. These rows must NOT be flagged.
        // ---------------------------------------------------------------
        for (var i = 0; i < options.CleanTransactionCount; i++)
        {
            var category = Categories[random.Next(Categories.Length)];

            transactions.Add(new ProcurementTransaction
            {
                ExternalReference = NextReference(),
                TransactionDate = RandomDate(),
                Amount = BenfordAmount(random, category),
                VendorName = Vendors[random.Next(Vendors.Length)],
                Category = category,
                Department = Departments[random.Next(Departments.Length)],
                Description = $"Routine procurement — {category}"
            });
        }

        // ---------------------------------------------------------------
        // 2. Threshold evasion: contracts priced just below the approval
        //    ceiling to avoid the tender that a larger award would trigger.
        //    Bends the dataset-wide first-digit curve toward 4.
        // ---------------------------------------------------------------
        for (var i = 0; i < options.ThresholdEvasionCount; i++)
        {
            var category = Categories[random.Next(Categories.Length)];

            // 40,000,000 - 49,900,000: always leads with 4, just under the ceiling.
            // Rounded to the nearest 10,000, as hand-priced contracts tend to be.
            var raw = ApprovalCeiling * 0.80m + (decimal)random.NextDouble() * (ApprovalCeiling * 0.198m);
            var amount = Math.Round(raw / 10_000m) * 10_000m;

            transactions.Add(new ProcurementTransaction
            {
                ExternalReference = NextReference(),
                TransactionDate = RandomDate(),
                Amount = amount,
                VendorName = Vendors[random.Next(Vendors.Length)],
                Category = category,
                Department = Departments[random.Next(Departments.Length)],
                Description = $"Direct award below approval ceiling — {category}"
            });
        }

        planted.Add(new PlantedAnomaly(
            PlantedAnomalyKind.ThresholdEvasion,
            "dataset",
            $"{options.ThresholdEvasionCount} contracts priced between 80% and 99.8% of the " +
            $"{ApprovalCeiling:N0} approval ceiling, over-representing leading digit 4 across the dataset.",
            options.ThresholdEvasionCount));

        // ---------------------------------------------------------------
        // 3. Round-number invoicing: one vendor whose amounts are manufactured
        //    rather than costed. Should fail per-vendor Benford.
        // ---------------------------------------------------------------
        for (var i = 0; i < options.RoundNumberInvoiceCount; i++)
        {
            // Amounts like 25,000,000 / 30,000,000 / 75,000,000 — human-chosen,
            // heavily biased toward digits 2, 3, 5 and 7.
            var choices = new[] { 25m, 30m, 50m, 75m, 20m, 35m, 25m, 50m };
            var amount = choices[random.Next(choices.Length)] * 1_000_000m;

            transactions.Add(new ProcurementTransaction
            {
                ExternalReference = NextReference(),
                TransactionDate = RandomDate(),
                Amount = amount,
                VendorName = RoundNumberVendor,
                Category = "Medical Supplies",
                Department = "Ministry of Health",
                Description = "Consumables supply agreement"
            });
        }

        planted.Add(new PlantedAnomaly(
            PlantedAnomalyKind.RoundNumberInvoicing,
            RoundNumberVendor,
            $"{options.RoundNumberInvoiceCount} invoices from {RoundNumberVendor}, all round " +
            "multiples of 5,000,000, producing a first-digit distribution that cannot occur naturally.",
            options.RoundNumberInvoiceCount));

        // ---------------------------------------------------------------
        // 4. Vendor concentration: one supplier taking an implausible share of
        //    the Construction category.
        // ---------------------------------------------------------------
        for (var i = 0; i < options.ConcentrationContractCount; i++)
        {
            transactions.Add(new ProcurementTransaction
            {
                ExternalReference = NextReference(),
                TransactionDate = RandomDate(),
                Amount = BenfordAmount(random, ConcentratedCategory) * 3m,
                VendorName = ConcentratedVendor,
                Category = ConcentratedCategory,
                Department = "Tashkent City Administration",
                Description = "Infrastructure works package"
            });
        }

        planted.Add(new PlantedAnomaly(
            PlantedAnomalyKind.VendorConcentration,
            ConcentratedVendor,
            $"{ConcentratedVendor} awarded {options.ConcentrationContractCount} high-value " +
            $"contracts in the {ConcentratedCategory} category, taking a share far above the " +
            "even-split expectation for the number of competing vendors.",
            options.ConcentrationContractCount));

        // ---------------------------------------------------------------
        // 5. Extreme outliers: individual payments far outside their category's
        //    normal range. Should be caught by peer-relative z-score.
        // ---------------------------------------------------------------
        var outlierReferences = new List<string>();

        for (var i = 0; i < options.ExtremeOutlierCount; i++)
        {
            var category = Categories[i % Categories.Length];
            var outlierReference = NextReference();
            outlierReferences.Add(outlierReference);

            transactions.Add(new ProcurementTransaction
            {
                ExternalReference = outlierReference,
                TransactionDate = RandomDate(),
                // Two orders of magnitude above the category's typical spend.
                // The clean baseline for a category already spans two decades,
                // so an anomaly has to sit well above that whole range to be a
                // genuine outlier rather than just a large ordinary contract.
                Amount = Math.Round(CategoryScale(category) * (1_500m + i * 300m), 2),
                VendorName = Vendors[random.Next(Vendors.Length)],
                Category = category,
                Department = Departments[random.Next(Departments.Length)],
                Description = $"Emergency single-source purchase — {category}"
            });
        }

        planted.Add(new PlantedAnomaly(
            PlantedAnomalyKind.ExtremeOutlier,
            string.Join(", ", outlierReferences),
            $"{options.ExtremeOutlierCount} payments priced far above the entire normal range for " +
            "their spending category, in the guise of emergency single-source purchases.",
            options.ExtremeOutlierCount));

        return new SyntheticDataset(transactions, planted);
    }

    /// <summary>
    /// Draws a Benford-conforming amount: 10^u with u uniform, scaled to the
    /// category's typical order of magnitude. A uniform exponent gives a
    /// uniform mantissa in log space, which is exactly what produces the
    /// log10(1 + 1/d) first-digit law.
    /// </summary>
    private static decimal BenfordAmount(Random random, string category)
    {
        // Two full decades of spread within the category's own scale.
        var magnitude = Math.Pow(10d, random.NextDouble() * 2d);
        return Math.Round(CategoryScale(category) * (decimal)magnitude, 2);
    }

    /// <summary>Typical order of magnitude for a category, so peer groups differ realistically.</summary>
    private static decimal CategoryScale(string category) => category switch
    {
        "Construction" => 8_000_000m,
        "Road Maintenance" => 6_000_000m,
        "Medical Supplies" => 2_500_000m,
        "Vehicle Fleet" => 3_000_000m,
        "IT Equipment" => 1_800_000m,
        "Textbooks" => 900_000m,
        "Catering Services" => 700_000m,
        _ => 400_000m
    };
}
