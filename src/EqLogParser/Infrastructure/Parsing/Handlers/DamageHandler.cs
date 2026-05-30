namespace EqLogParser.Infrastructure.Parsing.Handlers;

public sealed class DamageHandler(IDamageLineParser parser, EncounterTracker tracker) : ILogLineHandler
{
    public bool Handle(EqLogLine line)
    {
        var ev = parser.TryParse(line.Message);
        if (ev is null) return false;
        tracker.AddDamage(ev.MobName, ev.Source, ev.Amount, ev.Kind, line.ParsedTimestamp, ev.SpellName);
        return true;
    }

    public void Reset() { }
}
