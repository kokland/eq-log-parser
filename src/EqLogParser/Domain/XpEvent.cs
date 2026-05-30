namespace EqLogParser.Domain;

public sealed record XpEvent(
    int LineNumber,
    string Timestamp,
    double Percent,
    bool IsParty);
