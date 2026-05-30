using System.CommandLine;
using EqLogParser.Core;
using EqLogParser.Infrastructure.Config;
using EqLogParser.Infrastructure.Parsing;
using EqLogParser.Presentation.Console;
using EqLogParser.Presentation.Tui;

var logFileArgument = new Argument<FileInfo>("log-file")
{
    Description = "EverQuest log file named like eqlog_CharacterName_ServerName.txt"
};

var textOption = new Option<bool>("--text", "--no-tui")
{
    Description = "Write a plain text report instead of opening the Terminal.Gui interface."
};

var watchOption = new Option<bool>("--watch", "-w")
{
    Description = "Keep refreshing the report until interrupted."
};

var intervalOption = new Option<int>("--interval", "-i")
{
    Description = "Refresh interval in seconds when --watch is enabled.",
    DefaultValueFactory = _ => 30
};

var rootCommand = new RootCommand("Parse EverQuest logs and report outgoing damage by mob and kill.");
rootCommand.Arguments.Add(logFileArgument);
rootCommand.Options.Add(textOption);
rootCommand.Options.Add(watchOption);
rootCommand.Options.Add(intervalOption);

rootCommand.SetAction(parseResult =>
{
    var logFile         = parseResult.GetValue(logFileArgument);
    var useTextReport   = parseResult.GetValue(textOption);
    var watch           = parseResult.GetValue(watchOption);
    var intervalSeconds = parseResult.GetValue(intervalOption);

    return Run(logFile!, useTextReport, watch, TimeSpan.FromSeconds(intervalSeconds));
});

return rootCommand.Parse(args).Invoke();

static int Run(FileInfo logFile, bool useTextReport, bool watch, TimeSpan refreshInterval)
{
    var logPath = logFile.FullName;
    if (!logFile.Exists)
    {
        Console.Error.WriteLine($"Log file not found: {logPath}");
        return 1;
    }

    if (watch && refreshInterval <= TimeSpan.Zero)
    {
        Console.Error.WriteLine("Refresh interval must be greater than zero seconds.");
        return 1;
    }

    var parser         = new EqDamageParser(new DamageLineParser(), new KillLineParser(), new MobNameNormalizer());
    var identityParser = new LogIdentityParser();

    DamageReport CreateReport() =>
        new(Path.GetFullPath(logPath), identityParser.TryParse(logPath), parser.Parse(logPath), DateTimeOffset.Now);

    var report = CreateReport();
    if (!watch && report.Summary.TotalHits == 0)
    {
        Console.WriteLine("No outgoing player damage was found.");
        return 0;
    }

    IDamageReportRenderer renderer = (useTextReport || !CanRunTerminalUi())
        ? new ConsoleDamageReportRenderer()
        : new TerminalGuiDamageReportRenderer(ConfigStore.Default().Load());

    if (watch)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        renderer.RenderWatch(report, CreateReport, refreshInterval, cts.Token);
    }
    else
    {
        renderer.Render(report);
    }

    return 0;
}

static bool CanRunTerminalUi() =>
    !Console.IsInputRedirected && !Console.IsOutputRedirected;
