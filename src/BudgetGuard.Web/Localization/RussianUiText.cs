namespace BudgetGuard.Web.Localization;

/// <summary>Russian UI strings.</summary>
public sealed class RussianUiText : IUiText
{
    public string LanguageTag => "ru";
    public string LanguageName => "Русский";

    public string Tagline => "Объяснимая форензика госзакупок";
    public string NavUpload => "Загрузка";
    public string NavReport => "Отчёт об аномалиях";
    public string NavBenford => "Закон Бенфорда";
    public string NavVendors => "Риски поставщиков";
    public string NavHowItWorks => "Как это работает";

    public string FooterDisclaimer =>
        "BudgetGuard AI — статистическая форензика государственных расходов. Результаты являются " +
        "поводом для проверки человеком, а не установлением факта нарушения.";

    public string SyntheticBanner =>
        "Синтетические демонстрационные данные. Эти цифры сгенерированы для демонстрации и содержат " +
        "намеренно заложенные аномалии. Это не реальные данные о государственных закупках; ни один " +
        "реальный поставщик или ведомство здесь не описан.";

    public string NoDatasetTitle => "Анализ недоступен.";
    public string NoDatasetBody => "Ни один набор данных ещё не загружен.";
    public string LoadDatasetFirst => "Сначала загрузите данные.";
    public string Loading => "Выполняется анализ…";
    public string TransactionsAnalysed(int count) => $"проанализировано операций: {count:N0}";

    public string HeroTitle => "Найдите расходы, которые не сходятся, — и объясните почему.";

    public string HeroBody =>
        "BudgetGuard AI проверяет данные о закупках и бюджетных расходах тремя статистическими " +
        "методами, которые аудитор может перепроверить вручную: соответствие первых цифр закону " +
        "Бенфорда, аномальные суммы относительно сопоставимых, и концентрация поставщиков. " +
        "К каждому сигналу прилагаются расчёты, поэтому его можно проверить, а не принимать на веру.";

    public string DemoTitle => "Загрузить демонстрационные данные";

    public string DemoBody =>
        "Самый быстрый способ увидеть работу инструмента. Создаётся синтетический реестр закупок с " +
        "намеренно заложенными аномалиями: обход порога согласования, поставщик с «круглыми» счетами, " +
        "поставщик, захвативший категорию, и несколько экстремальных платежей.";

    public string DemoButton => "Загрузить демо-данные";
    public string DemoGenerating => "Генерация…";
    public string DemoReady => "Демо-данные готовы.";
    public string DemoPlanted => "Что в них заложено (эталон)";

    public string UploadTitle => "Загрузить свои данные";

    public string UploadBody =>
        "CSV или Excel (.xlsx), до 32 МБ. Обязательные столбцы: TransactionDate, Amount, VendorName, " +
        "Category, Department. Необязательные: ExternalReference, Currency, Description. " +
        "Распространённые альтернативные названия, например Supplier или Ministry, распознаются автоматически.";

    public string UploadChooseFile => "Выберите файл CSV или Excel";
    public string UploadWorking => "Разбор и анализ…";
    public string UploadFailed => "Файл не удалось использовать.";
    public string UploadAccepted(int accepted) => $"Загружено. Принято строк: {accepted:N0}.";
    public string UploadSkipped(int skipped) => $"Пропущено строк: {skipped:N0}.";
    public string UploadWhySkipped => "Почему строки пропущены";
    public string StoredDatasets => "Сохранённые наборы данных";
    public string ColName => "Название";
    public string ColSource => "Источник";
    public string ColRows => "Строк";
    public string ColUploaded => "Загружено";
    public string ActionAnalyse => "Анализ";

    public string ReportTitle => "Отчёт об аномалиях";
    public string SeverityCritical => "Критично";
    public string SeverityHigh => "Высокий";
    public string SeverityMedium => "Средний";
    public string SeverityLow => "Низкий";
    public string Corroborated => "Подтверждено 2+ методами";
    public string FilterCategory => "Категория";
    public string FilterDepartment => "Ведомство";
    public string FilterSeverity => "Минимальный уровень";
    public string FilterAllCategories => "Все категории";
    public string FilterAllDepartments => "Все ведомства";
    public string FilterAny => "Любой";
    public string FilterMediumAbove => "Средний и выше";
    public string FilterHighAbove => "Высокий и выше";
    public string FilterCriticalOnly => "Только критичные";

    public string FilterNote =>
        "Фильтры ограничивают только отображаемые строки. Они не меняют совокупность, по которой " +
        "рассчитаны статистики.";

    public string NoFindings =>
        "Под эти фильтры ничего не подпадает. На чистых данных это ожидаемый результат — движок " +
        "рассчитан на то, чтобы молчать, когда расходы ведут себя нормально.";

    public string ColSeverity => "Уровень";
    public string ColSubject => "Объект";
    public string ColCategoryDepartment => "Категория / Ведомство";
    public string ColAmount => "Сумма";
    public string ColRisk => "Риск";
    public string ColWhyFlagged => "Почему отмечено";
    public string MethodsCount(int count) => $"методов: {count}";
    public string SubjectTransaction => "Операция";
    public string SubjectVendor => "Поставщик";
    public string SubjectDataset => "Набор данных";

    public string BenfordTitle => "Закон Бенфорда — распределение первых цифр";
    public string BenfordSubtitle => "Ожидаемые и фактические первые цифры";
    public string BenfordAmountsTested(int count) => $"проверено сумм: {count:N0}";
    public string BenfordConforms => "Соответствует";
    public string BenfordDoesNotConform => "Не соответствует";
    public string BenfordStatistics => "Статистики теста";
    public string BenfordMad => "Среднее абсолютное отклонение";
    public string BenfordThresholdApplied => "Применённый порог";

