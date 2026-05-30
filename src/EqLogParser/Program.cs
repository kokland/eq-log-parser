using System.CommandLine;
using EqLogParser.Core;
using EqLogParser.Infrastructure.Config;
using EqLogParser.Infrastructure.Parsing;
using EqLogParser.Presentation.Cli;
using EqLogParser.Presentation.Tui;

var logFileArgument = new Argument<FileInfo?>("log-file")
{
    Description = "EverQuest log file named like eqlog_CharacterName_ServerName.txt",
    Arity       = ArgumentArity.ZeroOrOne,
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

    return Run(logFile, useTextReport, watch, TimeSpan.FromSeconds(intervalSeconds));
});

return rootCommand.Parse(args).Invoke();

static int Run(FileInfo? logFile, bool useTextReport, bool watch, TimeSpan refreshInterval)
{
    var store  = ConfigStore.Default();
    var config = store.Load();

    // ---- resolve log file path ----
    string? logPath = null;

    if (logFile is not null)
    {
        logPath = logFile.FullName;
    }
    else if (!useTextReport && CanRunTerminalUi())
    {
        // No file on the command line — check whether there is a remembered file.
        if (config.LastLogPath is not null && File.Exists(config.LastLogPath))
        {
            // Ask the user whether to resume or open a different file.
            bool resume = LogFilePicker.AskResume(config.LastLogPath);
            logPath = resume
                ? config.LastLogPath
                : LogFilePicker.PickStandalone(config.LastLogPath);
        }
        else
        {
            logPath = LogFilePicker.PickStandalone();
        }

        if (logPath is null)
            return 0; // user cancelled
    }
    else
    {
        Console.Error.WriteLine("No log file specified.");
        return 1;
    }

    if (!File.Exists(logPath))
    {
        Console.Error.WriteLine($"Log file not found: {logPath}");
        return 1;
    }

    if (watch && refreshInterval <= TimeSpan.Zero)
    {
        Console.Error.WriteLine("Refresh interval must be greater than zero seconds.");
        return 1;
    }

    // Persist the chosen file so we can offer to resume it next time.
    config.LastLogPath = logPath;
    store.Save(config);

    // fileLoader creates a fresh parser per file — incremental parser state is per-file.
    (DamageReport Initial, Func<DamageReport>? Refresh) LoadFile(string path)
    {
        // Persist the newly opened file every time the user switches via O key.
        config.LastLogPath = path;
        store.Save(config);

        var p  = new EqDamageParser(new DamageLineParser(), new KillLineParser(), new MobNameNormalizer());
        var ip = new LogIdentityParser();
        DamageReport Make() => new(Path.GetFullPath(path), ip.TryParse(path), p.Parse(path), DateTimeOffset.Now);
        return (Make(), watch ? Make : null);
    }

    var (initialReport, refreshFn) = LoadFile(logPath);

    if (!watch && initialReport.Summary.TotalHits == 0)
    {
        Console.WriteLine("No outgoing player damage was found.");
        return 0;
    }

    if (useTextReport || !CanRunTerminalUi())
    {
        IDamageReportRenderer textRenderer = new ConsoleDamageReportRenderer();
        if (watch && refreshFn is not null)
        {
            using var cts2 = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts2.Cancel(); };
            textRenderer.RenderWatch(initialReport, refreshFn, refreshInterval, cts2.Token);
        }
        else
        {
            textRenderer.Render(initialReport);
        }
        return 0;
    }

    var renderer = new TerminalGuiDamageReportRenderer(config, LoadFile);

    if (watch && refreshFn is not null)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        renderer.RenderWatch(initialReport, refreshFn, refreshInterval, cts.Token);
    }
    else
    {
        renderer.Render(initialReport);
    }

    return 0;
}

static bool CanRunTerminalUi() =>
    !Console.IsInputRedirected && !Console.IsOutputRedirected;
