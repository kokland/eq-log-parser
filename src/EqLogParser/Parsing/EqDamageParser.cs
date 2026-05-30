using System.Globalization;
using System.Runtime.InteropServices;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

public sealed class EqDamageParser(
    IDamageLineParser damageLineParser,
    IKillLineParser killLineParser,
    IMobNameNormalizer mobNameNormalizer) : IEqDamageParser
{
    private static readonly StringComparer MobNameComparer = StringComparer.OrdinalIgnoreCase;

    // Two formats for EQ timestamps: 2-digit vs space-padded 1-digit day.
    private static readonly string[] TimestampFormats =
    [
        "ddd MMM dd HH:mm:ss yyyy",
        "ddd MMM  d HH:mm:ss yyyy"
    ];

    /// <summary>Gap larger than this between consecutive timestamped lines starts a new session.</summary>
    public TimeSpan SessionIdleThreshold { get; set; } = TimeSpan.FromMinutes(30);

    private readonly LootLineParser   lootLineParser   = new();
    private readonly XpLineParser     xpLineParser     = new();
    private readonly DeathLineParser  deathLineParser  = new();
    private readonly ZoneLineParser   zoneLineParser   = new();
    private readonly HealLineParser   healLineParser   = new();
    private readonly ResistLineParser resistLineParser = new();
    private readonly MissLineParser   missLineParser   = new();

    // Incremental-parse state — accumulated across calls to Parse(path).
    private readonly Dictionary<string, MobDamage> _totals           = new(MobNameComparer);
    private readonly Dictionary<string, MobDamage> _activeEncounters = new(MobNameComparer);
    private readonly List<KillSummary>   _kills      = [];
    private readonly List<LootEvent>     _lootEvents = [];
    private readonly List<XpEvent>       _xpEvents   = [];
    private readonly List<DeathEvent>    _deaths     = [];
    private readonly List<ZoneEvent>     _zones      = [];
    private readonly List<HealEvent>     _heals      = [];
    // Session boundary tracking.
    private readonly List<(int StartLine, DateTime StartTime)> _sessionStarts = [];
    private readonly List<(int EndLine, DateTime EndTime)>     _sessionEnds   = [];
    private DateTime _lastLineTime   = DateTime.MinValue;
    private int      _lastLineNumber = 0;
    private int      _lineNumber     = 0;
    private long     _byteOffset     = 0;

    /// <summary>
    /// Reads only the lines appended since the last call and merges them into the
    /// accumulated state. If the file has shrunk (rotation / truncation) the state
    /// is reset and the file is read from the beginning.
    /// </summary>
    public DamageSummary Parse(string path)
    {
        using var stream = OpenFile(path);

        if (stream.Length < _byteOffset)
            Reset();

        stream.Seek(_byteOffset, SeekOrigin.Begin);

        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            while (reader.ReadLine() is { } text)
            {
                _lineNumber++;
                var line = EqLogLine.FromText(_lineNumber, text);

                TrackSession(line);
                if (TrackDamage(line))  continue;
                if (TrackKill(line))    continue;
                if (TrackLoot(line))    continue;
                if (TrackXp(line))      continue;
                if (TrackDeath(line))   continue;
                if (TrackZone(line))    continue;
                if (TrackHeal(line))    continue;
                if (TrackResist(line))  continue;
                TrackMiss(line);
            }
        }

        _byteOffset = stream.Length;
        return BuildSummary();
    }

    /// <summary>
    /// Parses an in-memory sequence of lines from scratch (useful for tests).
    /// </summary>
    public DamageSummary Parse(IEnumerable<string> lines)
    {
        Reset();

        foreach (var text in lines)
        {
            _lineNumber++;
            var line = EqLogLine.FromText(_lineNumber, text);

            TrackSession(line);
            if (TrackDamage(line))  continue;
            if (TrackKill(line))    continue;
            if (TrackLoot(line))    continue;
            if (TrackXp(line))      continue;
            if (TrackDeath(line))   continue;
            if (TrackZone(line))    continue;
            if (TrackHeal(line))    continue;
            if (TrackResist(line))  continue;
            TrackMiss(line);
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
        _xpEvents.Clear();
        _deaths.Clear();
        _zones.Clear();
        _heals.Clear();
        _sessionStarts.Clear();
        _sessionEnds.Clear();
        _lastLineTime   = DateTime.MinValue;
        _lastLineNumber = 0;
        _lineNumber     = 0;
        _byteOffset     = 0;
    }

    private bool TrackDamage(EqLogLine line)
    {
        var damage = damageLineParser.TryParse(line.Message);
        if (damage is null) return false;

        var mobName = mobNameNormalizer.Normalize(damage.MobName);
        AddDamage(_totals,          mobName, damage.Source, damage.Amount, damage.Kind, line.Timestamp, damage.SpellName);
        AddDamage(_activeEncounters, mobName, damage.Source, damage.Amount, damage.Kind, line.Timestamp, damage.SpellName);
        return true;
    }

    private bool TrackKill(EqLogLine line)
    {
        var kill = killLineParser.TryParse(line.Message);
        if (kill is null) return false;

        var killedMobName = mobNameNormalizer.Normalize(kill.MobName);
        if (!_activeEncounters.Remove(killedMobName, out var encounter))
            return false;

        double? dps = null;
        if (encounter.FirstHitTimestamp is not null
            && TryParseTimestamp(encounter.FirstHitTimestamp, out var startTime)
            && TryParseTimestamp(line.Timestamp, out var killTime))
        {
            var secs = (killTime - startTime).TotalSeconds;
            if (secs > 0)
                dps = encounter.TotalDamage / secs;
        }

        _kills.Add(new KillSummary(line.Number, line.Timestamp, encounter, kill.KilledBy, dps));
        return true;
    }

    private bool TrackLoot(EqLogLine line)
    {
        var loot = lootLineParser.TryParse(line.Message);
        if (loot is null) return false;

        _lootEvents.Add(loot with { LineNumber = line.Number, Timestamp = line.Timestamp });
        return true;
    }

    private bool TrackXp(EqLogLine line)
    {
        var xp = xpLineParser.TryParse(line.Message);
        if (xp is null) return false;

        _xpEvents.Add(xp with { LineNumber = line.Number, Timestamp = line.Timestamp });
        return true;
    }

    private bool TrackDeath(EqLogLine line)
    {
        var killer = deathLineParser.TryParse(line.Message);
        if (killer is null) return false;

        _deaths.Add(new DeathEvent(line.Number, line.Timestamp, killer));
        return true;
    }

    private bool TrackZone(EqLogLine line)
    {
        var zone = zoneLineParser.TryParse(line.Message);
        if (zone is null) return false;

        _zones.Add(new ZoneEvent(line.Number, line.Timestamp, zone));
        return true;
    }

    private bool TrackHeal(EqLogLine line)
    {
        var heal = healLineParser.TryParse(line.Message);
        if (heal is null) return false;

        _heals.Add(heal with { LineNumber = line.Number, Timestamp = line.Timestamp });
        return true;
    }

    private bool TrackResist(EqLogLine line)
    {
        var result = resistLineParser.TryParse(line.Message);
        if (result is null) return false;

        var mobName = mobNameNormalizer.Normalize(result.Value.MobName);
        // Increment resist counter on both totals and active encounter (if present).
        if (_totals.TryGetValue(mobName, out var total))
            total.AddResist();
        if (_activeEncounters.TryGetValue(mobName, out var active))
            active.AddResist();
        return true;
    }

    private bool TrackMiss(EqLogLine line)
    {
        var mobName = missLineParser.TryParse(line.Message);
        if (mobName is null) return false;

        var normalized = mobNameNormalizer.Normalize(mobName);
        if (_totals.TryGetValue(normalized, out var total))
            total.AddMiss();
        if (_activeEncounters.TryGetValue(normalized, out var active))
            active.AddMiss();
        return true;
    }

    private void TrackSession(EqLogLine line)
    {
        if (!TryParseTimestamp(line.Timestamp, out var ts)) return;

        if (_lastLineTime == DateTime.MinValue)
        {
            _sessionStarts.Add((line.Number, ts));
        }
        else if (ts - _lastLineTime > SessionIdleThreshold)
        {
            _sessionEnds.Add((_lastLineNumber, _lastLineTime));
            _sessionStarts.Add((line.Number, ts));
        }

        _lastLineTime   = ts;
        _lastLineNumber = line.Number;
    }

    private DamageSummary BuildSummary()
    {
        var mobs           = OrderByDamage(_totals.Values);
        var openEncounters = OrderByDamage(_activeEncounters.Values);
        var loot           = LinkLoot(_lootEvents, _kills);
        var sessions       = BuildSessions(loot);
        long totalHealing  = _heals.Sum(h => (long)h.Amount);

        return new DamageSummary(
            mobs,
            _kills.AsReadOnly(),
            openEncounters,
            loot,
            _xpEvents.AsReadOnly(),
            sessions,
            _deaths.AsReadOnly(),
            _zones.AsReadOnly(),
            _heals.AsReadOnly(),
            mobs.Sum(m => m.TotalDamage),
            mobs.Sum(m => m.Hits),
            totalHealing);
    }

    private IReadOnlyList<LootSummary> LinkLoot(List<LootEvent> lootEvents, List<KillSummary> kills)
    {
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
                int lo = 0, hi = killLines.Count - 1, best = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (killLines[mid] <= ev.LineNumber) { best = killLines[mid]; lo = mid + 1; }
                    else                                  hi = mid - 1;
                }
                if (best >= 0) killLine = best;
            }

            result.Add(new LootSummary(ev.LineNumber, ev.Timestamp, ev.ItemName, mobName, ev.AutoSold, killLine));
        }

        return result;
    }

    private IReadOnlyList<SessionSummary> BuildSessions(IReadOnlyList<LootSummary> loot)
    {
        if (_sessionStarts.Count == 0) return [];

        var ends  = new List<(int EndLine, DateTime EndTime)>(_sessionEnds) { (_lastLineNumber, _lastLineTime) };
        int count = Math.Min(_sessionStarts.Count, ends.Count);
        var result = new List<SessionSummary>(count);

        for (int i = 0; i < count; i++)
        {
            var (startLine, startTime) = _sessionStarts[i];
            var (endLine,   endTime)   = ends[i];

            var sessionKills  = _kills  .Where(k => k.LineNumber >= startLine && k.LineNumber <= endLine).ToList();
            var lootCount     = loot     .Count(l => l.LineNumber >= startLine && l.LineNumber <= endLine);
            var xpPercent     = _xpEvents.Where(x => x.LineNumber >= startLine && x.LineNumber <= endLine).Sum(x => x.Percent);
            var deathCount    = _deaths  .Count(d => d.LineNumber >= startLine && d.LineNumber <= endLine);
            var healingTotal  = _heals   .Where(h => h.LineNumber >= startLine && h.LineNumber <= endLine).Sum(h => (long)h.Amount);

            // Resists and misses for kills within the session (from mob accumulators via kills).
            int resistCount = sessionKills.Sum(k => k.Mob.Resists);
            int missCount   = sessionKills.Sum(k => k.Mob.Misses);

            long totalDamage = sessionKills.Sum(k => k.Mob.TotalDamage);

            // First zone entered during this session.
            string? zone = _zones
                .Where(z => z.LineNumber >= startLine && z.LineNumber <= endLine)
                .OrderBy(z => z.LineNumber)
                .Select(z => z.ZoneName)
                .FirstOrDefault();

            // Session DPS based on actual play time (StartTime → EndTime).
            double durationSecs = (endTime - startTime).TotalSeconds;
            double dps = durationSecs > 0 ? totalDamage / durationSecs : 0;

            result.Add(new SessionSummary(
                Number:       i + 1,
                StartLine:    startLine,
                EndLine:      endLine,
                StartTime:    startTime,
                EndTime:      endTime,
                KillCount:    sessionKills.Count,
                LootCount:    lootCount,
                TotalDamage:  totalDamage,
                XpPercent:    xpPercent,
                Deaths:       deathCount,
                Resists:      resistCount,
                Misses:       missCount,
                Zone:         zone,
                TotalHealing: healingTotal,
                Dps:          dps));
        }

        result.Reverse(); // newest first
        return result.AsReadOnly();
    }

    private static bool TryParseTimestamp(string? raw, out DateTime result)
    {
        if (raw is not null &&
            DateTime.TryParseExact(raw, TimestampFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
            return true;

        result = default;
        return false;
    }

    private static void AddDamage(
        Dictionary<string, MobDamage> totals,
        string mobName,
        string source,
        int amount,
        DamageKind kind,
        string? timestamp   = null,
        string? spellName   = null)
    {
        ref var mob = ref CollectionsMarshal.GetValueRefOrAddDefault(totals, mobName, out var exists);
        if (!exists)
            mob = new MobDamage(mobName);

        mob!.Add(source, amount, kind, timestamp, spellName);
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
