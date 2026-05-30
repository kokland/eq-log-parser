using EqLogParser.Domain;

namespace EqLogParser.Rendering;

public sealed record DamageReport(
    string LogPath,
    LogIdentity? Identity,
    DamageSummary Summary);
