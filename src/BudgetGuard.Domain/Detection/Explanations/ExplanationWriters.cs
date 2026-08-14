namespace BudgetGuard.Domain.Detection.Explanations;

/// <summary>Selects the explanation writer for a language.</summary>
public static class ExplanationWriters
{
    /// <summary>Languages the detection output is available in, most-preferred first.</summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } = ["en", "uz", "ru"];

    /// <summary>
    /// Returns the writer for a BCP-47 language tag, falling back to English.
    /// <para>
    /// Matches on the primary subtag, so "uz-Latn-UZ", "uz-Cyrl" and "uz" all
    /// resolve. Uzbek is written in Latin script here regardless of the tag's
    /// script subtag, because that is the script used in current Uzbek
    /// government publications, which is the register this output sits beside.
    /// </para>
    /// <para>
    /// An unknown language falls back to English rather than throwing: a
    /// mistyped <c>Accept-Language</c> header should give a reader a report they
    /// can still act on, not an error page.
    /// </para>
    /// </summary>
    /// <param name="languageTag">A BCP-47 tag such as "ru", "uz-Latn-UZ", or null.</param>
    public static IExplanationWriter For(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return new EnglishExplanationWriter();
        }

        var primary = languageTag.Split('-')[0].ToLowerInvariant();

        return primary switch
        {
            "uz" => new UzbekExplanationWriter(),
            "ru" => new RussianExplanationWriter(),
            _ => new EnglishExplanationWriter()
        };
    }

    /// <summary>True when a language tag maps to a translation rather than the English fallback.</summary>
    public static bool IsSupported(string? languageTag) =>
        !string.IsNullOrWhiteSpace(languageTag) &&
        SupportedLanguages.Contains(languageTag.Split('-')[0].ToLowerInvariant());
}
