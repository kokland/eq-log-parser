using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public interface IEqDamageParser
{
    DamageSummary Parse(string path);
}
