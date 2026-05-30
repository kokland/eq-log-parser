using EqLogParser.Core.Domain;

namespace EqLogParser.Infrastructure.Parsing.Handlers;

public sealed class KillHandler(IKillLineParser parser, EncounterTracker tracker) : ILogLineHandler
{
    public bool Handle(EqLogLine line)
    {
        var ev = parser.TryParse(line.Message);
        if (ev is null) return false;
        tracker.RecordKill(ev.MobName, ev.KilledBy, line);
        return true;
    }

    public void Reset() { }
}
