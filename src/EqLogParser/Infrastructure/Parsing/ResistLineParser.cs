using System.Text.RegularExpressions;

namespace EqLogParser.Infrastructure.Parsing;

public sealed partial class ResistLineParser : IResistLineParser
{
    /// <summary>
    /// Parses "X resisted your SpellName!" lines.
    /// Returns (MobName, SpellName) or null.
    /// </summary>
    public (string MobName, string SpellName)? TryParse(string message)
    {
        var m = MobResistedRegex().Match(message);
        if (!m.Success) return null;
        return (m.Groups["mob"].Value, m.Groups["spell"].Value);
    }

    [GeneratedRegex(
        @"^(?<mob>.+?) resisted your (?<spell>.+)!$",
        RegexOptions.CultureInvariant)]
    private static partial Regex MobResistedRegex();
}
