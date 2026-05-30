using EqLogParser.Domain;

namespace EqLogParser.Parsing.Handlers;

public sealed class LootHandler(ILootLineParser parser) : ILogLineHandler
{
    private readonly List<LootEvent> _events = [];
    public IReadOnlyList<LootEvent> Events => _events;

    public bool Handle(EqLogLine line)
    {
        var ev = parser.TryParse(line.Message);
        if (ev is null) return false;
        _events.Add(ev with { LineNumber = line.Number, Timestamp = line.Timestamp });
        return true;
    }

    public void Reset() => _events.Clear();
}
