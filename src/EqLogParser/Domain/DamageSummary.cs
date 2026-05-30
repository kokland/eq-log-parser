namespace EqLogParser.Domain;

public sealed record DamageSummary(
    IReadOnlyList<MobDamage> Mobs,
    IReadOnlyList<KillSummary> Kills,
    IReadOnlyList<MobDamage> OpenEncounters,
    IReadOnlyList<LootSummary> Loot,
    long TotalDamage,
    long TotalHits);
