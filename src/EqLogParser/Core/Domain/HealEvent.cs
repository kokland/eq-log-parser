namespace EqLogParser.Core.Domain;

public sealed record HealEvent(
    int LineNumber,
    string Timestamp,
    string Target,
    int Amount,
    string SpellName);