    public string BenfordThresholdRaised(double noiseFloor) =>
        $"Повышен относительно опубликованного порога, поскольку при таком размере выборки одна лишь " +
        $"случайность даёт отклонение около {noiseFloor:F4}.";

    public string BenfordThresholdStandard =>
        "Опубликованный Нигрини порог несоответствия для первых цифр.";

    public string BenfordBand => "Полоса соответствия";
    public string BenfordChiSquare => "Хи-квадрат (8 ст. св.)";

    public string BenfordChiSquareNote =>
        "Приводится для полноты. Не используется как вердикт: хи-квадрат растёт с размером выборки и " +
        "на больших наборах отвергает соответствие при отклонениях, слишком малых, чтобы иметь значение.";

    public string BenfordExcluded => "Исключено сумм (ноль или отрицательные)";
    public string BenfordPerDigit => "Детализация по цифрам";
    public string ColDigit => "Цифра";
    public string ColObserved => "Факт";
    public string ColExpected => "Ожидание";
    public string ColDeviation => "Отклонение";
    public string ColExcess => "Превышение";
    public string BenfordChartExpected => "Ожидается по закону Бенфорда";
    public string BenfordChartObserved => "Фактически (в пределах ожидания)";
    public string BenfordChartOver => "Фактически (существенно завышено)";
    public string BenfordLeadingDigit => "Первая цифра";
    public string BenfordReadingTitle => "Как читать этот график";

    public string BenfordReadingBody =>
        "В данных, охватывающих несколько порядков величины и возникающих из мультипликативных " +
        "процессов — а расходы на закупки именно такие — первые цифры распределены неравномерно. " +
        "Около 30,1% естественных сумм начинаются с 1 и лишь 4,6% — с 9. Люди, придумывающие числа, " +
        "эту кривую не воспроизводят: сфабрикованные счета группируются вокруг круглых значений и " +
        "цифр чуть ниже порогов согласования, поэтому лимит в 50 000 000 порождает избыток сумм, " +
        "начинающихся с 4. Несоответствие здесь не указывает на конкретную виновную операцию — оно " +
        "говорит, что эта совокупность возникла не так, как возникают честные расходы, и это повод " +
        "поднять первичные документы.";

    public string BenfordMethodologyLink => "Полная методология, включая ограничения →";

    public string VendorsTitle => "Риски поставщиков";
    public string VendorsSubtitle => "Доля поставщика в расходах по каждой категории и ведомству";
    public string ScopeConcentrationTitle => "Концентрация рынка по областям";

    public string ScopeConcentrationNote =>
        "Индекс Херфиндаля — Хиршмана — это сумма квадратов процентных долей рынка, от почти 0 " +
        "(много равных поставщиков) до 10 000 (единственный поставщик). Антимонопольные органы " +
        "считают значения выше 2 500 признаком высокой концентрации. Область с плохим показателем " +
        "стоит проверить как закупочный процесс, даже если ни один отдельный поставщик не превысил порог.";

    public string ColScope => "Область";
    public string ColScopeName => "Название";
    public string ColVendors => "Поставщиков";
    public string ColContracts => "Контрактов";
    public string ColTotalSpend => "Всего расходов";
    public string ColHhi => "ИХХ";
    public string ColLargestSupplier => "Крупнейший поставщик";
    public string EvenSplit => "при равной доле";
    public string OfSpend => "расходов";
    public string FilterScopeType => "Тип области";
    public string FilterAllScopes => "Все области";
    public string FilterShow => "Показать";
    public string FilterAllVendors => "Всех поставщиков";
    public string FilterFlaggedOnly => "Только отмеченных";
    public string SortBy => "Сортировка";
    public string SortShare => "Доля в расходах области";
    public string SortMultiple => "Кратность ожидаемой доли";
    public string SortContracts => "Число контрактов";
    public string SortSpend => "Абсолютные расходы";

    public string VendorsNote =>
        "Показаны все поставщики, а не только отмеченные: долю можно оценить лишь на фоне остальных, " +
        "поэтому для суждения о любом сигнале нужно видеть всё распределение.";

    public string ColVendor => "Поставщик";
    public string ColSpend => "Расходы";
    public string ColShareOfScope => "Доля в области";
    public string ColVsExpected => "к ожиданию";
    public string ColStatus => "Статус";
    public string StatusFlagged => "Отмечен";
    public string StatusWithinThresholds => "В пределах порогов";
    public string Why => "Почему";
    public string EvenSplitWouldBe => "при равной доле было бы";
    public string ExpectedShort => "ожид.";

    public string NoScopes =>
        "Ни в одной области этого набора данных не набралось достаточно поставщиков и контрактов, " +
        "чтобы судить о концентрации.";

    public string HowItWorksTitle => "Как это работает";

    public string HowItWorksSubtitle =>
        "Статистические методы, стоящие за каждым сигналом, выбранные пороги и обоснование, " +
        "а также случаи, когда методы могут ошибаться.";

    public string MethodologyUnavailable =>
        "Не удалось загрузить документ с методологией. Он доступен в репозитории: " +
        "docs/DETECTION_METHODOLOGY.md.";

    public string MethodologyEnglishOnly =>
        "Полный документ с методологией пока доступен только на английском языке. Сами результаты " +
        "анализа, включая все объяснения, переведены.";
}
