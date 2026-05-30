namespace EqLogParser.Core.Domain;

public sealed record ZoneEvent(int LineNumber, string Timestamp, string ZoneName);
