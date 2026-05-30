namespace EqLogParser.Domain;

public sealed record DamageEvent(string MobName, int Amount, DamageKind Kind);
