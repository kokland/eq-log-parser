using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public interface ILogIdentityParser
{
    LogIdentity? TryParse(string path);
}
