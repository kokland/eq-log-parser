using EqLogParser.Parsing;
using EqLogParser.Rendering;

var options = CommandLineOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: EqLogParser [--text|--no-tui] <path-to-eq-log>");
    return 1;
}

var logPath = options.LogPath;
if (!File.Exists(logPath))
{
    Console.Error.WriteLine($"Log file not found: {logPath}");
    return 1;
}

var parser = new EqDamageParser(
    new DamageLineParser(),
    new KillLineParser(),
    new MobNameNormalizer());

var identityParser = new LogIdentityParser();

var summary = parser.Parse(logPath);
if (summary.TotalHits == 0)
{
    Console.WriteLine("No outgoing player damage was found.");
    return 0;
}

var report = new DamageReport(
    Path.GetFullPath(logPath),
    identityParser.TryParse(logPath),
    summary);

if (options.UseTextReport || !CanRunTerminalUi())
{
    new ConsoleDamageReportRenderer().Render(report, Console.Out);
    return 0;
}

new TerminalGuiDamageReportRenderer().Render(report);
return 0;

static bool CanRunTerminalUi()
{
    return !Console.IsInputRedirected && !Console.IsOutputRedirected;
}

internal sealed record CommandLineOptions(string LogPath, bool UseTextReport)
{
    public static CommandLineOptions? Parse(string[] args)
    {
        if (args.Length is 0 or > 2)
        {
            return null;
        }

        var useTextReport = false;
        string? logPath = null;

        foreach (var arg in args)
        {
            if (arg is "--text" or "--no-tui")
            {
                useTextReport = true;
                continue;
            }

            if (logPath is not null)
            {
                return null;
            }

            logPath = arg;
        }

        return logPath is null
            ? null
            : new CommandLineOptions(logPath, useTextReport);
    }
}
