namespace EqLogParser.Domain;

public sealed class SourceDamage(string source)
{
    public string Source { get; } = source;
    public long DirectDamage { get; private set; }
    public long EffectDamage { get; private set; }
    public long TotalDamage => DirectDamage + EffectDamage;
    public long Hits { get; private set; }

    public void Add(int damage, DamageKind kind)
    {
        Hits++;
        if (kind == DamageKind.Direct)
            DirectDamage += damage;
        else
            EffectDamage += damage;
    }
}
