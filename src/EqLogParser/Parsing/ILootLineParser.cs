using EqLogParser.Domain;
namespace EqLogParser.Parsing;
public interface ILootLineParser  { LootEvent?  TryParse(string message); }
