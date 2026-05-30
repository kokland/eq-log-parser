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

public sealed class ConfigStore : IConfigStore
{
    private readonly string _configPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public ConfigStore(string configPath)
    {
        _configPath = configPath;
    }

    /// <summary>Creates a store rooted in the current working directory.</summary>
    public static ConfigStore Default() =>
        new(Path.Combine(Directory.GetCurrentDirectory(), "eqparser.json"));

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
        }
        catch
        {
            // Ignore corrupt / unreadable config; use defaults.
        }

        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Saving is best-effort; never crash the app.
        }
    }
}
