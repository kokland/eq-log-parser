using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public interface IKillLineParser
{
    KillEvent? TryParse(string message);
}
