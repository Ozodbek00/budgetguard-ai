using System.Globalization;
using System.Text;
using BudgetGuard.Domain.Demo;
using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Benford;
using BudgetGuard.Domain.Detection.Concentration;
using BudgetGuard.Domain.Detection.Explanations;
using BudgetGuard.Domain.Detection.Outliers;

// Small utility with two jobs:
//   csv       write the synthetic demo dataset to a CSV file
//   diagnose  run the detection pipeline over it and print the ranked findings
//
// `diagnose` exists so detector tuning can be inspected directly rather than
// only through test assertions — when a threshold changes, this shows what
// moved and why.

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "csv";
var generator = new SyntheticDatasetGenerator();
var dataset = generator.Generate();

switch (command)
{
    case "csv":
        WriteCsv(args.Length > 1 ? args[1] : "demo-procurement-data.csv");
        break;

    case "diagnose":
        Diagnose();
        break;

    case "lang":
        ShowTranslations();
        break;

    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Use 'csv [path]' or 'diagnose'.");
        return 1;
}

return 0;

void WriteCsv(string path)
{
    var builder = new StringBuilder();
    builder.AppendLine("ExternalReference,TransactionDate,Amount,Currency,VendorName,Category,Department,Description");

    foreach (var t in dataset.Transactions)
    {
        builder.AppendLine(string.Join(',',
            Quote(t.ExternalReference),
            t.TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            t.Amount.ToString(CultureInfo.InvariantCulture),
            Quote(t.Currency),
            Quote(t.VendorName),
            Quote(t.Category),
            Quote(t.Department),
            Quote(t.Description)));
    }

    File.WriteAllText(path, builder.ToString(), Encoding.UTF8);

    Console.WriteLine($"Wrote {dataset.Transactions.Count:N0} synthetic transactions to {path}");
    Console.WriteLine();
    Console.WriteLine("SYNTHETIC DATA — planted anomalies (ground truth):");

    foreach (var planted in dataset.PlantedAnomalies)
    {
        Console.WriteLine($"  [{planted.Kind}] {planted.Description}");
    }
}

static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

void Diagnose()
{
    var settings = new DetectionSettings();

    var aggregator = new AnomalyAggregator(
        new BenfordAnalyzer(settings.Benford),
        new ZScoreOutlierDetector(settings.ZScore),
        new VendorConcentrationAnalyzer(settings.VendorConcentration),
        settings);

    var report = aggregator.Analyze(dataset.Transactions, "Demo dataset");

    Console.WriteLine($"Transactions      : {report.TransactionCount:N0}");
    Console.WriteLine($"Findings          : {report.Findings.Count:N0}");
    Console.WriteLine($"Actionable        : {report.ActionableCount:N0}");
    Console.WriteLine($"Corroborated      : {report.CorroboratedCount:N0}");
    Console.WriteLine($"Dataset Benford   : {report.DatasetBenford.Conformity} " +
                      $"(MAD {report.DatasetBenford.MeanAbsoluteDeviation:F4}, " +
                      $"threshold {report.DatasetBenford.EffectiveMadThreshold:F4})");
    Console.WriteLine();

    Console.WriteLine("Planted (ground truth):");
    foreach (var planted in dataset.PlantedAnomalies)
    {
        Console.WriteLine($"  [{planted.Kind}] {planted.SubjectKey}");
    }

    Console.WriteLine();
    Console.WriteLine("Top vendor findings:");

    foreach (var finding in report.Findings
                 .Where(f => f.SubjectType == AnomalySubjectType.Vendor)
                 .Take(12))
    {
        var detectors = string.Join('+', finding.CorroboratingDetectors);
        Console.WriteLine($"  {finding.RiskScore:F3} {finding.Severity,-8} {finding.SubjectLabel,-28} [{detectors}]");

        foreach (var signal in finding.Signals)
        {
            Console.WriteLine($"        {signal.Detector} {signal.Score:F3}: {Truncate(signal.Explanation, 150)}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Top transaction findings:");

    foreach (var finding in report.Findings
                 .Where(f => f.SubjectType == AnomalySubjectType.Transaction)
                 .Take(10))
    {
        Console.WriteLine($"  {finding.RiskScore:F3} {finding.Severity,-8} {finding.SubjectLabel}");
    }
}

static string Truncate(string value, int max) =>
    value.Length <= max ? value : value[..max] + "...";

// Prints the same finding in every supported language, side by side. Exists so
// the translations can be read and checked by a person — a native speaker
// reviewing wording should not have to run the web app to see the output.
void ShowTranslations()
{
    var settings = new DetectionSettings();

    foreach (var tag in ExplanationWriters.SupportedLanguages)
    {
        var writer = ExplanationWriters.For(tag);

        var aggregator = new AnomalyAggregator(
            new BenfordAnalyzer(settings.Benford, writer),
            new ZScoreOutlierDetector(settings.ZScore, writer),
            new VendorConcentrationAnalyzer(settings.VendorConcentration, writer),
            settings,
            writer);

        var report = aggregator.Analyze(dataset.Transactions, "Demo dataset");

        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"  {tag.ToUpperInvariant()}");
        Console.WriteLine(new string('=', 78));

        Console.WriteLine();
        Console.WriteLine("-- dataset Benford --");
        Console.WriteLine(report.DatasetBenford.Explanation);

        var vendor = report.Findings.FirstOrDefault(f => f.SubjectType == AnomalySubjectType.Vendor);
        if (vendor is not null)
        {
            Console.WriteLine();
            Console.WriteLine("-- vendor concentration --");
            Console.WriteLine(vendor.Signals[0].Explanation);
        }

        var transaction = report.Findings.FirstOrDefault(f => f.SubjectType == AnomalySubjectType.Transaction);
        if (transaction is not null)
        {
            Console.WriteLine();
            Console.WriteLine("-- amount outlier --");
            Console.WriteLine(transaction.Signals[0].Explanation);
        }

        var scope = report.Scopes.FirstOrDefault();
        if (scope is not null)
        {
            Console.WriteLine();
            Console.WriteLine("-- scope concentration --");
            Console.WriteLine(scope.Explanation);
        }

        Console.WriteLine();
    }
}
