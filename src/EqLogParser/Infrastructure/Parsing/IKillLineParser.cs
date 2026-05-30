using EqLogParser.Core.Domain;

namespace EqLogParser.Infrastructure.Parsing;

public interface IKillLineParser
{
    KillEvent? TryParse(string message);
}
