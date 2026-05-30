using EqLogParser.Core.Domain;

namespace EqLogParser.Infrastructure.Parsing;

public interface ILogIdentityParser
{
    LogIdentity? TryParse(string path);
}
