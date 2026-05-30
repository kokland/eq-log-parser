using EqLogParser.Core.Domain;

namespace EqLogParser.Infrastructure.Parsing;

/// <summary>
/// Detects session boundaries (gaps > <see cref="IdleThreshold"/>) and
/// builds <see cref="SessionSummary"/> objects once all lines are processed.
/// </summary>
public sealed class SessionTracker
{
    public TimeSpan IdleThreshold { get; set; } = TimeSpan.FromMinutes(30);

    private readonly List<(int Line, DateTime Time)> _starts = [];
    private readonly List<(int Line, DateTime Time)> _ends   = [];
    private DateTime _lastTime   = DateTime.MinValue;
    private int      _lastLine   = 0;

    public void Track(EqLogLine line)
    {
        if (!line.ParsedTimestamp.HasValue) return;
        var ts = line.ParsedTimestamp.Value;

        if (_lastTime == DateTime.MinValue)
        {
            _starts.Add((line.Number, ts));
        }
        else if (ts - _lastTime > IdleThreshold)
        {
            _ends  .Add((_lastLine, _lastTime));
            _starts.Add((line.Number, ts));
        }

        _lastTime = ts;
        _lastLine = line.Number;
    }

    public IReadOnlyList<SessionSummary> BuildSessions(
        IReadOnlyList<KillSummary>  kills,
        IReadOnlyList<LootSummary>  loot,
        IReadOnlyList<XpEvent>      xp,
        IReadOnlyList<DeathEvent>   deaths,
        IReadOnlyList<HealEvent>    heals,
        IReadOnlyList<ZoneEvent>    zones)
    {
        if (_starts.Count == 0) return [];

        var ends  = new List<(int Line, DateTime Time)>(_ends) { (_lastLine, _lastTime) };
        int count = Math.Min(_starts.Count, ends.Count);
        var result = new List<SessionSummary>(count);

        for (int i = 0; i < count; i++)
        {
            var (startLine, startTime) = _starts[i];
            var (endLine,   endTime)   = ends[i];

            var sessionKills = kills.Where(k => k.LineNumber >= startLine && k.LineNumber <= endLine).ToList();
            var lootCount    = loot  .Count(l => l.LineNumber >= startLine && l.LineNumber <= endLine);
            var xpTotal      = xp    .Where(x => x.LineNumber >= startLine && x.LineNumber <= endLine).Sum(x => x.Percent);
            var deathCount   = deaths.Count(d => d.LineNumber >= startLine && d.LineNumber <= endLine);
            var healing      = heals .Where(h => h.LineNumber >= startLine && h.LineNumber <= endLine).Sum(h => (long)h.Amount);

            int  resistCount = sessionKills.Sum(k => k.Mob.Resists);
            int  missCount   = sessionKills.Sum(k => k.Mob.Misses);
            long totalDamage = sessionKills.Sum(k => k.Mob.TotalDamage);

            string? zone = zones
                .Where(z => z.LineNumber >= startLine && z.LineNumber <= endLine)
                .OrderBy(z => z.LineNumber)
                .Select(z => z.ZoneName)
                .FirstOrDefault();

            double secs = (endTime - startTime).TotalSeconds;
            double dps  = secs > 0 ? totalDamage / secs : 0;

            result.Add(new SessionSummary(
                Number:       i + 1,
                StartLine:    startLine,
                EndLine:      endLine,
                StartTime:    startTime,
                EndTime:      endTime,
                KillCount:    sessionKills.Count,
                LootCount:    lootCount,
                TotalDamage:  totalDamage,
                XpPercent:    xpTotal,
                Deaths:       deathCount,
                Resists:      resistCount,
                Misses:       missCount,
                Zone:         zone,
                TotalHealing: healing,
                Dps:          dps));
        }

        result.Reverse();
        return result.AsReadOnly();
    }

    public void Reset()
    {
        _starts.Clear();
        _ends.Clear();
        _lastTime = DateTime.MinValue;
        _lastLine = 0;
    }
}
