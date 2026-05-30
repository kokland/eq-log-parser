namespace EqLogParser.Domain;

public sealed class SourceDamage(string source)
{
    private readonly Dictionary<string, long> _bySpell = new(StringComparer.OrdinalIgnoreCase);

    public string Source { get; } = source;
    public long DirectDamage { get; private set; }
    public long EffectDamage { get; private set; }
    public long TotalDamage => DirectDamage + EffectDamage;
    public long Hits { get; private set; }

    /// <summary>
    /// Spell-name → total damage; only populated for YOUR-effect hits that carry a spell name.
    /// Sorted descending by damage on access.
    /// </summary>
    public IReadOnlyList<(string Spell, long Damage)> BySpell =>
        _bySpell.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value)).ToList();

    public void Add(int damage, DamageKind kind, string? spellName = null)
    {
        Hits++;
        if (kind == DamageKind.Direct)
            DirectDamage += damage;
        else
        {
            EffectDamage += damage;
            if (spellName is not null)
            {
                _bySpell.TryGetValue(spellName, out var existing);
                _bySpell[spellName] = existing + damage;
            }
        }
    }
}
