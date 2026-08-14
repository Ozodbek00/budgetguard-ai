using System.Globalization;
using BudgetGuard.Domain.Detection.Benford;

namespace BudgetGuard.Domain.Detection.Explanations;

/// <summary>
/// Russian explanations.
/// <para>
/// Number formatting uses an explicitly constructed
/// <see cref="NumberFormatInfo"/> — space for thousands, comma for decimals —
/// rather than <c>CultureInfo("ru-RU")</c>. ICU data varies between images and
/// has changed separator characters between versions; pinning the format here
/// means a figure quoted in a report is identical wherever the analysis ran,
/// which matters when the sentence is meant to be checked by hand.
/// </para>
/// </summary>
public sealed class RussianExplanationWriter : IExplanationWriter
{
    /// <summary>
    /// Non-breaking space (U+00A0): the correct grouping character for both
    /// Russian and Uzbek, and it stops a figure being split across a line
    /// break mid-number. Written as an escape because a literal is
    /// indistinguishable from a plain space in source — the two writers
    /// silently disagreeing on which one they used was a real bug here,
    /// caught only because one test normalised whitespace and another did not.
    /// </summary>
    private const string GroupSeparator = "\u00A0";

    private static readonly NumberFormatInfo Format = new()
    {
        NumberGroupSeparator = GroupSeparator,
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3]
    };

    /// <inheritdoc />
    public string LanguageTag => "ru";

    private static string N0(decimal v) => v.ToString("N0", Format);
    private static string N0(double v) => v.ToString("N0", Format);
    private static string N0(int v) => v.ToString("N0", Format);
    private static string N1(decimal v) => v.ToString("N1", Format);
    private static string N1(double v) => v.ToString("N1", Format);
    private static string F1(double v) => v.ToString("F1", Format);
    private static string F3(double v) => v.ToString("F3", Format);
    private static string F4(double v) => v.ToString("F4", Format);

    private static string Percent(double proportion, int decimals = 1) =>
        (proportion * 100d).ToString($"F{decimals}", Format) + "%";

    /// <inheritdoc />
    public string Benford(BenfordExplanationContext c)
    {
        var verdict = c.Conformity switch
        {
            BenfordConformity.Close =>
                "точно следует закону Бенфорда, что характерно для естественно возникающих расходов",
            BenfordConformity.Acceptable =>
                "приемлемо следует закону Бенфорда — на уровне цифр замечаний нет",
            BenfordConformity.MarginallyAcceptable =>
                "незначительно выходит за пределы соответствия закону Бенфорда: заслуживает проверки, " +
                "но доказательством не является",
            _ =>
                "не следует закону Бенфорда, что характерно для сумм, которые были сконструированы, " +
                "округлены или раздроблены, а не возникли естественным образом"
        };

        var thresholdBasis = c.EffectiveThreshold > c.FixedThreshold
            ? $"{F4(c.EffectiveThreshold)} — он повышен относительно стандартного порога " +
              $"{F3(c.FixedThreshold)}, поскольку выборка всего из {N0(c.SampleSize)} сумм даёт " +
              $"отклонение около {F4(c.NoiseFloor)} по чистой случайности"
            : F4(c.EffectiveThreshold);

        var headline =
            $"Распределение первых цифр для {c.PopulationLabel} {verdict}. " +
            $"Среднее абсолютное отклонение составляет {F4(c.MeanAbsoluteDeviation)} " +
            $"при пороге несоответствия {thresholdBasis} (рассчитано по {N0(c.SampleSize)} суммам).";

        var digitDetail =
            $" Наибольшее отклонение приходится на цифру {c.Worst.Digit}: она стоит первой в " +
            $"{Percent(c.Worst.ObservedProportion)} сумм, тогда как закон Бенфорда предсказывает " +
            $"{Percent(c.Worst.ExpectedProportion)} — наблюдается {N0(c.Worst.ObservedCount)} против " +
            $"ожидаемых {N0(c.Worst.ExpectedCount)}, превышение составляет {N0(c.Worst.ExcessCount)}.";

        var chiDetail =
            $" Критерий хи-квадрат равен {F1(c.ChiSquare)} при критическом значении " +
            $"{F3(c.ChiSquareCriticalValue)} с 8 степенями свободы" +
            (c.ChiSquare > c.ChiSquareCriticalValue
                ? " (также отвергает соответствие)."
                : " (соответствие не отвергает).");

        return headline + digitDetail + chiDetail;
    }

    /// <inheritdoc />
    public string BenfordInsufficientData(string populationLabel, int sampleSize, int minimumSampleSize) =>
        $"Для {populationLabel} доступно всего {N0(sampleSize)} пригодных сумм; для статистически " +
        $"значимого вывода по первым цифрам требуется не менее {N0(minimumSampleSize)}. " +
        "Заключение не выносится.";

    /// <inheritdoc />
    public string Outlier(OutlierExplanationContext c)
    {
        var isLog = c.Transform == AmountTransform.Log10;
        var isClassic = c.Method == OutlierMethod.ClassicZScore;

        // Phrased as "exceeds X by N units" so the numeral agrees with a
        // decimal value, which a direct translation of the English word order
        // would not.
        var unit = isClassic ? "стандартных отклонения" : "единицы модифицированного z-показателя";
        var relation = c.Score > 0 ? "превышает" : "ниже";
        var group = GroupingLabel(c.Grouping);

        var basis = (isClassic, isLog) switch
        {
            (true, true) =>
                $"типичный платёж по {group} «{c.GroupKey}» " +
                $"(среднее геометрическое {N0(c.Centre)} {c.Currency}; одно стандартное отклонение " +
                $"соответствует множителю {N1(c.Dispersion)}×, по {N0(c.GroupSize)} платежам)",

            (true, false) =>
                $"среднее по {group} «{c.GroupKey}» " +
                $"(среднее {N0(c.Centre)}, стандартное отклонение {N0(c.Dispersion)} " +
                $"по {N0(c.GroupSize)} платежам)",

            (false, true) =>
                $"медианный платёж по {group} «{c.GroupKey}» " +
                $"(медиана {N0(c.Centre)} {c.Currency}; медианное абсолютное отклонение соответствует " +
                $"множителю {N1(c.Dispersion)}×, по {N0(c.GroupSize)} платежам)",

            _ =>
                $"медиану по {group} «{c.GroupKey}» " +
                $"(медиана {N0(c.Centre)}, медианное абсолютное отклонение {N0(c.Dispersion)} " +
                $"по {N0(c.GroupSize)} платежам)"
        };

        var sentence =
            $"Платёж {c.Reference} на сумму {N0(c.Amount)} {c.Currency} в адрес «{c.VendorName}» " +
            $"{relation} {basis} на {F1(Math.Abs(c.Score))} {unit}. " +
            $"Порог срабатывания — {F1(c.Threshold)} {unit}.";

        if (isLog)
        {
            sentence +=
                " Суммы сравниваются в логарифмической шкале: измеряется, во сколько раз платёж больше " +
                "или меньше сопоставимых, а не на сколько единиц — это корректное сравнение для расходов " +
                "с сильной правосторонней асимметрией.";
        }

        if (!isClassic)
        {
            sentence +=
                " Статистика построена на медиане, а не на среднем, поэтому крупный платёж не может " +
                "скрыться за разбросом, который сам же и создаёт.";
        }

        return sentence;
    }

    /// <inheritdoc />
    public string Concentration(ConcentrationExplanationContext c)
    {
        var scope = ScopeLabel(c.Scope);
        var scopeIn = c.Scope == ConcentrationScope.Category ? "в этой категории" : "в этом ведомстве";

        return
            $"«{c.VendorName}» получил {Percent(c.SpendShare)} всех расходов по {scope} " +
            $"«{c.ScopeKey}» ({N0(c.Spend)} из {N0(c.ScopeTotalSpend)}), тогда как при равном " +
            $"распределении между {N0(c.VendorsInScope)} поставщиками, конкурирующими {scopeIn}, " +
            $"ожидалось бы {Percent(c.ExpectedShare)} — превышение в {F1(c.ExcessMultiple)}× " +
            $"относительно равной доли. Это не единичный крупный контракт: поставщик держит " +
            $"{N0(c.ContractCount)} из {N0(c.ScopeTransactionCount)} контрактов " +
            $"({Percent(c.CountShare)}), тогда как при равномерном распределении ему пришлось бы " +
            $"около {N1(c.ExpectedContractCount)}, что даёт превышение в " +
            $"{F1(c.ContractCountZScore)} стандартных отклонения. Пороги срабатывания: " +
            $"{Percent(c.SpendShareThreshold, 0)} расходов либо " +
            $"{F1(c.ExpectedShareMultipleThreshold)}× от равной доли, вместе с числом контрактов " +
            $"не менее чем на {F1(c.ContractCountZThreshold)} стандартных отклонения выше случайного.";
    }

    /// <inheritdoc />
    public string Scope(ScopeExplanationContext c)
    {
        var noun = c.Scope == ConcentrationScope.Category ? "Категория" : "Ведомство";
        var evenSplitHhi = c.VendorCount == 0 ? 0d : 10_000d / c.VendorCount;

        var verdict = c.Hhi > c.HighlyConcentratedThreshold
            ? $"Значение выше {N0(c.HighlyConcentratedThreshold)} классифицируется как " +
              "высококонцентрированный рынок согласно рекомендациям Минюста и FTC США по " +
              "горизонтальным слияниям"
            : $"Это ниже порога {N0(c.HighlyConcentratedThreshold)}, при котором рынок " +
              "классифицируется как высококонцентрированный";

        return
            $"{noun} «{c.ScopeKey}»: индекс Херфиндаля — Хиршмана составляет {N0(c.Hhi)} " +
            $"при {N0(c.VendorCount)} поставщиках. {verdict}; при равном распределении между " +
            $"{N0(c.VendorCount)} поставщиками индекс составил бы {N0(evenSplitHhi)}.";
    }

    /// <inheritdoc />
    public string DetectorName(DetectorKind detector) => detector switch
    {
        DetectorKind.Benford => "Закон Бенфорда",
        DetectorKind.ZScoreOutlier => "Аномальная сумма",
        DetectorKind.VendorConcentration => "Концентрация поставщика",
        _ => detector.ToString()
    };

    /// <inheritdoc />
    public string EvidenceLabel(EvidenceKey key) => key switch
    {
        EvidenceKey.SampleSize => "Размер выборки",
        EvidenceKey.ExcludedAmounts => "Исключено (ноль/отрицательные)",
        EvidenceKey.MeanAbsoluteDeviation => "Среднее абсолютное отклонение",
        EvidenceKey.ThresholdApplied => "Применённый порог",
        EvidenceKey.Conformity => "Соответствие",
        EvidenceKey.ChiSquare => "Хи-квадрат",
        EvidenceKey.ChiSquareCriticalValue => "Критическое значение хи-квадрат",
        EvidenceKey.Method => "Метод",
        EvidenceKey.Grouping => "Группировка",
        EvidenceKey.PeerGroup => "Группа сравнения",
        EvidenceKey.PeerGroupSize => "Размер группы сравнения",
        EvidenceKey.TestStatistic => "Значение статистики",
        EvidenceKey.Threshold => "Порог",
        EvidenceKey.GroupCentre => "Центр группы",
        EvidenceKey.GroupDispersion => "Разброс в группе",
        EvidenceKey.Scope => "Область",
        EvidenceKey.VendorSpend => "Расходы на поставщика",
        EvidenceKey.ScopeSpend => "Расходы по области",
        EvidenceKey.SpendShare => "Доля расходов",
        EvidenceKey.EvenSplitExpectation => "Ожидание при равной доле",
        EvidenceKey.ExcessMultiple => "Кратность превышения",
        EvidenceKey.ContractsHeld => "Контрактов у поставщика",
        EvidenceKey.ContractsExpectedByChance => "Ожидаемо случайно",
        EvidenceKey.ContractCountExcess => "Превышение по числу контрактов",
        EvidenceKey.VendorsInScope => "Поставщиков в области",
        _ => key.ToString()
    };

    private static string GroupingLabel(OutlierGrouping grouping) => grouping switch
    {
        OutlierGrouping.Vendor => "поставщику",
        OutlierGrouping.Category => "категории",
        _ => "ведомству"
    };

    private static string ScopeLabel(ConcentrationScope scope) =>
        scope == ConcentrationScope.Category ? "категории" : "ведомству";
}
