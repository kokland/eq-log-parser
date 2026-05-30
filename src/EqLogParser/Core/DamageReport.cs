using EqLogParser.Core.Domain;

namespace EqLogParser.Core;

public sealed record DamageReport(
    string LogPath,
    LogIdentity? Identity,
    DamageSummary Summary,
    DateTimeOffset UpdatedAt);
