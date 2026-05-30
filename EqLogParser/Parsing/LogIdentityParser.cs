using System.Text.RegularExpressions;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed partial class LogIdentityParser : ILogIdentityParser
{
    public LogIdentity? TryParse(string path)
    {
        var fileName = Path.GetFileName(path);
        var match = FileNameRegex().Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        return new LogIdentity(
            match.Groups["character"].Value,
            match.Groups["server"].Value);
    }

    [GeneratedRegex(
        @"^eqlog_(?<character>[^_]+)_(?<server>.+)\.txt$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FileNameRegex();
}
