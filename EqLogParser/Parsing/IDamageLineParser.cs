using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public interface IDamageLineParser
{
    DamageEvent? TryParse(string message);
}
