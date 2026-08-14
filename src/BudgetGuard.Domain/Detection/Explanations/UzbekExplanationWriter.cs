using System.Globalization;
using BudgetGuard.Domain.Detection.Benford;

namespace BudgetGuard.Domain.Detection.Explanations;

/// <summary>
/// Uzbek (Latin script) explanations.
/// <para>
/// Latin script rather than Cyrillic: it is the official script of Uzbekistan
/// and the one used in current government publications, which is the register
/// this tool's output has to sit alongside.
/// </para>
/// <para>
/// As with the Russian writer, the numeric format is constructed explicitly —
/// space for thousands, comma for decimals — rather than taken from ICU, so a
/// figure quoted in a finding reads the same wherever the analysis ran.
/// </para>
/// </summary>
public sealed class UzbekExplanationWriter : IExplanationWriter
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
    public string LanguageTag => "uz";

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
                "Benford qonuniga to'liq mos keladi — bu tabiiy ravishda shakllangan xarajatlarga xos",
            BenfordConformity.Acceptable =>
                "Benford qonuniga maqbul darajada mos keladi — raqamlar darajasida e'tiroz yo'q",
            BenfordConformity.MarginallyAcceptable =>
                "Benford qonuniga muvofiqlik chegarasidan biroz chetda: ko'rib chiqishga arziydi, " +
                "ammo dalil bo'la olmaydi",
            _ =>
                "Benford qonuniga mos kelmaydi; bu summalar tabiiy yuzaga kelmasdan, sun'iy tarzda " +
                "tuzilgani, yaxlitlangani yoki bo'lib yuborilganiga xos holat"
        };

        var thresholdBasis = c.EffectiveThreshold > c.FixedThreshold
            ? $"{F4(c.EffectiveThreshold)} — bu standart {F3(c.FixedThreshold)} chegaradan " +
              $"yuqoriroq, chunki atigi {N0(c.SampleSize)} ta summadan iborat tanlanma sof tasodif " +
              $"tufayli ham taxminan {F4(c.NoiseFloor)} chetlanish beradi"
            : F4(c.EffectiveThreshold);

        var headline =
            $"{c.PopulationLabel} uchun birinchi raqamlar taqsimoti {verdict}. " +
            $"O'rtacha absolyut chetlanish {F4(c.MeanAbsoluteDeviation)} ni tashkil etadi, " +
            $"mos kelmaslik chegarasi esa {thresholdBasis} ({N0(c.SampleSize)} ta summa bo'yicha hisoblangan).";

        var digitDetail =
            $" Eng katta chetlanish {c.Worst.Digit} raqamiga to'g'ri keladi: u summalarning " +
            $"{Percent(c.Worst.ObservedProportion)} qismida birinchi turadi, Benford qonuni esa " +
            $"{Percent(c.Worst.ExpectedProportion)} ni bashorat qiladi — kutilgan " +
            $"{N0(c.Worst.ExpectedCount)} ta o'rniga {N0(c.Worst.ObservedCount)} ta kuzatildi, " +
            $"ortiqchasi {N0(c.Worst.ExcessCount)} ta.";

        var chiDetail =
            $" Xi-kvadrat {F1(c.ChiSquare)} ni tashkil etadi, 8 erkinlik darajasidagi kritik qiymat " +
            $"{F3(c.ChiSquareCriticalValue)}" +
            (c.ChiSquare > c.ChiSquareCriticalValue
                ? " (u ham muvofiqlikni rad etadi)."
                : " (muvofiqlikni rad etmaydi).");

        return headline + digitDetail + chiDetail;
    }

    /// <inheritdoc />
    public string BenfordInsufficientData(string populationLabel, int sampleSize, int minimumSampleSize) =>
        $"{populationLabel} uchun faqat {N0(sampleSize)} ta yaroqli summa mavjud; birinchi raqamlar " +
        $"bo'yicha statistik jihatdan asosli xulosa uchun kamida {N0(minimumSampleSize)} ta talab " +
        "qilinadi. Xulosa chiqarilmadi.";

    /// <inheritdoc />
    public string Outlier(OutlierExplanationContext c)
    {
        var isLog = c.Transform == AmountTransform.Log10;
        var isClassic = c.Method == OutlierMethod.ClassicZScore;

        var unit = isClassic ? "standart chetlanish" : "modifikatsiyalangan z-ko'rsatkich birligi";
        var direction = c.Score > 0 ? "yuqori" : "past";
        var group = GroupingLabel(c.Grouping);

        var basis = (isClassic, isLog) switch
        {
            (true, true) =>
                $"«{c.GroupKey}» {group} uchun odatdagi to'lovdan (geometrik o'rtacha " +
                $"{N0(c.Centre)} {c.Currency}; bitta standart chetlanish {N1(c.Dispersion)}× " +
                $"ko'paytmaga to'g'ri keladi, {N0(c.GroupSize)} ta to'lov bo'yicha)",

            (true, false) =>
                $"«{c.GroupKey}» {group} bo'yicha o'rtachadan (o'rtacha {N0(c.Centre)}, " +
                $"standart chetlanish {N0(c.Dispersion)}, {N0(c.GroupSize)} ta to'lov bo'yicha)",

            (false, true) =>
                $"«{c.GroupKey}» {group} uchun median to'lovdan (mediana {N0(c.Centre)} {c.Currency}; " +
                $"median absolyut chetlanish {N1(c.Dispersion)}× ko'paytmaga to'g'ri keladi, " +
                $"{N0(c.GroupSize)} ta to'lov bo'yicha)",

            _ =>
                $"«{c.GroupKey}» {group} bo'yicha medianadan (mediana {N0(c.Centre)}, " +
                $"median absolyut chetlanish {N0(c.Dispersion)}, {N0(c.GroupSize)} ta to'lov bo'yicha)"
        };

        var sentence =
            $"«{c.VendorName}» foydasiga {N0(c.Amount)} {c.Currency} miqdoridagi {c.Reference} to'lovi " +
            $"{basis} {F1(Math.Abs(c.Score))} {unit} {direction}. " +
            $"Belgilash chegarasi — {F1(c.Threshold)} {unit}.";

        if (isLog)
        {
            sentence +=
                " Summalar logarifmik shkalada taqqoslanadi: to'lov o'xshash to'lovlardan necha marta " +
                "katta yoki kichik ekani o'lchanadi, necha birlikka emas — kuchli o'ngga qiyshaygan " +
                "xarajatlar uchun aynan shu to'g'ri taqqoslash hisoblanadi.";
        }

        if (!isClassic)
        {
            sentence +=
                " Bu statistika o'rtacha emas, mediana asosida qurilgan, shuning uchun yirik to'lov " +
                "o'zi yuzaga keltirgan tarqoqlik ortiga yashirina olmaydi.";
        }

        return sentence;
    }

    /// <inheritdoc />
    public string Concentration(ConcentrationExplanationContext c)
    {
        var scope = ScopeLabel(c.Scope);

        return
            $"«{c.VendorName}» «{c.ScopeKey}» {scope}sidagi barcha xarajatlarning " +
            $"{Percent(c.SpendShare)} qismini oldi ({N0(c.ScopeTotalSpend)} dan {N0(c.Spend)}), " +
            $"holbuki shu {scope}da raqobatlashayotgan {N0(c.VendorsInScope)} ta yetkazib beruvchi " +
            $"o'rtasida teng taqsimlanganda {Percent(c.ExpectedShare)} kutilar edi — bu teng ulushdan " +
            $"{F1(c.ExcessMultiple)}× ortiq. Bu bitta yirik shartnoma emas: ular " +
            $"{N0(c.ScopeTransactionCount)} ta shartnomadan {N0(c.ContractCount)} tasini " +
            $"({Percent(c.CountShare)}) egallaydi, teng taqsimotda esa ularga taxminan " +
            $"{N1(c.ExpectedContractCount)} ta to'g'ri kelardi; bu {F1(c.ContractCountZScore)} " +
            $"standart chetlanishga teng ortiqchalikdir. Belgilash chegaralari: xarajatlarning " +
            $"{Percent(c.SpendShareThreshold, 0)} qismi yoki teng ulushdan " +
            $"{F1(c.ExpectedShareMultipleThreshold)}× ortiq, shu bilan birga shartnomalar soni " +
            $"tasodifiy darajadan kamida {F1(c.ContractCountZThreshold)} standart chetlanish " +
            "yuqori bo'lishi.";
    }

    /// <inheritdoc />
    public string Scope(ScopeExplanationContext c)
    {
        var noun = c.Scope == ConcentrationScope.Category ? "toifasi" : "idorasi";
        var evenSplitHhi = c.VendorCount == 0 ? 0d : 10_000d / c.VendorCount;

        var verdict = c.Hhi > c.HighlyConcentratedThreshold
            ? $"{N0(c.HighlyConcentratedThreshold)} dan yuqori qiymat AQSh Adliya vazirligi va FTC " +
              "ning gorizontal qo'shilishlar bo'yicha ko'rsatmalariga muvofiq yuqori " +
              "konsentratsiyalangan bozor deb tasniflanadi"
            : $"Bu bozor yuqori konsentratsiyalangan deb tasniflanadigan " +
              $"{N0(c.HighlyConcentratedThreshold)} chegarasidan past";

        return
            $"«{c.ScopeKey}» {noun} uchun Herfindal — Hirshman indeksi {N0(c.VendorCount)} ta " +
            $"yetkazib beruvchi bo'yicha {N0(c.Hhi)} ni tashkil etadi. {verdict}; " +
            $"{N0(c.VendorCount)} ta yetkazib beruvchi o'rtasida teng taqsimotda indeks " +
            $"{N0(evenSplitHhi)} bo'lardi.";
    }

    /// <inheritdoc />
    public string DetectorName(DetectorKind detector) => detector switch
    {
        DetectorKind.Benford => "Benford qonuni",
        DetectorKind.ZScoreOutlier => "G'ayrioddiy summa",
        DetectorKind.VendorConcentration => "Yetkazib beruvchi konsentratsiyasi",
        _ => detector.ToString()
    };

    /// <inheritdoc />
    public string EvidenceLabel(EvidenceKey key) => key switch
    {
        EvidenceKey.SampleSize => "Tanlanma hajmi",
        EvidenceKey.ExcludedAmounts => "Chiqarib tashlandi (nol/manfiy)",
        EvidenceKey.MeanAbsoluteDeviation => "O'rtacha absolyut chetlanish",
        EvidenceKey.ThresholdApplied => "Qo'llanilgan chegara",
        EvidenceKey.Conformity => "Muvofiqlik",
        EvidenceKey.ChiSquare => "Xi-kvadrat",
        EvidenceKey.ChiSquareCriticalValue => "Xi-kvadrat kritik qiymati",
        EvidenceKey.Method => "Usul",
        EvidenceKey.Grouping => "Guruhlash",
        EvidenceKey.PeerGroup => "Taqqoslash guruhi",
        EvidenceKey.PeerGroupSize => "Taqqoslash guruhi hajmi",
        EvidenceKey.TestStatistic => "Statistika qiymati",
        EvidenceKey.Threshold => "Chegara",
        EvidenceKey.GroupCentre => "Guruh markazi",
        EvidenceKey.GroupDispersion => "Guruhdagi tarqoqlik",
        EvidenceKey.Scope => "Qamrov",
        EvidenceKey.VendorSpend => "Yetkazib beruvchi xarajatlari",
        EvidenceKey.ScopeSpend => "Qamrov bo'yicha xarajatlar",
        EvidenceKey.SpendShare => "Xarajatlardagi ulush",
        EvidenceKey.EvenSplitExpectation => "Teng ulushdagi kutilma",
        EvidenceKey.ExcessMultiple => "Ortiqchalik koeffitsiyenti",
        EvidenceKey.ContractsHeld => "Shartnomalar soni",
        EvidenceKey.ContractsExpectedByChance => "Tasodifan kutilgan",
        EvidenceKey.ContractCountExcess => "Shartnomalar soni bo'yicha ortiqchalik",
        EvidenceKey.VendorsInScope => "Qamrovdagi yetkazib beruvchilar",
        _ => key.ToString()
    };

    private static string GroupingLabel(OutlierGrouping grouping) => grouping switch
    {
        OutlierGrouping.Vendor => "yetkazib beruvchisi",
        OutlierGrouping.Category => "toifasi",
        _ => "idorasi"
    };

    private static string ScopeLabel(ConcentrationScope scope) =>
        scope == ConcentrationScope.Category ? "toifa" : "idora";
}
