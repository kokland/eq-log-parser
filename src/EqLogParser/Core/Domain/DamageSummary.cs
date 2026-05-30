namespace EqLogParser.Core.Domain;

public sealed record DamageSummary(
    IReadOnlyList<MobDamage>     Mobs,
    IReadOnlyList<KillSummary>   Kills,
    IReadOnlyList<MobDamage>     OpenEncounters,
    IReadOnlyList<LootSummary>   Loot,
    IReadOnlyList<XpEvent>       Xp,
    IReadOnlyList<SessionSummary> Sessions,
    IReadOnlyList<DeathEvent>    Deaths,
    IReadOnlyList<ZoneEvent>     Zones,
    IReadOnlyList<HealEvent>     Heals,
    long TotalDamage,
    long TotalHits,
    long TotalHealing);
