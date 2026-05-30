using System.Text.RegularExpressions;

namespace EqLogParser.Parsing;

public sealed partial class ZoneLineParser
{
    /// <summary>
    /// Parses zone-change lines: "You have entered ZoneName."
    /// Returns the zone name, or null if the line does not match.
    /// </summary>
    public string? TryParse(string message)
    {
        var m = EnteredZoneRegex().Match(message);
        return m.Success ? m.Groups["zone"].Value : null;
    }

    [GeneratedRegex(
        @"^You have entered (?<zone>.+)\.$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EnteredZoneRegex();
}
