using EqLogParser.Core.Domain;

namespace EqLogParser.Infrastructure.Parsing.Handlers;

public sealed class HealHandler(IHealLineParser parser) : ILogLineHandler
{
    private readonly List<HealEvent> _events = [];
    public IReadOnlyList<HealEvent> Events => _events;

    public bool Handle(EqLogLine line)
    {
        var ev = parser.TryParse(line.Message);
        if (ev is null) return false;
        _events.Add(ev with { LineNumber = line.Number, Timestamp = line.Timestamp });
        return true;
    }

    public void Reset() => _events.Clear();
}
