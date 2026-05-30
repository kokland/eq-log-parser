using System.Globalization;
using System.Text.RegularExpressions;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed partial class DamageLineParser : IDamageLineParser
{
    public DamageEvent? TryParse(string message)
    {
        return TryParseDirectDamage(message) ?? TryParseYourEffectDamage(message);
    }

    private static DamageEvent? TryParseDirectDamage(string message)
    {
        var match = DirectDamageRegex().Match(message);
        if (!match.Success)
        {
            return null;
        }

        return new DamageEvent(
            match.Groups["mob"].Value,
            int.Parse(match.Groups["damage"].Value, CultureInfo.InvariantCulture),
            DamageKind.Direct);
    }

    private static DamageEvent? TryParseYourEffectDamage(string message)
    {
        var match = YourEffectDamageRegex().Match(message);
        if (!match.Success)
        {
            return null;
        }

        return new DamageEvent(
            match.Groups["mob"].Value,
            int.Parse(match.Groups["damage"].Value, CultureInfo.InvariantCulture),
            DamageKind.YourEffect);
    }

    [GeneratedRegex(
        @"^You\s+\S+\s+(?<mob>.+?)\s+for\s+(?<damage>\d+)\s+points\s+of\s+(?:(?:\S+)\s+)?damage(?:\s+by\s+.+?)?(?:\.|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex DirectDamageRegex();

    [GeneratedRegex(
        @"^(?<mob>.+?)\s+is\s+.+?\s+by\s+YOUR\s+.+?\s+for\s+(?<damage>\d+)\s+points\s+of\s+(?:(?:\S+)\s+)?damage(?:\.|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex YourEffectDamageRegex();
}
