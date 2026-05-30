namespace EqLogParser.Domain;

public sealed class MobDamage(string name)
{
    public string Name { get; } = name;
    public long DirectDamage { get; private set; }
    public long YourEffectDamage { get; private set; }
    public long TotalDamage => DirectDamage + YourEffectDamage;
    public long Hits { get; private set; }

    public void Add(int damage, DamageKind kind)
    {
        Hits++;

        if (kind == DamageKind.Direct)
        {
            DirectDamage += damage;
            return;
        }

        YourEffectDamage += damage;
    }
}
