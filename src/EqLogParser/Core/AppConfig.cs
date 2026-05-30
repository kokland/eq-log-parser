namespace EqLogParser.Core;

public sealed class AppConfig
{
    public int  WatchIntervalSeconds { get; set; } = 30;
    public bool ShowTotals           { get; set; } = true;
    public bool ShowKills            { get; set; } = true;
    public bool ShowLoot             { get; set; } = true;
    public bool ShowSessions         { get; set; } = true;
    public bool ShowXp               { get; set; } = true;
}
