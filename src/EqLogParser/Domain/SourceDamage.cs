namespace EqLogParser.Domain;

public sealed class SourceDamage(string source)
{
    private readonly Dictionary<string, long> _bySpell = new(StringComparer.OrdinalIgnoreCase);
    private List<(string Spell, long Damage)>? _bySpellSorted;

    public string Source { get; } = source;
    public long DirectDamage { get; private set; }
    public long EffectDamage { get; private set; }
    public long TotalDamage => DirectDamage + EffectDamage;
    public long Hits { get; private set; }

    /// <summary>Spell → damage for YOUR-effect hits that carry a spell name, sorted descending by damage. Cached.</summary>
    public IReadOnlyList<(string Spell, long Damage)> BySpell =>
        _bySpellSorted ??= _bySpell
            .OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

    public void Add(int damage, DamageKind kind, string? spellName = null)
    {
        Hits++;
        _bySpellSorted = null; // invalidate cache

        if (kind == DamageKind.Direct)
        {
            DirectDamage += damage;
        }
        else
        {
            EffectDamage += damage;
            if (spellName is not null)
            {
                _bySpell.TryGetValue(spellName, out var prev);
                _bySpell[spellName] = prev + damage;
            }
        }
    }

    public SourceDamageSnapshot Snapshot() =>
        new(Source, DirectDamage, EffectDamage, TotalDamage, Hits, BySpell);
}
