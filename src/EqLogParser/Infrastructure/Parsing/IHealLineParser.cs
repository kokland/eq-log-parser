using EqLogParser.Core.Domain;
namespace EqLogParser.Infrastructure.Parsing;
public interface IHealLineParser  { HealEvent? TryParse(string message); }
