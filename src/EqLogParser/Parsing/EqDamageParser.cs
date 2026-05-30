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

    // Incremental-parse state — accumulated across calls to Parse(path).
    private readonly Dictionary<string, MobDamage> _totals          = new(MobNameComparer);
    private readonly Dictionary<string, MobDamage> _activeEncounters = new(MobNameComparer);
    private readonly List<KillSummary>              _kills            = [];
    private readonly List<LootEvent>                _lootEvents       = [];
    private int  _lineNumber  = 0;
    private long _byteOffset  = 0;

    /// <summary>
    /// Reads only the lines appended since the last call and merges them into the
    /// accumulated state. If the file has shrunk (rotation / truncation) the state
    /// is reset and the file is read from the beginning.
    /// </summary>
    public DamageSummary Parse(string path)
    {
        using var stream = OpenFile(path);

        // Detect log rotation / truncation.
        if (stream.Length < _byteOffset)
            Reset();

        stream.Seek(_byteOffset, SeekOrigin.Begin);

        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            while (reader.ReadLine() is { } text)
            {
                _lineNumber++;
                var line = EqLogLine.FromText(_lineNumber, text);

                if (TrackDamage(line))  continue;
                if (TrackKill(line))    continue;
                TrackLoot(line);
            }
        }

        // After exhausting all lines the reader sits at EOF == stream.Length.
        _byteOffset = stream.Length;

        return BuildSummary();
    }

    /// <summary>
    /// Parses an in-memory sequence of lines from scratch (useful for tests).
    /// Resets all accumulated state before parsing.
    /// </summary>
    public DamageSummary Parse(IEnumerable<string> lines)
    {
        Reset();

        foreach (var text in lines)
        {
            _lineNumber++;
            var line = EqLogLine.FromText(_lineNumber, text);

            if (TrackDamage(line))  continue;
            if (TrackKill(line))    continue;
            TrackLoot(line);
        }

        return BuildSummary();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void Reset()
    {
        _totals.Clear();
        _activeEncounters.Clear();
        _kills.Clear();
        _lootEvents.Clear();
        _lineNumber = 0;
        _byteOffset = 0;
    }

    private bool TrackDamage(EqLogLine line)
    {
        var damage = damageLineParser.TryParse(line.Message);
        if (damage is null) return false;

        var mobName = mobNameNormalizer.Normalize(damage.MobName);
        AddDamage(_totals, mobName, damage.Source, damage.Amount, damage.Kind);
        AddDamage(_activeEncounters, mobName, damage.Source, damage.Amount, damage.Kind);
        return true;
    }

    private bool TrackKill(EqLogLine line)
    {
        var kill = killLineParser.TryParse(line.Message);
        if (kill is null) return false;

        var killedMobName = mobNameNormalizer.Normalize(kill.MobName);
        if (!_activeEncounters.Remove(killedMobName, out var encounter))
            return false;

        _kills.Add(new KillSummary(line.Number, line.Timestamp, encounter, kill.KilledBy));
        return true;
    }

    private void TrackLoot(EqLogLine line)
    {
        var loot = lootLineParser.TryParse(line.Message);
        if (loot is null) return;

        _lootEvents.Add(loot with { LineNumber = line.Number, Timestamp = line.Timestamp });
    }

    private DamageSummary BuildSummary()
    {
        var mobs           = OrderByDamage(_totals.Values);
        var openEncounters = OrderByDamage(_activeEncounters.Values);
        var loot           = LinkLoot(_lootEvents, _kills);

        return new DamageSummary(
            mobs,
            _kills.AsReadOnly(),
            openEncounters,
            loot,
            mobs.Sum(m => m.TotalDamage),
            mobs.Sum(m => m.Hits));
    }

    private IReadOnlyList<LootSummary> LinkLoot(List<LootEvent> lootEvents, List<KillSummary> kills)
    {
        // Build index: normalizedMobName -> sorted list of kill line numbers (ascending).
        var killIndex = new Dictionary<string, List<int>>(MobNameComparer);
        foreach (var k in kills)
        {
            if (!killIndex.TryGetValue(k.Mob.Name, out var list))
                killIndex[k.Mob.Name] = list = [];
            list.Add(k.LineNumber);
        }

        var result = new List<LootSummary>(lootEvents.Count);
        foreach (var ev in lootEvents)
        {
            var mobName = mobNameNormalizer.Normalize(ev.MobName);
            int? killLine = null;

            if (killIndex.TryGetValue(mobName, out var killLines))
            {
                // Binary search for the last kill at or before the loot line.
                int lo = 0, hi = killLines.Count - 1, best = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (killLines[mid] <= ev.LineNumber) { best = killLines[mid]; lo = mid + 1; }
                    else                                  hi = mid - 1;
                }
                if (best >= 0) killLine = best;
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

    private static FileStream OpenFile(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
    }
}
