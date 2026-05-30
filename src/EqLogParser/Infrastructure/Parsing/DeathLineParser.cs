using System.Text.RegularExpressions;
using EqLogParser.Core.Domain;

namespace EqLogParser.Infrastructure.Parsing;

public sealed partial class DeathLineParser : IDeathLineParser
{
    /// <summary>
    /// Parses player-death lines: "You have been slain by X!"
    /// Returns the killer name, or null if the line does not match.
    /// </summary>
    public string? TryParse(string message)
    {
        var m = YouSlainByRegex().Match(message);
        return m.Success ? m.Groups["killer"].Value : null;
    }

    [GeneratedRegex(
        @"^You have been slain by (?<killer>.+)!$",
        RegexOptions.CultureInvariant)]
    private static partial Regex YouSlainByRegex();
}
