using System.Globalization;
using System.Text.RegularExpressions;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed partial class HealLineParser : IHealLineParser
{
    /// <summary>
    /// Parses YOU-sourced heal lines:
    ///   "You healed Target for N (M) hit points by SpellName."
    ///   "You healed Target for N hit points by SpellName."
    /// Returns a HealEvent with Amount = actual healed value (first number), or null.
    /// </summary>
    public HealEvent? TryParse(string message)
    {
        var m = YouHealedRegex().Match(message);
        if (!m.Success) return null;

        return new HealEvent(
            LineNumber: 0,
            Timestamp:  string.Empty,
            Target:     m.Groups["target"].Value,
            Amount:     int.Parse(m.Groups["amount"].Value, CultureInfo.InvariantCulture),
            SpellName:  m.Groups["spell"].Value);
    }

    // "You healed Target for N (M) hit points by SpellName."  or  "...for N hit points by SpellName."
    [GeneratedRegex(
        @"^You healed (?<target>.+?) for (?<amount>\d+)(?: \(\d+\))? hit points by (?<spell>.+)\.$",
        RegexOptions.CultureInvariant)]
    private static partial Regex YouHealedRegex();
}
