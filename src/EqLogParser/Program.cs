using EqLogParser.Parsing;
using EqLogParser.Rendering;

if (args.Length is 0 or > 1)
{
    Console.Error.WriteLine("Usage: EqLogParser <path-to-eq-log>");
    return 1;
}

var logPath = args[0];
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
var renderer = new ConsoleDamageReportRenderer();

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

renderer.Render(report, Console.Out);
return 0;
