namespace EqLogParser.Core.Domain;

public sealed class MobDamage(string name)
{
    private readonly Dictionary<string, SourceDamage> _bySource = new(StringComparer.OrdinalIgnoreCase);
    private List<SourceDamage>? _bySourceSorted;

    public string Name { get; } = name;
    public long DirectDamage { get; private set; }
    public long YourEffectDamage { get; private set; }
    public long TotalDamage => DirectDamage + YourEffectDamage;
    public long Hits { get; private set; }
    public int Resists { get; private set; }
    public int Misses { get; private set; }

    /// <summary>Parsed timestamp of the first damage hit on this mob.</summary>
    public DateTime? FirstHitTime { get; private set; }

    /// <summary>Per-source damage sorted descending by total. Cached until the next Add call.</summary>
    public IReadOnlyList<SourceDamage> BySource =>
        _bySourceSorted ??= _bySource.Values.OrderByDescending(s => s.TotalDamage).ToList();

    public void Add(string source, int damage, DamageKind kind, DateTime? timestamp = null, string? spellName = null)
    {
        Hits++;
        _bySourceSorted = null;

        FirstHitTime ??= timestamp;

        if (kind == DamageKind.Direct)
            DirectDamage += damage;
        else
            YourEffectDamage += damage;

        if (!_bySource.TryGetValue(source, out var sd))
            _bySource[source] = sd = new SourceDamage(source);

        sd.Add(damage, kind, spellName);
    }

    public void AddResist() => Resists++;
    public void AddMiss()   => Misses++;

    /// <summary>Creates an immutable snapshot of the current state.</summary>
    public MobDamageSnapshot Snapshot() =>
        new(Name, DirectDamage, YourEffectDamage, TotalDamage, Hits, Resists, Misses,
            FirstHitTime,
            BySource.Select(s => s.Snapshot()).ToList());
}
