namespace EqLogParser.Domain;

/// <summary>
/// Aggregated stats for a single play session (contiguous activity block).
/// Sessions are split when consecutive log lines have a gap larger than the
/// configured idle threshold (default 30 minutes).
/// </summary>
public sealed record SessionSummary(
    int      Number,
    int      StartLine,
    int      EndLine,
    DateTime StartTime,
    DateTime EndTime,
    int      KillCount,
    int      LootCount,
    long     TotalDamage,
    double   XpPercent,
    int      Deaths,
    int      Resists,
    int      Misses,
    string?  Zone,
    long     TotalHealing,
    double   Dps)
{
    public TimeSpan Duration => EndTime - StartTime;
}
