using System.Runtime.InteropServices;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed class EqDamageParser(
    IDamageLineParser damageLineParser,
    IKillLineParser killLineParser,
    IMobNameNormalizer mobNameNormalizer) : IEqDamageParser
{
    private static readonly StringComparer MobNameComparer = StringComparer.OrdinalIgnoreCase;
    private readonly LootLineParser lootLineParser = new();

    public DamageSummary Parse(string path)
    {
        return Parse(ReadLogLines(path));
    }

    public DamageSummary Parse(IEnumerable<string> lines)
    {
        var totals = new Dictionary<string, MobDamage>(MobNameComparer);
        var activeEncounters = new Dictionary<string, MobDamage>(MobNameComparer);
        var kills = new List<KillSummary>();
        var lootEvents = new List<LootEvent>();
        var lineNumber = 0;

        foreach (var text in lines)
        {
            lineNumber++;

            var line = EqLogLine.FromText(lineNumber, text);
            if (TrackDamage(line, totals, activeEncounters))
                continue;

            if (TrackKill(line, activeEncounters, kills))
                continue;

            TrackLoot(line, lootEvents);
        }

        var mobs = OrderByDamage(totals.Values);
        var openEncounters = OrderByDamage(activeEncounters.Values);
        var loot = LinkLoot(lootEvents, kills);

        return new DamageSummary(
            mobs,
            kills,
            openEncounters,
            loot,
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
            return false;

        var mobName = mobNameNormalizer.Normalize(damage.MobName);
        AddDamage(totals, mobName, damage.Source, damage.Amount, damage.Kind);
        AddDamage(activeEncounters, mobName, damage.Source, damage.Amount, damage.Kind);
        return true;
    }

    private bool TrackKill(
        EqLogLine line,
        Dictionary<string, MobDamage> activeEncounters,
        List<KillSummary> kills)
    {
        var kill = killLineParser.TryParse(line.Message);
        if (kill is null)
            return false;

        var killedMobName = mobNameNormalizer.Normalize(kill.MobName);
        if (!activeEncounters.Remove(killedMobName, out var encounter))
            return false;

        kills.Add(new KillSummary(line.Number, line.Timestamp, encounter, kill.KilledBy));
        return true;
    }

    private void TrackLoot(EqLogLine line, List<LootEvent> lootEvents)
    {
        var loot = lootLineParser.TryParse(line.Message);
        if (loot is null) return;

        lootEvents.Add(loot with
        {
            LineNumber = line.Number,
            Timestamp  = line.Timestamp
        });
    }

    private IReadOnlyList<LootSummary> LinkLoot(List<LootEvent> lootEvents, List<KillSummary> kills)
    {
        // Build an index: normalizedMobName -> list of kill line numbers (ascending).
        var killIndex = new Dictionary<string, List<int>>(MobNameComparer);
        foreach (var k in kills)
        {
            var name = k.Mob.Name;
            if (!killIndex.TryGetValue(name, out var list))
                killIndex[name] = list = [];
            list.Add(k.LineNumber);
        }

        var result = new List<LootSummary>(lootEvents.Count);
        foreach (var ev in lootEvents)
        {
            var mobName = mobNameNormalizer.Normalize(ev.MobName);
            int? killLine = null;

            if (killIndex.TryGetValue(mobName, out var killLines))
            {
                // Most-recent kill that happened before (or at) the loot line.
                killLine = killLines
                    .Where(kl => kl <= ev.LineNumber)
                    .DefaultIfEmpty(-1)
                    .Max();
                if (killLine == -1) killLine = null;
            }

            result.Add(new LootSummary(
                ev.LineNumber, ev.Timestamp, ev.ItemName, mobName,
                ev.AutoSold, killLine));
        }

        return result;
    }

    private static void AddDamage(
        Dictionary<string, MobDamage> totals,
        string mobName,
        string source,
        int amount,
        DamageKind kind)
    {
        ref var mob = ref CollectionsMarshal.GetValueRefOrAddDefault(totals, mobName, out var exists);
        if (!exists)
            mob = new MobDamage(mobName);

        mob!.Add(source, amount, kind);
    }

    private static MobDamage[] OrderByDamage(IEnumerable<MobDamage> mobs)
    {
        return mobs
            .OrderByDescending(mob => mob.TotalDamage)
            .ThenBy(mob => mob.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ReadLogLines(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}

