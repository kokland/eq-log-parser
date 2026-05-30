using System.CommandLine;
using EqLogParser.Parsing;
using EqLogParser.Rendering;

var logFileArgument = new Argument<FileInfo>("log-file")
{
    Description = "EverQuest log file named like eqlog_CharacterName_ServerName.txt"
};

var textOption = new Option<bool>("--text", "--no-tui")
{
    Description = "Write a plain text report instead of opening the Terminal.Gui interface."
};

var rootCommand = new RootCommand("Parse EverQuest logs and report outgoing damage by mob and kill.");
rootCommand.Arguments.Add(logFileArgument);
rootCommand.Options.Add(textOption);

rootCommand.SetAction(parseResult =>
{
    var logFile = parseResult.GetValue(logFileArgument);
    var useTextReport = parseResult.GetValue(textOption);

    return Run(logFile!, useTextReport);
});

return rootCommand.Parse(args).Invoke();

static int Run(FileInfo logFile, bool useTextReport)
{
    var logPath = logFile.FullName;
    if (!logFile.Exists)
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

    if (useTextReport || !CanRunTerminalUi())
    {
        new ConsoleDamageReportRenderer().Render(report, Console.Out);
        return 0;
    }

    new TerminalGuiDamageReportRenderer().Render(report);
    return 0;
}

static bool CanRunTerminalUi()
{
    return !Console.IsInputRedirected && !Console.IsOutputRedirected;
}
