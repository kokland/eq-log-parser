using EqLogParser.Core;
using EqLogParser.Core.Domain;
using EqLogParser.Infrastructure.Parsing.Handlers;

namespace EqLogParser.Infrastructure.Parsing;

public sealed class EqDamageParser : IEqDamageParser
{
    private readonly IMobNameNormalizer _normalizer;
    private readonly EncounterTracker   _encounters;
    private readonly SessionTracker     _sessions = new();

    private readonly LootHandler  _lootHandler;
    private readonly XpHandler    _xpHandler;
    private readonly DeathHandler _deathHandler;
    private readonly ZoneHandler  _zoneHandler;
    private readonly HealHandler  _healHandler;

    private readonly ILogLineHandler[] _pipeline;

    private int  _lineNumber = 0;
    private long _byteOffset = 0;

    // Full-injection ctor (for testing / DI).
    public EqDamageParser(
        IDamageLineParser  damageLineParser,
        IKillLineParser    killLineParser,
        ILootLineParser    lootLineParser,
        IXpLineParser      xpLineParser,
        IDeathLineParser   deathLineParser,
        IZoneLineParser    zoneLineParser,
        IHealLineParser    healLineParser,
        IResistLineParser  resistLineParser,
        IMissLineParser    missLineParser,
        IMobNameNormalizer mobNameNormalizer)
    {
        _normalizer   = mobNameNormalizer;
        _encounters   = new EncounterTracker(mobNameNormalizer);

        _lootHandler  = new LootHandler(lootLineParser);
        _xpHandler    = new XpHandler(xpLineParser);
        _deathHandler = new DeathHandler(deathLineParser);
        _zoneHandler  = new ZoneHandler(zoneLineParser);
        _healHandler  = new HealHandler(healLineParser);

        _pipeline =
        [
            new DamageHandler (damageLineParser,  _encounters),
            new KillHandler   (killLineParser,     _encounters),
            _lootHandler,
            _xpHandler,
            _deathHandler,
            _zoneHandler,
            _healHandler,
            new ResistHandler (resistLineParser,   _encounters),
            new MissHandler   (missLineParser,     _encounters),
        ];
    }

    // Convenience ctor that uses default concrete parsers.
    public EqDamageParser(
        IDamageLineParser  damageLineParser,
        IKillLineParser    killLineParser,
        IMobNameNormalizer mobNameNormalizer)
        : this(
            damageLineParser,
            killLineParser,
            new LootLineParser(),
            new XpLineParser(),
            new DeathLineParser(),
            new ZoneLineParser(),
            new HealLineParser(),
            new ResistLineParser(),
            new MissLineParser(),
            mobNameNormalizer)
    { }

    public TimeSpan SessionIdleThreshold
    {
        get => _sessions.IdleThreshold;
        set => _sessions.IdleThreshold = value;
    }

    /// <summary>
    /// Reads only the lines appended since the last call (incremental / watch mode).
    /// Resets state automatically if the file has been truncated.
    /// </summary>
    public DamageSummary Parse(string path)
    {
        using var stream = OpenFile(path);

        if (stream.Length < _byteOffset)
            FullReset();

        stream.Seek(_byteOffset, SeekOrigin.Begin);

        using (var reader = new StreamReader(stream, leaveOpen: true))
            ProcessLines(reader);

        _byteOffset = stream.Length;
        return BuildSummary();
    }

    /// <summary>Parses an in-memory line sequence (useful for tests).</summary>
    public DamageSummary Parse(IEnumerable<string> lines)
    {
        FullReset();
        using var reader = new StringReader(string.Join('\n', lines));
        ProcessLines(reader);
        return BuildSummary();
    }

    // -------------------------------------------------------------------------

    private void ProcessLines(TextReader reader)
    {
        while (reader.ReadLine() is { } text)
        {
            _lineNumber++;
            var line = EqLogLine.FromText(_lineNumber, text);
            _sessions.Track(line);
            foreach (var handler in _pipeline)
                if (handler.Handle(line)) break;
        }
    }

    private DamageSummary BuildSummary()
    {
        var loot     = LootLinker.Link(_lootHandler.Events, _encounters.Kills, _normalizer);
        var sessions = _sessions.BuildSessions(
            _encounters.Kills,
            loot,
            _xpHandler.Events,
            _deathHandler.Events,
            _healHandler.Events,
            _zoneHandler.Events);

        var mobs          = _encounters.Mobs;
        long totalHealing = _healHandler.Events.Sum(h => (long)h.Amount);

        return new DamageSummary(
            mobs,
            _encounters.Kills,
            _encounters.OpenEncounters,
            loot,
            _xpHandler.Events,
            sessions,
            _deathHandler.Events,
            _zoneHandler.Events,
            _healHandler.Events,
            mobs.Sum(m => m.TotalDamage),
            mobs.Sum(m => m.Hits),
            totalHealing);
    }

    private void FullReset()
    {
        _encounters.Reset();
        _sessions.Reset();
        foreach (var h in _pipeline) h.Reset();
        _lineNumber = 0;
        _byteOffset = 0;
    }

    private static FileStream OpenFile(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
