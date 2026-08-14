using BudgetGuard.Domain.Detection.Explanations;

namespace BudgetGuard.Web.Localization;

/// <summary>
/// Every string the UI renders, in one interface per language.
/// <para>
/// A typed interface rather than .resx and <c>IStringLocalizer</c>. The usual
/// resource-file approach looks up by string key and silently renders the key
/// itself when a translation is missing — which ships as an English-looking
/// artefact in the middle of a Russian page and is easy to miss. Here a missing
/// string is a compile error, and the language implementations sit beside the
/// <see cref="IExplanationWriter"/> implementations that translate the detection
/// output, so the whole product's text follows one pattern.
/// </para>
/// </summary>
public interface IUiText
{
    string LanguageTag { get; }

    // Chrome
    string Tagline { get; }
    string NavUpload { get; }
    string NavReport { get; }
    string NavBenford { get; }
    string NavVendors { get; }
    string NavHowItWorks { get; }
    string FooterDisclaimer { get; }
    string LanguageName { get; }

    // Shared
    string SyntheticBanner { get; }
    string NoDatasetTitle { get; }
    string NoDatasetBody { get; }
    string LoadDatasetFirst { get; }
    string Loading { get; }
    string TransactionsAnalysed(int count);

    // Home
    string HeroTitle { get; }
    string HeroBody { get; }
    string DemoTitle { get; }
    string DemoBody { get; }
    string DemoButton { get; }
    string DemoGenerating { get; }
    string DemoReady { get; }
    string DemoPlanted { get; }
    string UploadTitle { get; }
    string UploadBody { get; }
    string UploadChooseFile { get; }
    string UploadWorking { get; }
    string UploadFailed { get; }
    string UploadAccepted(int accepted);
    string UploadSkipped(int skipped);
    string UploadWhySkipped { get; }
    string StoredDatasets { get; }
    string ColName { get; }
    string ColSource { get; }
    string ColRows { get; }
    string ColUploaded { get; }
    string ActionAnalyse { get; }

    // Report
    string ReportTitle { get; }
    string SeverityCritical { get; }
    string SeverityHigh { get; }
    string SeverityMedium { get; }
    string SeverityLow { get; }
    string Corroborated { get; }
    string FilterCategory { get; }
    string FilterDepartment { get; }
    string FilterSeverity { get; }
    string FilterAllCategories { get; }
    string FilterAllDepartments { get; }
    string FilterAny { get; }
    string FilterMediumAbove { get; }
    string FilterHighAbove { get; }
    string FilterCriticalOnly { get; }
    string FilterNote { get; }
    string NoFindings { get; }
    string ColSeverity { get; }
    string ColSubject { get; }
    string ColCategoryDepartment { get; }
    string ColAmount { get; }
    string ColRisk { get; }
    string ColWhyFlagged { get; }
    string MethodsCount(int count);
    string SubjectTransaction { get; }
    string SubjectVendor { get; }
    string SubjectDataset { get; }

    // Benford
    string BenfordTitle { get; }
    string BenfordSubtitle { get; }
    string BenfordAmountsTested(int count);
    string BenfordConforms { get; }
    string BenfordDoesNotConform { get; }
    string BenfordStatistics { get; }
    string BenfordMad { get; }
    string BenfordThresholdApplied { get; }
    string BenfordThresholdRaised(double noiseFloor);
    string BenfordThresholdStandard { get; }
    string BenfordBand { get; }
    string BenfordChiSquare { get; }
    string BenfordChiSquareNote { get; }
    string BenfordExcluded { get; }
    string BenfordPerDigit { get; }
    string ColDigit { get; }
    string ColObserved { get; }
    string ColExpected { get; }
    string ColDeviation { get; }
    string ColExcess { get; }
    string BenfordChartExpected { get; }
    string BenfordChartObserved { get; }
    string BenfordChartOver { get; }
    string BenfordLeadingDigit { get; }
    string BenfordReadingTitle { get; }
    string BenfordReadingBody { get; }
    string BenfordMethodologyLink { get; }

    // Vendor risk
    string VendorsTitle { get; }
    string VendorsSubtitle { get; }
    string ScopeConcentrationTitle { get; }
    string ScopeConcentrationNote { get; }
    string ColScope { get; }
    string ColScopeName { get; }
    string ColVendors { get; }
    string ColContracts { get; }
    string ColTotalSpend { get; }
    string ColHhi { get; }
    string ColLargestSupplier { get; }
    string EvenSplit { get; }
    string OfSpend { get; }
    string FilterScopeType { get; }
    string FilterAllScopes { get; }
    string FilterShow { get; }
    string FilterAllVendors { get; }
    string FilterFlaggedOnly { get; }
    string SortBy { get; }
    string SortShare { get; }
    string SortMultiple { get; }
    string SortContracts { get; }
    string SortSpend { get; }
    string VendorsNote { get; }
    string ColVendor { get; }
    string ColSpend { get; }
    string ColShareOfScope { get; }
    string ColVsExpected { get; }
    string ColStatus { get; }
    string StatusFlagged { get; }
    string StatusWithinThresholds { get; }
    string Why { get; }
    string EvenSplitWouldBe { get; }
    string ExpectedShort { get; }
    string NoScopes { get; }

    // How it works
    string HowItWorksTitle { get; }
    string HowItWorksSubtitle { get; }
    string MethodologyUnavailable { get; }
    string MethodologyEnglishOnly { get; }
}

/// <summary>Selects the UI text for a language, falling back to English.</summary>
public static class UiTexts
{
    public static IUiText For(string? languageTag) =>
        (languageTag ?? string.Empty).Split('-')[0].ToLowerInvariant() switch
        {
            "uz" => new UzbekUiText(),
            "ru" => new RussianUiText(),
            _ => new EnglishUiText()
        };
}
