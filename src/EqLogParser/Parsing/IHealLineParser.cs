using EqLogParser.Domain;
namespace EqLogParser.Parsing;
public interface IHealLineParser  { HealEvent? TryParse(string message); }
