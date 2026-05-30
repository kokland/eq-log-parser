using EqLogParser.Core.Domain;
namespace EqLogParser.Infrastructure.Parsing;
public interface IXpLineParser    { XpEvent?   TryParse(string message); }
