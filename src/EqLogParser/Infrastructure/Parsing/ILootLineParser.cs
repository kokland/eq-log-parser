using EqLogParser.Core.Domain;
namespace EqLogParser.Infrastructure.Parsing;
public interface ILootLineParser  { LootEvent?  TryParse(string message); }
