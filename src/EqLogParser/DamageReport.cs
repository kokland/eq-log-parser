using EqLogParser.Domain;

namespace EqLogParser;

public sealed record DamageReport(
    string LogPath,
    LogIdentity? Identity,
    DamageSummary Summary,
    DateTimeOffset UpdatedAt);
