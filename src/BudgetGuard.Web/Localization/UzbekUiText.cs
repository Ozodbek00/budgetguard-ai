namespace BudgetGuard.Web.Localization;

/// <summary>Uzbek (Latin script) UI strings.</summary>
public sealed class UzbekUiText : IUiText
{
    public string LanguageTag => "uz";
    public string LanguageName => "O'zbekcha";

    public string Tagline => "Xaridlar bo'yicha izohlanadigan forenzika";
    public string NavUpload => "Yuklash";
    public string NavReport => "Anomaliyalar hisoboti";
    public string NavBenford => "Benford qonuni";
    public string NavVendors => "Yetkazib beruvchi xavfi";
    public string NavHowItWorks => "Bu qanday ishlaydi";

    public string FooterDisclaimer =>
        "BudgetGuard AI — davlat xarajatlari bo'yicha statistik forenzika. Natijalar inson " +
        "tekshiruvi uchun asos bo'lib, huquqbuzarlik faktini belgilamaydi.";

    public string SyntheticBanner =>
        "Sintetik namoyish ma'lumotlari. Bu raqamlar namoyish uchun yaratilgan va ataylab " +
        "joylashtirilgan anomaliyalarni o'z ichiga oladi. Bu haqiqiy davlat xaridlari ma'lumotlari " +
        "emas; bu yerda hech qanday haqiqiy yetkazib beruvchi yoki idora tasvirlanmagan.";

    public string NoDatasetTitle => "Tahlil mavjud emas.";
    public string NoDatasetBody => "Hali hech qanday ma'lumotlar to'plami yuklanmagan.";
    public string LoadDatasetFirst => "Avval ma'lumotlarni yuklang.";
    public string Loading => "Tahlil bajarilmoqda…";
    public string TransactionsAnalysed(int count) => $"tahlil qilingan operatsiyalar: {count:N0}";

    public string HeroTitle => "Mos kelmaydigan xarajatlarni toping — va sababini tushuntiring.";

    public string HeroBody =>
        "BudgetGuard AI xaridlar va byudjet xarajatlarini auditor qo'lda tekshira oladigan uchta " +
        "statistik usul bilan skanerlaydi: birinchi raqamlarning Benford qonuniga muvofiqligi, " +
        "o'xshash to'lovlarga nisbatan g'ayrioddiy summalar va yetkazib beruvchilar konsentratsiyasi. " +
        "Har bir belgiga uning hisob-kitobi ilova qilinadi, shuning uchun unga ishonish emas, " +
        "uni tekshirish mumkin.";

    public string DemoTitle => "Namoyish ma'lumotlarini yuklash";

    public string DemoBody =>
        "Vositaning ishlashini ko'rishning eng tez yo'li. Ataylab joylashtirilgan anomaliyalarga ega " +
        "sintetik xaridlar reyestri yaratiladi: tasdiqlash chegarasini chetlab o'tish, «yaxlit» " +
        "hisob-fakturali yetkazib beruvchi, bitta toifani egallab olgan yetkazib beruvchi va bir " +
        "nechta o'ta yirik to'lov.";

    public string DemoButton => "Namoyish ma'lumotlarini yuklash";
    public string DemoGenerating => "Yaratilmoqda…";
    public string DemoReady => "Namoyish ma'lumotlari tayyor.";
    public string DemoPlanted => "Unga nima joylashtirilgan (etalon)";

    public string UploadTitle => "O'z ma'lumotlaringizni yuklang";

    public string UploadBody =>
        "CSV yoki Excel (.xlsx), 32 MB gacha. Majburiy ustunlar: TransactionDate, Amount, VendorName, " +
        "Category, Department. Ixtiyoriy: ExternalReference, Currency, Description. Supplier yoki " +
        "Ministry kabi keng tarqalgan muqobil nomlar avtomatik tanib olinadi.";

    public string UploadChooseFile => "CSV yoki Excel faylini tanlang";
    public string UploadWorking => "Tahlil qilinmoqda…";
    public string UploadFailed => "Fayldan foydalanib bo'lmadi.";
    public string UploadAccepted(int accepted) => $"Yuklandi. Qabul qilingan qatorlar: {accepted:N0}.";
    public string UploadSkipped(int skipped) => $"O'tkazib yuborilgan qatorlar: {skipped:N0}.";
    public string UploadWhySkipped => "Qatorlar nima uchun o'tkazib yuborildi";
    public string StoredDatasets => "Saqlangan ma'lumotlar to'plamlari";
    public string ColName => "Nomi";
    public string ColSource => "Manba";
    public string ColRows => "Qatorlar";
    public string ColUploaded => "Yuklangan";
    public string ActionAnalyse => "Tahlil";

