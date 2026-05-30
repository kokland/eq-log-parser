namespace EqLogParser.Core.Domain;

public sealed record DamageEvent(string Source, string MobName, int Amount, DamageKind Kind, string? SpellName = null);

