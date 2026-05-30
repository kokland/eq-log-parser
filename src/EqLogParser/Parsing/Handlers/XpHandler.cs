using EqLogParser.Domain;

namespace EqLogParser.Parsing.Handlers;

public sealed class XpHandler(IXpLineParser parser) : ILogLineHandler
{
    private readonly List<XpEvent> _events = [];
    public IReadOnlyList<XpEvent> Events => _events;

    public bool Handle(EqLogLine line)
    {
        var ev = parser.TryParse(line.Message);
        if (ev is null) return false;
        _events.Add(ev with { LineNumber = line.Number, Timestamp = line.Timestamp });
        return true;
    }

    public void Reset() => _events.Clear();
}
