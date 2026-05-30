using System.Text.RegularExpressions;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed class XpLineParser
{
    // You gain experience! (4.401%)
    // You gain party experience! (2.094%)
    private static readonly Regex XpRegex = new(
        @"^You gain (?<party>party )?experience! \((?<pct>\d+\.\d+)%\)$",
        RegexOptions.Compiled);

    public XpEvent? TryParse(string message)
    {
        var m = XpRegex.Match(message);
        if (!m.Success) return null;

        var pct     = double.Parse(m.Groups["pct"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var isParty = m.Groups["party"].Success;
        return new XpEvent(0, string.Empty, pct, isParty);
    }
}
