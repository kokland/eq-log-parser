using EqLogParser.Domain;

namespace EqLogParser.Parsing;

/// <summary>
/// Links raw <see cref="LootEvent"/>s to the most-recent preceding kill of
/// the same (already normalised) mob name.
/// </summary>
public static class LootLinker
{
    public static IReadOnlyList<LootSummary> Link(
        IReadOnlyList<LootEvent>  lootEvents,
        IReadOnlyList<KillSummary> kills,
        IMobNameNormalizer normalizer)
    {
        // Build a kill-line index keyed on normalised mob name.
        var killIndex = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in kills)
        {
            if (!killIndex.TryGetValue(k.Mob.Name, out var list))
                killIndex[k.Mob.Name] = list = [];
            list.Add(k.LineNumber);
        }

        var result = new List<LootSummary>(lootEvents.Count);
        foreach (var ev in lootEvents)
        {
            var mobName = normalizer.Normalize(ev.MobName);
            int? killLine = null;

            if (killIndex.TryGetValue(mobName, out var killLines))
            {
                // Binary search for the latest kill that precedes this loot line.
                int lo = 0, hi = killLines.Count - 1, best = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (killLines[mid] <= ev.LineNumber) { best = killLines[mid]; lo = mid + 1; }
                    else                                   hi = mid - 1;
                }
                if (best >= 0) killLine = best;
            }

            result.Add(new LootSummary(ev.LineNumber, ev.Timestamp, ev.ItemName, mobName, ev.AutoSold, killLine));
        }

        return result.AsReadOnly();
    }
}
