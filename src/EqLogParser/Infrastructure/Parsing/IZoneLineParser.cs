namespace EqLogParser.Infrastructure.Parsing;
/// <summary>Returns the zone name or null.</summary>
public interface IZoneLineParser  { string? TryParse(string message); }
