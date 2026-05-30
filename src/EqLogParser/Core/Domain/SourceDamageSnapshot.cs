namespace EqLogParser.Core.Domain;

/// <summary>
/// Immutable snapshot of a <see cref="SourceDamage"/> taken at the moment a kill is recorded.
/// </summary>
public sealed record SourceDamageSnapshot(
    string Source,
    long DirectDamage,
    long EffectDamage,
    long TotalDamage,
    long Hits,
    IReadOnlyList<(string Spell, long Damage)> BySpell);
