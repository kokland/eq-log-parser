namespace EqLogParser.Infrastructure.Parsing.Handlers;

public sealed class MissHandler(IMissLineParser parser, EncounterTracker tracker) : ILogLineHandler
{
    public bool Handle(EqLogLine line)
    {
        var mobName = parser.TryParse(line.Message);
        if (mobName is null) return false;
        tracker.AddMiss(mobName);
        return true;
    }

    public void Reset() { }
}
