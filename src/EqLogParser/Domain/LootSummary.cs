namespace EqLogParser.Domain;

/// <summary>Loot event enriched with the kill it is linked to (if any).</summary>
public sealed record LootSummary(
    int LineNumber,
    string Timestamp,
    string ItemName,
    string MobName,
    bool AutoSold,
    int? KillLineNumber);
