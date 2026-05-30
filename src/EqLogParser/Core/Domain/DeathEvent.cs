namespace EqLogParser.Core.Domain;

public sealed record DeathEvent(int LineNumber, string Timestamp, string KilledBy);
