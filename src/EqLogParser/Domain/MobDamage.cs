namespace EqLogParser.Domain;

public sealed class MobDamage(string name)
{
    private readonly Dictionary<string, SourceDamage> _bySource = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; } = name;
    public long DirectDamage { get; private set; }
    public long YourEffectDamage { get; private set; }
    public long TotalDamage => DirectDamage + YourEffectDamage;
    public long Hits { get; private set; }

    /// <summary>Per-character damage contribution, sorted descending by total damage.</summary>
    public IReadOnlyList<SourceDamage> BySource =>
        _bySource.Values.OrderByDescending(s => s.TotalDamage).ToList();

    public void Add(string source, int damage, DamageKind kind)
    {
        Hits++;

        if (kind == DamageKind.Direct)
            DirectDamage += damage;
        else
            YourEffectDamage += damage;

        if (!_bySource.TryGetValue(source, out var sd))
            _bySource[source] = sd = new SourceDamage(source);

        sd.Add(damage, kind);
    }
}
