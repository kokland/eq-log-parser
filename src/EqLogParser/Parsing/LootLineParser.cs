using System.Text.RegularExpressions;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed class LootLineParser : ILootLineParser
{
    // --You have looted a Torn Page of Magi`kot pg. 3 from a bloodthirsty ghoul's corpse.--
    private static readonly Regex KeptRegex = new(
        @"^--You have looted (?<item>.+?) from (?<mob>.+?)'s corpse\.--$",
        RegexOptions.Compiled);

    // Catches all "You looted <item> from <mob>'s corpse ..." variants:
    //   ... and sold it for <price>
    //   ... and sold it for free.
    //   ... to create a <upgraded item>
    //   (no suffix — plain kept)
    private static readonly Regex YouLootedRegex = new(
        @"^You looted (?<item>.+?) from (?<mob>.+?)'s corpse",
        RegexOptions.Compiled);

    public LootEvent? TryParse(string message)
    {
        var m = KeptRegex.Match(message);
        if (m.Success)
            return new LootEvent(0, string.Empty, m.Groups["item"].Value, m.Groups["mob"].Value, AutoSold: false);

        m = YouLootedRegex.Match(message);
        if (m.Success)
        {
            var autoSold = message.Contains("and sold it for", StringComparison.Ordinal);
            return new LootEvent(0, string.Empty, m.Groups["item"].Value, m.Groups["mob"].Value, autoSold);
        }

        return null;
    }
}
