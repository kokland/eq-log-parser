using System.Text.RegularExpressions;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed partial class KillLineParser : IKillLineParser
{
    public KillEvent? TryParse(string message)
    {
        var youMatch = YouSlainRegex().Match(message);
        if (youMatch.Success)
        {
            return new KillEvent(youMatch.Groups["mob"].Value, "You");
        }

        var slainMatch = SlainByRegex().Match(message);
        if (slainMatch.Success)
        {
            return new KillEvent(
                slainMatch.Groups["mob"].Value,
                slainMatch.Groups["killer"].Value);
        }

        var struckDownMatch = StruckDownRegex().Match(message);
        if (struckDownMatch.Success)
        {
            return new KillEvent(
                struckDownMatch.Groups["mob"].Value,
                struckDownMatch.Groups["killer"].Value);
        }

        return null;
    }

    [GeneratedRegex(
        @"^You have slain (?<mob>.+)!$",
        RegexOptions.CultureInvariant)]
    private static partial Regex YouSlainRegex();

    [GeneratedRegex(
        @"^(?<mob>.+?) has been slain by (?<killer>.+)!$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SlainByRegex();

    [GeneratedRegex(
        @"^(?<mob>.+?) has been struck down by (?<killer>.+)\.$",
        RegexOptions.CultureInvariant)]
    private static partial Regex StruckDownRegex();
}
