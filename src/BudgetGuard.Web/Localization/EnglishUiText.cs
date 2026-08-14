namespace BudgetGuard.Web.Localization;

/// <summary>English UI strings. The reference wording the translations follow.</summary>
public sealed class EnglishUiText : IUiText
{
    public string LanguageTag => "en";
    public string LanguageName => "English";

    public string Tagline => "Explainable procurement forensics";
    public string NavUpload => "Upload";
    public string NavReport => "Anomaly report";
    public string NavBenford => "Benford's Law";
    public string NavVendors => "Vendor risk";
    public string NavHowItWorks => "How this works";

    public string FooterDisclaimer =>
        "BudgetGuard AI — statistical forensics for public spending. Findings are screening " +
        "leads for human review, not determinations of wrongdoing.";

    public string SyntheticBanner =>
        "Synthetic demo data. These figures are generated for demonstration and contain " +
        "deliberately planted anomalies. They are not real government procurement data, and no " +
        "real vendor or agency is described here.";

    public string NoDatasetTitle => "No analysis available.";
    public string NoDatasetBody => "No dataset has been uploaded yet.";
    public string LoadDatasetFirst => "Load a dataset first.";
    public string Loading => "Running detection…";
    public string TransactionsAnalysed(int count) => $"{count:N0} transactions analysed";

    public string HeroTitle => "Find the spending that does not add up — and explain why.";

    public string HeroBody =>
        "BudgetGuard AI screens procurement and budget data with three statistical tests auditors " +
        "can check by hand: first-digit conformity (Benford's Law), peer-relative amount outliers, " +
        "and supplier concentration. Every flag comes with the arithmetic behind it, so you can " +
        "verify it rather than trust it.";

    public string DemoTitle => "Load the demo dataset";

    public string DemoBody =>
        "The fastest way to see the tool work. Generates a synthetic procurement ledger with " +
        "deliberately planted anomalies — threshold evasion, a round-number invoicing vendor, a " +
        "supplier capturing one category, and several extreme payments.";

    public string DemoButton => "Load demo dataset";
    public string DemoGenerating => "Generating…";
    public string DemoReady => "Demo dataset ready.";
    public string DemoPlanted => "What was planted in it (ground truth)";

    public string UploadTitle => "Upload your own data";

    public string UploadBody =>
        "CSV or Excel (.xlsx), up to 32 MB. Required columns: TransactionDate, Amount, VendorName, " +
        "Category, Department. Optional: ExternalReference, Currency, Description. Common " +
        "alternative names such as Supplier or Ministry are recognised automatically.";

    public string UploadChooseFile => "Choose a CSV or Excel file";
    public string UploadWorking => "Parsing and analysing…";
    public string UploadFailed => "The file could not be used.";
    public string UploadAccepted(int accepted) => $"Uploaded. {accepted:N0} rows accepted.";
    public string UploadSkipped(int skipped) => $"{skipped:N0} rows skipped.";
    public string UploadWhySkipped => "Why rows were skipped";
    public string StoredDatasets => "Stored datasets";
    public string ColName => "Name";
    public string ColSource => "Source";
    public string ColRows => "Rows";
    public string ColUploaded => "Uploaded";
    public string ActionAnalyse => "Analyse";

    public string ReportTitle => "Anomaly report";
    public string SeverityCritical => "Critical";
    public string SeverityHigh => "High";
    public string SeverityMedium => "Medium";
    public string SeverityLow => "Low";
    public string Corroborated => "Corroborated by 2+ methods";
    public string FilterCategory => "Category";
    public string FilterDepartment => "Department";
    public string FilterSeverity => "Minimum severity";
    public string FilterAllCategories => "All categories";
    public string FilterAllDepartments => "All departments";
    public string FilterAny => "Any";
    public string FilterMediumAbove => "Medium and above";
    public string FilterHighAbove => "High and above";
    public string FilterCriticalOnly => "Critical only";

    public string FilterNote =>
        "Filters narrow what is shown. They never change the population the statistics were " +
        "computed over.";

    public string NoFindings =>
        "No findings match these filters. On a clean dataset that is the expected result — the " +
        "engine is built to stay quiet on spending that behaves normally.";

    public string ColSeverity => "Severity";
    public string ColSubject => "Subject";
    public string ColCategoryDepartment => "Category / Department";
    public string ColAmount => "Amount";
    public string ColRisk => "Risk";
    public string ColWhyFlagged => "Why it was flagged";
    public string MethodsCount(int count) => $"{count} methods";
    public string SubjectTransaction => "Transaction";
    public string SubjectVendor => "Vendor";
    public string SubjectDataset => "Dataset";

