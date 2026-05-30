using System.Runtime.InteropServices;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed class EqDamageParser(
    IDamageLineParser damageLineParser,
    IKillLineParser killLineParser,
    IMobNameNormalizer mobNameNormalizer) : IEqDamageParser
{
    private static readonly StringComparer MobNameComparer = StringComparer.OrdinalIgnoreCase;

    public DamageSummary Parse(string path)
    {
        return Parse(File.ReadLines(path));
    }

    public DamageSummary Parse(IEnumerable<string> lines)
    {
        var totals = new Dictionary<string, MobDamage>(MobNameComparer);
        var activeEncounters = new Dictionary<string, MobDamage>(MobNameComparer);
        var kills = new List<KillSummary>();
        var lineNumber = 0;

        foreach (var text in lines)
        {
            lineNumber++;

            var line = EqLogLine.FromText(lineNumber, text);
            if (TrackDamage(line, totals, activeEncounters))
            {
                continue;
            }

            TrackKill(line, activeEncounters, kills);
        }

        var mobs = OrderByDamage(totals.Values);
        var openEncounters = OrderByDamage(activeEncounters.Values);

        return new DamageSummary(
            mobs,
            kills,
            openEncounters,
            mobs.Sum(mob => mob.TotalDamage),
            mobs.Sum(mob => mob.Hits));
    }

    private bool TrackDamage(
        EqLogLine line,
        Dictionary<string, MobDamage> totals,
        Dictionary<string, MobDamage> activeEncounters)
    {
        var damage = damageLineParser.TryParse(line.Message);
        if (damage is null)
        {
            return false;
        }

        var mobName = mobNameNormalizer.Normalize(damage.MobName);
        AddDamage(totals, mobName, damage.Amount, damage.Kind);
        AddDamage(activeEncounters, mobName, damage.Amount, damage.Kind);

        return true;
    }

    private void TrackKill(
        EqLogLine line,
        Dictionary<string, MobDamage> activeEncounters,
        List<KillSummary> kills)
    {
        var kill = killLineParser.TryParse(line.Message);
        if (kill is null)
        {
            return;
        }

        var killedMobName = mobNameNormalizer.Normalize(kill.MobName);
        if (!activeEncounters.Remove(killedMobName, out var encounter))
        {
            return;
        }

        kills.Add(new KillSummary(
            line.Number,
            line.Timestamp,
            encounter,
            kill.KilledBy));
    }

    private static void AddDamage(
        Dictionary<string, MobDamage> totals,
        string mobName,
        int amount,
        DamageKind kind)
    {
        ref var mob = ref CollectionsMarshal.GetValueRefOrAddDefault(totals, mobName, out var exists);
        if (!exists)
        {
            mob = new MobDamage(mobName);
        }

        mob!.Add(amount, kind);
    }

    private static MobDamage[] OrderByDamage(IEnumerable<MobDamage> mobs)
    {
        return mobs
            .OrderByDescending(mob => mob.TotalDamage)
            .ThenBy(mob => mob.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
