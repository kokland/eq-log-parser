using System.Text.RegularExpressions;

namespace EqLogParser.Parsing;

public sealed partial class MissLineParser
{
    /// <summary>
    /// Parses "You try to verb X, but miss!" lines.
    /// Returns the mob name, or null.
    /// </summary>
    public string? TryParse(string message)
    {
        var m = YouMissRegex().Match(message);
        return m.Success ? m.Groups["mob"].Value : null;
    }

    [GeneratedRegex(
        @"^You try to \S+ (?<mob>.+), but miss!$",
        RegexOptions.CultureInvariant)]
    private static partial Regex YouMissRegex();
}
