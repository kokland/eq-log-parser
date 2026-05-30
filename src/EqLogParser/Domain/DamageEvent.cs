namespace EqLogParser.Domain;

public sealed record DamageEvent(string Source, string MobName, int Amount, DamageKind Kind);