    public string ReportTitle => "Anomaliyalar hisoboti";
    public string SeverityCritical => "Kritik";
    public string SeverityHigh => "Yuqori";
    public string SeverityMedium => "O'rta";
    public string SeverityLow => "Past";
    public string Corroborated => "2+ usul bilan tasdiqlangan";
    public string FilterCategory => "Toifa";
    public string FilterDepartment => "Idora";
    public string FilterSeverity => "Minimal daraja";
    public string FilterAllCategories => "Barcha toifalar";
    public string FilterAllDepartments => "Barcha idoralar";
    public string FilterAny => "Har qanday";
    public string FilterMediumAbove => "O'rta va undan yuqori";
    public string FilterHighAbove => "Yuqori va undan yuqori";
    public string FilterCriticalOnly => "Faqat kritik";

    public string FilterNote =>
        "Filtrlar faqat ko'rsatilayotgan qatorlarni cheklaydi. Ular statistika hisoblangan " +
        "to'plamni o'zgartirmaydi.";

    public string NoFindings =>
        "Bu filtrlarga hech narsa mos kelmadi. Toza ma'lumotlarda bu kutilgan natija — mexanizm " +
        "xarajatlar odatdagidek bo'lganda jim turishi uchun mo'ljallangan.";

    public string ColSeverity => "Daraja";
    public string ColSubject => "Obyekt";
    public string ColCategoryDepartment => "Toifa / Idora";
    public string ColAmount => "Summa";
    public string ColRisk => "Xavf";
    public string ColWhyFlagged => "Nima uchun belgilangan";
    public string MethodsCount(int count) => $"usullar: {count}";
    public string SubjectTransaction => "Operatsiya";
    public string SubjectVendor => "Yetkazib beruvchi";
    public string SubjectDataset => "Ma'lumotlar to'plami";

    public string BenfordTitle => "Benford qonuni — birinchi raqamlar taqsimoti";
    public string BenfordSubtitle => "Kutilgan va haqiqiy birinchi raqamlar";
    public string BenfordAmountsTested(int count) => $"tekshirilgan summalar: {count:N0}";
    public string BenfordConforms => "Mos keladi";
    public string BenfordDoesNotConform => "Mos kelmaydi";
    public string BenfordStatistics => "Test statistikalari";
    public string BenfordMad => "O'rtacha absolyut chetlanish";
    public string BenfordThresholdApplied => "Qo'llanilgan chegara";

    public string BenfordThresholdRaised(double noiseFloor) =>
        $"E'lon qilingan chegaradan yuqoriroq, chunki bunday tanlanma hajmida sof tasodifning o'zi " +
        $"taxminan {noiseFloor:F4} chetlanish beradi.";

    public string BenfordThresholdStandard =>
        "Nigrini e'lon qilgan birinchi raqamlar uchun mos kelmaslik chegarasi.";

    public string BenfordBand => "Muvofiqlik oralig'i";
    public string BenfordChiSquare => "Xi-kvadrat (8 e.d.)";

    public string BenfordChiSquareNote =>
        "To'liqlik uchun keltirilgan. Xulosa sifatida ishlatilmaydi: xi-kvadrat tanlanma hajmi bilan " +
        "o'sadi va katta to'plamlarda ahamiyatsiz darajadagi chetlanishlarda ham muvofiqlikni rad etadi.";

    public string BenfordExcluded => "Chiqarib tashlangan summalar (nol yoki manfiy)";
    public string BenfordPerDigit => "Raqamlar bo'yicha tafsilot";
    public string ColDigit => "Raqam";
    public string ColObserved => "Haqiqiy";
    public string ColExpected => "Kutilgan";
    public string ColDeviation => "Chetlanish";
    public string ColExcess => "Ortiqchalik";
    public string BenfordChartExpected => "Benford qonuni bo'yicha kutilgan";
    public string BenfordChartObserved => "Haqiqiy (kutilma doirasida)";
    public string BenfordChartOver => "Haqiqiy (sezilarli darajada ortiq)";
    public string BenfordLeadingDigit => "Birinchi raqam";
    public string BenfordReadingTitle => "Bu grafikni qanday o'qish kerak";

