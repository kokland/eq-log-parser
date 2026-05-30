namespace EqLogParser.Core.Domain;

/// <summary>Raw loot event extracted from a single log line.</summary>
public sealed record LootEvent(
    int LineNumber,
    string Timestamp,
    string ItemName,
    string MobName,
    bool AutoSold);
