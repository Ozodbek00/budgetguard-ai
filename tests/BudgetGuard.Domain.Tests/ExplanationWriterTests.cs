using System.Globalization;
using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Benford;
using BudgetGuard.Domain.Detection.Concentration;
using BudgetGuard.Domain.Detection.Explanations;
using BudgetGuard.Domain.Detection.Outliers;

namespace BudgetGuard.Domain.Tests;

/// <summary>
/// The explanations are the product, so their translations are tested like
/// product output rather than treated as cosmetic strings.
/// <para>
/// These tests do not check that a translation is good — no test can — but they
/// do check the things that silently break a localised build: a language
/// falling back to English without saying so, numbers losing their formatting,
/// and figures disagreeing between languages when they must be identical.
/// </para>
/// </summary>
public sealed class ExplanationWriterTests
{
    public static TheoryData<IExplanationWriter> AllWriters =>
    [
        new EnglishExplanationWriter(),
        new UzbekExplanationWriter(),
        new RussianExplanationWriter()
    ];

    private static ConcentrationExplanationContext Concentration() =>
        new("Alfa Qurilish Invest", ConcentrationScope.Category, "Construction",
            16_446_676_399m, 52_946_940_210m, 0.3106, 0.0526, 5.9,
            34, 227, 0.1498, 19, 11.9, 6.6, 0.30, 3.0, 2.0);

    private static OutlierExplanationContext Outlier() =>
        new("BG-01705", 12_000_000_000m, "UZS", "Jizzax Injiniring",
            OutlierGrouping.Category, "Construction", 233,
            OutlierMethod.ClassicZScore, AmountTransform.Log10,
            4.3, 3.0, 24_500_000m, 3.2m);

    private static BenfordExplanationContext Benford(BenfordConformity conformity) =>
        new("Demo dataset", 1710, 0.0219, 0.0150, 0.0150, 0.0057, 84.3, 15.507,
            conformity,
            new BenfordDigitBucket(4, 395, 0.2135, 179.2, 0.0969));

