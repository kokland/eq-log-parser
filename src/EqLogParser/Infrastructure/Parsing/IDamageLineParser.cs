using EqLogParser.Core.Domain;

namespace EqLogParser.Infrastructure.Parsing;

public interface IDamageLineParser
{
    DamageEvent? TryParse(string message);
}
