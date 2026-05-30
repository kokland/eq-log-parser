using EqLogParser.Domain;
namespace EqLogParser.Parsing;
public interface IXpLineParser    { XpEvent?   TryParse(string message); }
