namespace EqLogParser.Domain;

public sealed record KillSummary(
    int LineNumber,
    string Timestamp,
    MobDamageSnapshot Mob,
    string KilledBy,
    double? Dps = null);
