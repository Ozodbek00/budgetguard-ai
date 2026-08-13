using System.Globalization;

namespace BudgetGuard.Domain.Detection;

/// <summary>
/// Formatting helpers for explanation text.
/// <para>
/// The invariant culture renders "P1" as "50.0 %", with a space. Explanations
/// are read by auditors and quoted into reports, so shares are formatted
/// explicitly here rather than left to a culture setting that differs between
/// a developer machine and a container running with
/// <c>InvariantGlobalization</c> enabled.
/// </para>
/// </summary>
internal static class DetectionFormat
{
    /// <summary>Formats a 0-1 proportion as a percentage string, e.g. 0.342 becomes "34.2%".</summary>
    /// <param name="proportion">Value in the range 0-1.</param>
    /// <param name="decimals">Decimal places to show.</param>
    internal static string Percent(double proportion, int decimals = 1) =>
        (proportion * 100d).ToString($"F{decimals}", CultureInfo.InvariantCulture) + "%";
}
