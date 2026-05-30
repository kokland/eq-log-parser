using EqLogParser.Core.Domain;

namespace EqLogParser.Core;

public interface IEqDamageParser
{
    DamageSummary Parse(string path);
}
