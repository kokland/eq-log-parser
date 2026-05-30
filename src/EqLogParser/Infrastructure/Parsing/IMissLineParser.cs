namespace EqLogParser.Infrastructure.Parsing;
/// <summary>Returns the mob name that was missed, or null.</summary>
public interface IMissLineParser   { string? TryParse(string message); }