    public string BenfordTitle => "Benford's Law — first-digit distribution";
    public string BenfordSubtitle => "Expected versus observed leading digits";
    public string BenfordAmountsTested(int count) => $"{count:N0} amounts tested";
    public string BenfordConforms => "Conforms";
    public string BenfordDoesNotConform => "Does not conform";
    public string BenfordStatistics => "Test statistics";
    public string BenfordMad => "Mean Absolute Deviation";
    public string BenfordThresholdApplied => "Threshold applied";

    public string BenfordThresholdRaised(double noiseFloor) =>
        $"Raised above the published band because at this sample size chance alone produces a " +
        $"deviation of roughly {noiseFloor:F4}.";

    public string BenfordThresholdStandard =>
        "Nigrini's published non-conformity band for first digits.";

    public string BenfordBand => "Conformity band";
    public string BenfordChiSquare => "Chi-square (8 df)";

    public string BenfordChiSquareNote =>
        "Reported for completeness. Not used as the verdict: chi-square grows with sample size and " +
        "rejects conformity on large datasets for deviations too small to matter.";

    public string BenfordExcluded => "Amounts excluded (zero or negative)";
    public string BenfordPerDigit => "Per-digit detail";
    public string ColDigit => "Digit";
    public string ColObserved => "Observed";
    public string ColExpected => "Expected";
    public string ColDeviation => "Deviation";
    public string ColExcess => "Excess count";
    public string BenfordChartExpected => "Expected under Benford's Law";
    public string BenfordChartObserved => "Observed (within expectation)";
    public string BenfordChartOver => "Observed (materially over-represented)";
    public string BenfordLeadingDigit => "Leading digit";
    public string BenfordReadingTitle => "Reading this chart";

    public string BenfordReadingBody =>
        "In data that spans several orders of magnitude and arises from multiplicative processes — " +
        "which procurement spending does — leading digits are not uniform. About 30.1% of natural " +
        "amounts begin with 1 and only 4.6% begin with 9. People inventing numbers do not reproduce " +
        "that curve: fabricated invoices cluster on round figures and on digits just below approval " +
        "ceilings, so a 50,000,000 sign-off limit produces a glut of amounts starting with 4. A " +
        "failure here does not identify a guilty transaction — it says this population was not " +
        "generated by the process that generates honest spending, which is the cue to pull the ledger.";

    public string BenfordMethodologyLink => "Full methodology, including limitations →";

    public string VendorsTitle => "Vendor risk";
    public string VendorsSubtitle => "Supplier share of spend within each category and department";
    public string ScopeConcentrationTitle => "Market concentration by scope";

    public string ScopeConcentrationNote =>
        "The Herfindahl-Hirschman Index is the sum of squared percentage market shares, from near 0 " +
        "(many equal suppliers) to 10,000 (a single supplier). Competition authorities treat above " +
        "2,500 as highly concentrated. A scope scoring badly here is worth reviewing as a procurement " +
        "process even when no individual vendor crosses a threshold.";

    public string ColScope => "Scope";
    public string ColScopeName => "Name";
    public string ColVendors => "Vendors";
    public string ColContracts => "Contracts";
    public string ColTotalSpend => "Total spend";
    public string ColHhi => "HHI";
    public string ColLargestSupplier => "Largest supplier";
    public string EvenSplit => "even split";
    public string OfSpend => "of spend";
    public string FilterScopeType => "Scope type";
    public string FilterAllScopes => "All scopes";
    public string FilterShow => "Show";
    public string FilterAllVendors => "All vendors";
    public string FilterFlaggedOnly => "Flagged vendors only";
    public string SortBy => "Sort by";
    public string SortShare => "Share of scope spend";
    public string SortMultiple => "Multiple of expected share";
    public string SortContracts => "Contract count";
    public string SortSpend => "Absolute spend";

    public string VendorsNote =>
        "Every vendor is listed, not only flagged ones: a share is only interpretable against the " +
        "field it sits in, so the whole distribution has to be visible to judge any one flag.";

    public string ColVendor => "Vendor";
    public string ColSpend => "Spend";
    public string ColShareOfScope => "Share of scope";
    public string ColVsExpected => "vs expected";
    public string ColStatus => "Status";
    public string StatusFlagged => "Flagged";
    public string StatusWithinThresholds => "Within thresholds";
    public string Why => "Why";
    public string EvenSplitWouldBe => "even split would be";
    public string ExpectedShort => "exp.";

    public string NoScopes =>
        "No scope in this dataset had enough vendors and contracts to support a concentration judgement.";

    public string HowItWorksTitle => "How this works";

    public string HowItWorksSubtitle =>
        "The statistical methods behind every flag, the thresholds chosen and why, and where they " +
        "can be wrong.";

    public string MethodologyUnavailable =>
        "The methodology document could not be loaded. It is available in the repository at " +
        "docs/DETECTION_METHODOLOGY.md.";

    public string MethodologyEnglishOnly =>
        "The full methodology document is currently available in English only. The findings " +
        "themselves, including every explanation, are translated.";
}
