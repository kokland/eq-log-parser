using System.Text.Json;
using System.Text.Json.Serialization;

namespace EqLogParser;

public sealed class AppConfig
{
    public int  WatchIntervalSeconds { get; set; } = 30;
    public bool ShowTotals           { get; set; } = true;
    public bool ShowKills            { get; set; } = true;
    public bool ShowLoot             { get; set; } = true;
    public bool ShowSessions         { get; set; } = true;
    public bool ShowXp               { get; set; } = true;
}

public static class ConfigStore
{
    private static readonly string ConfigPath =
        Path.Combine(Directory.GetCurrentDirectory(), "eqparser.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
        }
        catch
        {
            // Ignore corrupt / unreadable config; use defaults.
        }

        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Saving is best-effort; never crash the app.
        }
    }
}
