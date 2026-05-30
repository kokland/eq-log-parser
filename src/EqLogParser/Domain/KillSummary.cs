namespace EqLogParser.Domain;

public sealed record KillSummary(
    int LineNumber,
    string Timestamp,
    MobDamage Mob,
    string KilledBy);
