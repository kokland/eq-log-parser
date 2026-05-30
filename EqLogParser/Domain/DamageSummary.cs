namespace EqLogParser.Domain;

public sealed record DamageSummary(
    IReadOnlyList<MobDamage> Mobs,
    IReadOnlyList<KillSummary> Kills,
    IReadOnlyList<MobDamage> OpenEncounters,
    long TotalDamage,
    long TotalHits);
