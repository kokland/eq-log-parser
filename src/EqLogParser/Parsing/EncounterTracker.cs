using System.Runtime.InteropServices;
using EqLogParser.Domain;

namespace EqLogParser.Parsing;

/// <summary>
/// Owns the mutable per-mob damage accumulators (_totals and _activeEncounters),
/// and the accumulated KillSummary list.  Shared by the damage, kill, resist, and
/// miss handlers so they can all update the same mob objects.
/// </summary>
public sealed class EncounterTracker(IMobNameNormalizer normalizer)
{
    private readonly Dictionary<string, MobDamage> _totals           = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MobDamage> _activeEncounters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KillSummary>              _kills            = [];

    public IReadOnlyList<KillSummary> Kills           => _kills.AsReadOnly();
    public IReadOnlyList<MobDamage>   Mobs            => OrderByDamage(_totals.Values);
    public IReadOnlyList<MobDamage>   OpenEncounters  => OrderByDamage(_activeEncounters.Values);

    public void AddDamage(
        string rawMobName, string source, int amount, DamageKind kind,
        DateTime? timestamp = null, string? spellName = null)
    {
        var mob = normalizer.Normalize(rawMobName);
        Upsert(_totals,           mob).Add(source, amount, kind, timestamp, spellName);
        Upsert(_activeEncounters, mob).Add(source, amount, kind, timestamp, spellName);
    }

    /// <summary>
    /// Closes the active encounter for <paramref name="rawMobName"/> and records a KillSummary.
    /// Returns the new summary or null if no active encounter matched.
    /// </summary>
    public KillSummary? RecordKill(string rawMobName, string killedBy, EqLogLine line)
    {
        var mob = normalizer.Normalize(rawMobName);
        if (!_activeEncounters.Remove(mob, out var encounter))
            return null;

        double? dps = null;
        if (encounter.FirstHitTime.HasValue && line.ParsedTimestamp.HasValue)
        {
            var secs = (line.ParsedTimestamp.Value - encounter.FirstHitTime.Value).TotalSeconds;
            if (secs > 0) dps = encounter.TotalDamage / secs;
        }

        var kill = new KillSummary(line.Number, line.Timestamp, encounter.Snapshot(), killedBy, dps);
        _kills.Add(kill);
        return kill;
    }

    public void AddResist(string rawMobName)
    {
        var mob = normalizer.Normalize(rawMobName);
        if (_totals          .TryGetValue(mob, out var t)) t.AddResist();
        if (_activeEncounters.TryGetValue(mob, out var a)) a.AddResist();
    }

    public void AddMiss(string rawMobName)
    {
        var mob = normalizer.Normalize(rawMobName);
        if (_totals          .TryGetValue(mob, out var t)) t.AddMiss();
        if (_activeEncounters.TryGetValue(mob, out var a)) a.AddMiss();
    }

    public void Reset()
    {
        _totals.Clear();
        _activeEncounters.Clear();
        _kills.Clear();
    }

    private static MobDamage Upsert(Dictionary<string, MobDamage> dict, string name)
    {
        ref var mob = ref CollectionsMarshal.GetValueRefOrAddDefault(dict, name, out var exists);
        if (!exists) mob = new MobDamage(name);
        return mob!;
    }

    private static MobDamage[] OrderByDamage(IEnumerable<MobDamage> mobs) =>
        mobs.OrderByDescending(m => m.TotalDamage)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
