using System.Text.RegularExpressions;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed class LootLineParser
{
    // --You have looted a Torn Page of Magi`kot pg. 3 from a bloodthirsty ghoul's corpse.--
    private static readonly Regex KeptRegex = new(
        @"^--You have looted (?<item>.+?) from (?<mob>.+?)'s corpse\.--$",
        RegexOptions.Compiled);

    // You looted a Fine Steel Morning Star +1 from a bloodthirsty ghoul's corpse and sold it for ...
    private static readonly Regex SoldRegex = new(
        @"^You looted (?<item>.+?) from (?<mob>.+?)'s corpse and sold it for",
        RegexOptions.Compiled);

    public LootEvent? TryParse(string message)
    {
        var m = KeptRegex.Match(message);
        if (m.Success)
            return new LootEvent(0, string.Empty, m.Groups["item"].Value, m.Groups["mob"].Value, AutoSold: false);

        m = SoldRegex.Match(message);
        if (m.Success)
            return new LootEvent(0, string.Empty, m.Groups["item"].Value, m.Groups["mob"].Value, AutoSold: true);

        return null;
    }
}
