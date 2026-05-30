using System.Globalization;
using System.Text.RegularExpressions;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed partial class DamageLineParser : IDamageLineParser
{
    public DamageEvent? TryParse(string message)
    {
        return TryParseDirectDamage(message)
            ?? TryParseYourEffectDamage(message)
            ?? TryParseOtherDirectDamage(message);
    }

    private static DamageEvent? TryParseDirectDamage(string message)
    {
        var match = DirectDamageRegex().Match(message);
        if (!match.Success)
        {
            return null;
        }

        return new DamageEvent(
            "You",
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
            "You",
            match.Groups["mob"].Value,
            int.Parse(match.Groups["damage"].Value, CultureInfo.InvariantCulture),
            DamageKind.YourEffect,
            SpellName: match.Groups["spell"].Value);
    }

    private static DamageEvent? TryParseOtherDirectDamage(string message)
    {
        var match = OtherDirectDamageRegex().Match(message);
        if (!match.Success)
        {
            return null;
        }

        // Skip lines where the mob target is "YOU" — those are mobs attacking the player.
        var mob = match.Groups["mob"].Value;
        if (mob.Equals("YOU", StringComparison.Ordinal))
        {
            return null;
        }

        return new DamageEvent(
            match.Groups["source"].Value,
            mob,
            int.Parse(match.Groups["damage"].Value, CultureInfo.InvariantCulture),
            DamageKind.Direct);
    }

    // Matches "You verb mob for N points of [X] damage [by spell][.]"
    [GeneratedRegex(
        @"^You\s+\S+\s+(?<mob>.+?)\s+for\s+(?<damage>\d+)\s+points\s+of\s+(?:(?:\S+)\s+)?damage(?:\s+by\s+.+?)?(?:\.|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex DirectDamageRegex();

    // Matches "mob is ... by YOUR SpellName for N points of [X] damage[.]"
    [GeneratedRegex(
        @"^(?<mob>.+?)\s+is\s+.+?\s+by\s+YOUR\s+(?<spell>.+?)\s+for\s+(?<damage>\d+)\s+points\s+of\s+(?:(?:\S+)\s+)?damage(?:\.|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex YourEffectDamageRegex();

    // Matches "OtherChar verb mob for N points of [X] damage [by spell][.]"
    // Source must be a single word (player/pet names are always one token here).
    [GeneratedRegex(
        @"^(?<source>\S+)\s+\S+\s+(?<mob>.+?)\s+for\s+(?<damage>\d+)\s+points\s+of\s+(?:(?:\S+)\s+)?damage(?:\s+by\s+.+?)?(?:\.|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex OtherDirectDamageRegex();
}
