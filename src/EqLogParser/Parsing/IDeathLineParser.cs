namespace EqLogParser.Parsing;
/// <summary>Returns the killer name or null.</summary>
public interface IDeathLineParser { string? TryParse(string message); }
