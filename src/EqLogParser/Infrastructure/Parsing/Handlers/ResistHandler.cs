namespace EqLogParser.Infrastructure.Parsing.Handlers;

public sealed class ResistHandler(IResistLineParser parser, EncounterTracker tracker) : ILogLineHandler
{
    public bool Handle(EqLogLine line)
    {
        var result = parser.TryParse(line.Message);
        if (result is null) return false;
        tracker.AddResist(result.Value.MobName);
        return true;
    }

    public void Reset() { }
}
