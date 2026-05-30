using EqLogParser.Core.Domain;

namespace EqLogParser.Infrastructure.Parsing.Handlers;

public sealed class ZoneHandler(IZoneLineParser parser) : ILogLineHandler
{
    private readonly List<ZoneEvent> _events = [];
    public IReadOnlyList<ZoneEvent> Events => _events;

    public bool Handle(EqLogLine line)
    {
        var zone = parser.TryParse(line.Message);
        if (zone is null) return false;
        _events.Add(new ZoneEvent(line.Number, line.Timestamp, zone));
        return true;
    }

    public void Reset() => _events.Clear();
}
