using EqLogParser.Domain;

namespace EqLogParser.Parsing.Handlers;

public sealed class DeathHandler(IDeathLineParser parser) : ILogLineHandler
{
    private readonly List<DeathEvent> _events = [];
    public IReadOnlyList<DeathEvent> Events => _events;

    public bool Handle(EqLogLine line)
    {
        var killer = parser.TryParse(line.Message);
        if (killer is null) return false;
        _events.Add(new DeathEvent(line.Number, line.Timestamp, killer));
        return true;
    }

    public void Reset() => _events.Clear();
}
