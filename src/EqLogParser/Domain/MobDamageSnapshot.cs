namespace EqLogParser.Domain;

/// <summary>
/// Immutable snapshot of a <see cref="MobDamage"/> taken at the moment a kill is recorded.
/// Prevents callers from mutating encounter state through a KillSummary reference.
/// </summary>
public sealed record MobDamageSnapshot(
    string Name,
    long DirectDamage,
    long YourEffectDamage,
    long TotalDamage,
    long Hits,
    int Resists,
    int Misses,
    DateTime? FirstHitTime,
    IReadOnlyList<SourceDamageSnapshot> BySource);