    // -----------------------------------------------------------------
    // Language selection
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("en", "en")]
    [InlineData("uz", "uz")]
    [InlineData("ru", "ru")]
    [InlineData("uz-Latn-UZ", "uz")]
    [InlineData("uz-Cyrl", "uz")]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-GB", "en")]
    public void Language_tags_resolve_on_the_primary_subtag(string tag, string expected) =>
        Assert.Equal(expected, ExplanationWriters.For(tag).LanguageTag);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("not-a-language")]
    public void Unknown_languages_fall_back_to_English_rather_than_throwing(string? tag)
    {
        // A mistyped Accept-Language header should still produce a usable
        // report, not an error page.
        Assert.Equal("en", ExplanationWriters.For(tag).LanguageTag);
    }

    [Fact]
    public void Supported_languages_are_the_three_shipped_writers()
    {
        Assert.Equal(["en", "uz", "ru"], ExplanationWriters.SupportedLanguages);
        Assert.True(ExplanationWriters.IsSupported("uz-Latn-UZ"));
        Assert.False(ExplanationWriters.IsSupported("fr"));
    }

    // -----------------------------------------------------------------
    // Every writer produces complete, distinct output
    // -----------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllWriters))]
    public void Every_writer_returns_non_empty_text_for_every_sentence(IExplanationWriter writer)
    {
        Assert.False(string.IsNullOrWhiteSpace(writer.Benford(Benford(BenfordConformity.NonConformant))));
        Assert.False(string.IsNullOrWhiteSpace(writer.BenfordInsufficientData("vendor \"X\"", 12, 50)));
        Assert.False(string.IsNullOrWhiteSpace(writer.Outlier(Outlier())));
        Assert.False(string.IsNullOrWhiteSpace(writer.Concentration(Concentration())));
        Assert.False(string.IsNullOrWhiteSpace(writer.Scope(
            new ScopeExplanationContext(ConcentrationScope.Category, "Construction", 19, 3120, 2500))));
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public void Every_writer_labels_every_detector_and_evidence_key(IExplanationWriter writer)
    {
        foreach (var detector in Enum.GetValues<DetectorKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(writer.DetectorName(detector)));
        }

        foreach (var key in Enum.GetValues<EvidenceKey>())
        {
            Assert.False(string.IsNullOrWhiteSpace(writer.EvidenceLabel(key)));
        }
    }

    [Theory]
    [InlineData("uz")]
    [InlineData("ru")]
    public void Translated_writers_have_no_untranslated_labels(string tag)
    {
        // A missing switch case falls through to the raw enum name, which looks
        // plausible enough in a UI to ship unnoticed. Comparing against English
        // catches it; comparing against the enum name would not, because some
        // English labels are legitimately identical to it ("Method", "Scope").
        var translated = ExplanationWriters.For(tag);
        var english = new EnglishExplanationWriter();

        foreach (var detector in Enum.GetValues<DetectorKind>())
        {
            Assert.NotEqual(english.DetectorName(detector), translated.DetectorName(detector));
        }

        foreach (var key in Enum.GetValues<EvidenceKey>())
        {
            Assert.NotEqual(english.EvidenceLabel(key), translated.EvidenceLabel(key));
        }
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public void Every_conformity_band_has_its_own_wording(IExplanationWriter writer)
    {
        var bands = Enum.GetValues<BenfordConformity>()
            .Where(b => b != BenfordConformity.InsufficientData)
            .Select(b => writer.Benford(Benford(b)))
            .ToArray();

        Assert.Equal(bands.Length, bands.Distinct().Count());
    }

    [Fact]
    public void Translations_are_actually_different_from_English()
    {
        var english = new EnglishExplanationWriter().Concentration(Concentration());
        var uzbek = new UzbekExplanationWriter().Concentration(Concentration());
        var russian = new RussianExplanationWriter().Concentration(Concentration());

        Assert.NotEqual(english, uzbek);
        Assert.NotEqual(english, russian);
        Assert.NotEqual(uzbek, russian);
    }

    // -----------------------------------------------------------------
    // The figures must survive translation unchanged
    // -----------------------------------------------------------------

    [Fact]
    public void Uzbek_and_Russian_use_the_same_group_separator()
    {
        // These drifted apart once — one file held a plain space and the other a
        // non-breaking space, so identical-looking source produced different
        // output. Nothing but a test catches an invisible character.
        var uzbek = new UzbekExplanationWriter().Concentration(Concentration());
        var russian = new RussianExplanationWriter().Concentration(Concentration());

        Assert.Contains("16 446 676 399", uzbek);
        Assert.Contains("16 446 676 399", russian);
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public void Every_language_quotes_the_same_underlying_figures(IExplanationWriter writer)
    {
        var text = writer.Concentration(Concentration());

        // Percentages, the vendor, the scope and the contract counts must be
        // present in every language: an auditor reading the Uzbek report has to
        // be able to check exactly what the English reader checks.
        Assert.Contains("Alfa Qurilish Invest", text);
        Assert.Contains("Construction", text);
        Assert.Contains("31,1%", text.Replace("31.1%", "31,1%"));
        Assert.Contains("34", text);
        Assert.Contains("227", text);
    }

    [Fact]
    public void English_formats_numbers_with_comma_groups_and_a_decimal_point()
    {
        var text = new EnglishExplanationWriter().Concentration(Concentration());

        Assert.Contains("16,446,676,399", text);
        Assert.Contains("31.1%", text);
    }

    [Theory]
    [InlineData("uz")]
    [InlineData("ru")]
    public void Uzbek_and_Russian_format_numbers_with_space_groups_and_a_decimal_comma(string tag)
    {
        var text = ExplanationWriters.For(tag).Concentration(Concentration());

        // Whitespace is normalised before matching: the assertion is about the
        // grouping of the digits, and a line-wrapping difference in the sentence
        // around them should not fail it. The full text goes in the message so a
        // real formatting regression is diagnosable from the failure alone.
        var normalised = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

        Assert.True(normalised.Contains("16 446 676 399"), $"Grouping missing in {tag}: {text}");
        Assert.True(normalised.Contains("31,1%"), $"Decimal comma missing in {tag}: {text}");
    }

    [Theory]
    [MemberData(nameof(AllWriters))]
    public void Number_formatting_does_not_depend_on_the_ambient_culture(IExplanationWriter writer)
    {
        // Guards the reason each writer carries its own NumberFormatInfo: a
        // figure quoted in a finding must read identically wherever the
        // analysis ran, and the test suite must not pass or fail by locale.
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var underGerman = writer.Concentration(Concentration());

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var underInvariant = writer.Concentration(Concentration());

            Assert.Equal(underInvariant, underGerman);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // -----------------------------------------------------------------
    // Wiring: a detector given a writer uses it
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("uz")]
    [InlineData("ru")]
    public void Detectors_write_their_findings_in_the_language_they_were_given(string tag)
    {
        var writer = ExplanationWriters.For(tag);
        var settings = new DetectionSettings();

        var aggregator = new AnomalyAggregator(
            new BenfordAnalyzer(settings.Benford, writer),
            new ZScoreOutlierDetector(settings.ZScore, writer),
            new VendorConcentrationAnalyzer(settings.VendorConcentration, writer),
            settings,
            writer);

        var transactions = new List<Entities.ProcurementTransaction>();
        transactions.AddRange(Enumerable.Range(0, 20)
            .Select(_ => TestData.Transaction(25_000m, vendor: "Target")));
        transactions.AddRange(Enumerable.Range(0, 5)
            .Select(_ => TestData.Transaction(50_000m, vendor: "Other A")));
        transactions.AddRange(Enumerable.Range(0, 5)
            .Select(_ => TestData.Transaction(50_000m, vendor: "Other B")));

        var report = aggregator.Analyze(transactions, "Test");
        var finding = report.Findings.Single(f => f.SubjectKey == "Target");

        // Space-grouped numbers are the cheapest reliable signal that the
        // localised writer produced this sentence rather than the English one.
        // Scope spend is 1,000,000 — 20 x 25,000 plus 10 x 50,000. Whitespace is
        // normalised because the separator is a non-breaking space by design.
        var normalised = System.Text.RegularExpressions.Regex.Replace(
            finding.PrimaryExplanation, @"\s+", " ");

        Assert.True(normalised.Contains("1 000 000"),
            $"Expected localised grouping in {tag}: {finding.PrimaryExplanation}");
        Assert.DoesNotContain("even-split expectation", finding.PrimaryExplanation);
    }

    [Fact]
    public void A_detector_given_no_writer_still_produces_English()
    {
        var settings = new DetectionSettings();
        var result = new BenfordAnalyzer(settings.Benford)
            .Analyze(TestData.UniformDigitAmounts(200), "dataset");

        Assert.Contains("Benford's Law", result.Explanation);
    }
}