    public string BenfordReadingBody =>
        "Bir necha tartib kattalikni qamrab oladigan va ko'paytiruvchi jarayonlardan kelib chiqadigan " +
        "ma'lumotlarda — xaridlar xarajatlari aynan shunday — birinchi raqamlar bir tekis " +
        "taqsimlanmaydi. Tabiiy summalarning taxminan 30,1% i 1 bilan va atigi 4,6% i 9 bilan " +
        "boshlanadi. Raqamlarni o'ylab topayotgan odamlar bu egri chiziqni takrorlay olmaydi: " +
        "soxta hisob-fakturalar yaxlit qiymatlar va tasdiqlash chegarasidan bir oz pastdagi raqamlar " +
        "atrofida to'planadi, shuning uchun 50 000 000 lik chegara 4 bilan boshlanadigan summalarning " +
        "ortiqchaligini keltirib chiqaradi. Bu yerdagi nomuvofiqlik aniq bir aybdor operatsiyani " +
        "ko'rsatmaydi — u bu to'plam halol xarajatlar yuzaga keladigan tarzda shakllanmaganini " +
        "bildiradi, va bu birlamchi hujjatlarni ko'tarishga asos bo'ladi.";

    public string BenfordMethodologyLink => "To'liq metodologiya, cheklovlar bilan birga →";

    public string VendorsTitle => "Yetkazib beruvchi xavfi";
    public string VendorsSubtitle => "Har bir toifa va idora bo'yicha yetkazib beruvchi ulushi";
    public string ScopeConcentrationTitle => "Qamrovlar bo'yicha bozor konsentratsiyasi";

    public string ScopeConcentrationNote =>
        "Herfindal — Hirshman indeksi — bu foizli bozor ulushlari kvadratlarining yig'indisi, 0 ga " +
        "yaqin qiymatdan (ko'p teng yetkazib beruvchilar) 10 000 gacha (yagona yetkazib beruvchi). " +
        "Raqobat idoralari 2 500 dan yuqori qiymatni yuqori konsentratsiya deb hisoblaydi. Ko'rsatkichi " +
        "yomon qamrovni, hech bir yetkazib beruvchi chegarani oshirmagan bo'lsa ham, xarid jarayoni " +
        "sifatida ko'rib chiqishga arziydi.";

    public string ColScope => "Qamrov";
    public string ColScopeName => "Nomi";
    public string ColVendors => "Yetkazib beruvchilar";
    public string ColContracts => "Shartnomalar";
    public string ColTotalSpend => "Jami xarajat";
    public string ColHhi => "HHI";
    public string ColLargestSupplier => "Eng yirik yetkazib beruvchi";
    public string EvenSplit => "teng ulushda";
    public string OfSpend => "xarajatlardan";
    public string FilterScopeType => "Qamrov turi";
    public string FilterAllScopes => "Barcha qamrovlar";
    public string FilterShow => "Ko'rsatish";
    public string FilterAllVendors => "Barcha yetkazib beruvchilar";
    public string FilterFlaggedOnly => "Faqat belgilanganlar";
    public string SortBy => "Saralash";
    public string SortShare => "Qamrov xarajatlaridagi ulush";
    public string SortMultiple => "Kutilgan ulushga nisbatan koeffitsiyent";
    public string SortContracts => "Shartnomalar soni";
    public string SortSpend => "Mutlaq xarajat";

    public string VendorsNote =>
        "Faqat belgilanganlar emas, barcha yetkazib beruvchilar ko'rsatilgan: ulushni faqat " +
        "boshqalar fonida baholash mumkin, shuning uchun har qanday belgini baholash uchun butun " +
        "taqsimotni ko'rish kerak.";

    public string ColVendor => "Yetkazib beruvchi";
    public string ColSpend => "Xarajat";
    public string ColShareOfScope => "Qamrovdagi ulush";
    public string ColVsExpected => "kutilmaga nisbatan";
    public string ColStatus => "Holat";
    public string StatusFlagged => "Belgilangan";
    public string StatusWithinThresholds => "Chegaralar doirasida";
    public string Why => "Nima uchun";
    public string EvenSplitWouldBe => "teng ulushda bo'lardi";
    public string ExpectedShort => "kut.";

    public string NoScopes =>
        "Bu ma'lumotlar to'plamidagi hech bir qamrovda konsentratsiya haqida xulosa chiqarish uchun " +
        "yetarli yetkazib beruvchi va shartnoma to'planmadi.";

    public string HowItWorksTitle => "Bu qanday ishlaydi";

    public string HowItWorksSubtitle =>
        "Har bir belgi ortidagi statistik usullar, tanlangan chegaralar va ularning asoslari, " +
        "shuningdek usullar xato qilishi mumkin bo'lgan holatlar.";

    public string MethodologyUnavailable =>
        "Metodologiya hujjatini yuklab bo'lmadi. U repozitoriyda mavjud: docs/DETECTION_METHODOLOGY.md.";

    public string MethodologyEnglishOnly =>
        "To'liq metodologiya hujjati hozircha faqat ingliz tilida mavjud. Tahlil natijalari, " +
        "shu jumladan barcha izohlar, tarjima qilingan.";
}
