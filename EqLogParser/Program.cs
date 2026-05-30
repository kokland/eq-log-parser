using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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

var logIdentity = LogIdentity.FromPath(logPath);
var parser = new EqDamageParser();
var summary = parser.Parse(logPath);

if (summary.TotalHits == 0)
{
    Console.WriteLine("No outgoing player damage was found.");
    return 0;
}

Console.WriteLine($"Parsed damage from: {Path.GetFullPath(logPath)}");
if (logIdentity is not null)
{
    Console.WriteLine($"Character: {logIdentity.CharacterName}");
    Console.WriteLine($"Server: {logIdentity.ServerName}");
}

Console.WriteLine($"Total damage: {summary.TotalDamage:N0}");
Console.WriteLine($"Damage lines: {summary.TotalHits:N0}");
Console.WriteLine();

var mobWidth = Math.Max("Mob".Length, summary.Mobs.Max(mob => mob.Name.Length));
Console.WriteLine(
    $"{ "Mob".PadRight(mobWidth) }  { "Total",12 }  { "Direct",12 }  { "YOUR",12 }  { "Hits",8 }");
Console.WriteLine($"{new string('-', mobWidth)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 8)}");

foreach (var mob in summary.Mobs)
{
    Console.WriteLine(
        $"{mob.Name.PadRight(mobWidth)}  {mob.TotalDamage,12:N0}  {mob.DirectDamage,12:N0}  {mob.YourEffectDamage,12:N0}  {mob.Hits,8:N0}");
}

if (summary.Kills.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"Individual kills: {summary.Kills.Count:N0}");

    var killMobWidth = Math.Max("Mob".Length, summary.Kills.Max(kill => kill.Mob.Name.Length));
    var killedByWidth = Math.Max("Killed by".Length, summary.Kills.Max(kill => kill.KilledBy.Length));
    Console.WriteLine(
        $"{ "Line",8 }  { "Time",-24 }  { "Mob".PadRight(killMobWidth) }  { "Total",10 }  { "Direct",10 }  { "YOUR",8 }  { "Hits",6 }  { "Killed by".PadRight(killedByWidth) }");
    Console.WriteLine(
        $"{new string('-', 8)}  {new string('-', 24)}  {new string('-', killMobWidth)}  {new string('-', 10)}  {new string('-', 10)}  {new string('-', 8)}  {new string('-', 6)}  {new string('-', killedByWidth)}");

    foreach (var kill in summary.Kills)
    {
        Console.WriteLine(
            $"{kill.LineNumber,8:N0}  {kill.Timestamp,-24}  {kill.Mob.Name.PadRight(killMobWidth)}  {kill.Mob.TotalDamage,10:N0}  {kill.Mob.DirectDamage,10:N0}  {kill.Mob.YourEffectDamage,8:N0}  {kill.Mob.Hits,6:N0}  {kill.KilledBy.PadRight(killedByWidth)}");
    }
}

if (summary.OpenEncounters.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"Damage without a matching kill line: {summary.OpenEncounters.Count:N0}");

    var openMobWidth = Math.Max("Mob".Length, summary.OpenEncounters.Max(mob => mob.Name.Length));
    Console.WriteLine(
        $"{ "Mob".PadRight(openMobWidth) }  { "Total",12 }  { "Direct",12 }  { "YOUR",12 }  { "Hits",8 }");
    Console.WriteLine($"{new string('-', openMobWidth)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 8)}");

    foreach (var mob in summary.OpenEncounters)
    {
        Console.WriteLine(
            $"{mob.Name.PadRight(openMobWidth)}  {mob.TotalDamage,12:N0}  {mob.DirectDamage,12:N0}  {mob.YourEffectDamage,12:N0}  {mob.Hits,8:N0}");
    }
}

return 0;

internal sealed partial class EqDamageParser
{
    private static readonly StringComparer MobNameComparer = StringComparer.OrdinalIgnoreCase;

    public DamageSummary Parse(string path)
    {
        var totals = new Dictionary<string, MobDamage>(MobNameComparer);
        var activeEncounters = new Dictionary<string, MobDamage>(MobNameComparer);
        var kills = new List<KillSummary>();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;

            var message = GetMessage(line);
            var damage = TryParseDirectDamage(message) ?? TryParseYourEffectDamage(message);
            if (damage is not null)
            {
                var mobName = NormalizeMobName(damage.MobName);
                AddDamage(totals, mobName, damage.Amount, damage.Kind);
                AddDamage(activeEncounters, mobName, damage.Amount, damage.Kind);

                continue;
            }

            var kill = TryParseKill(message);
            if (kill is null)
            {
                continue;
            }

            var killedMobName = NormalizeMobName(kill.MobName);
            if (!activeEncounters.Remove(killedMobName, out var encounter))
            {
                continue;
            }

            kills.Add(new KillSummary(
                lineNumber,
                GetTimestamp(line),
                encounter,
                kill.KilledBy));
        }

        var mobs = totals.Values
            .OrderByDescending(mob => mob.TotalDamage)
            .ThenBy(mob => mob.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DamageSummary(
            mobs,
            kills,
            activeEncounters.Values
                .OrderByDescending(mob => mob.TotalDamage)
                .ThenBy(mob => mob.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            mobs.Sum(mob => mob.TotalDamage),
            mobs.Sum(mob => mob.Hits));
    }

    private static void AddDamage(
        Dictionary<string, MobDamage> totals,
        string mobName,
        int amount,
        DamageKind kind)
    {
        ref var mob = ref CollectionsMarshal.GetValueRefOrAddDefault(totals, mobName, out var exists);
        if (!exists)
        {
            mob = new MobDamage(mobName);
        }

        mob!.Add(amount, kind);
    }

    private static DamageEvent? TryParseDirectDamage(string message)
    {
        var match = DirectDamageRegex().Match(message);
        if (!match.Success)
        {
            return null;
        }

        return new DamageEvent(
            match.Groups["mob"].Value,
            int.Parse(match.Groups["damage"].Value, CultureInfo.InvariantCulture),
            DamageKind.Direct);
    }

    private static DamageEvent? TryParseYourEffectDamage(string message)
    {
        var match = YourEffectDamageRegex().Match(message);
        if (!match.Success)
        {
            return null;
        }

        return new DamageEvent(
            match.Groups["mob"].Value,
            int.Parse(match.Groups["damage"].Value, CultureInfo.InvariantCulture),
            DamageKind.YourEffect);
    }

    private static KillEvent? TryParseKill(string message)
    {
        var youMatch = YouSlainRegex().Match(message);
        if (youMatch.Success)
        {
            return new KillEvent(youMatch.Groups["mob"].Value, "You");
        }

        var slainMatch = SlainByRegex().Match(message);
        if (slainMatch.Success)
        {
            return new KillEvent(
                slainMatch.Groups["mob"].Value,
                slainMatch.Groups["killer"].Value);
        }

        var struckDownMatch = StruckDownRegex().Match(message);
        if (struckDownMatch.Success)
        {
            return new KillEvent(
                struckDownMatch.Groups["mob"].Value,
                struckDownMatch.Groups["killer"].Value);
        }

        return null;
    }

    private static string GetMessage(string line)
    {
        var messageStart = line.IndexOf("] ", StringComparison.Ordinal);
        return messageStart >= 0 ? line[(messageStart + 2)..] : line;
    }

    private static string GetTimestamp(string line)
    {
        if (line.Length == 0 || line[0] != '[')
        {
            return string.Empty;
        }

        var timestampEnd = line.IndexOf(']', StringComparison.Ordinal);
        return timestampEnd > 1 ? line[1..timestampEnd] : string.Empty;
    }

    private static string NormalizeMobName(string mobName)
    {
        mobName = mobName.Trim();
        if (mobName.Length < 2)
        {
            return mobName;
        }

        return char.ToUpperInvariant(mobName[0]) + mobName[1..];
    }

    [GeneratedRegex(
        @"^You\s+\S+\s+(?<mob>.+?)\s+for\s+(?<damage>\d+)\s+points\s+of\s+(?:(?:\S+)\s+)?damage(?:\s+by\s+.+?)?(?:\.|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex DirectDamageRegex();

    [GeneratedRegex(
        @"^(?<mob>.+?)\s+is\s+.+?\s+by\s+YOUR\s+.+?\s+for\s+(?<damage>\d+)\s+points\s+of\s+(?:(?:\S+)\s+)?damage(?:\.|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex YourEffectDamageRegex();

    [GeneratedRegex(
        @"^You have slain (?<mob>.+)!$",
        RegexOptions.CultureInvariant)]
    private static partial Regex YouSlainRegex();

    [GeneratedRegex(
        @"^(?<mob>.+?) has been slain by (?<killer>.+)!$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SlainByRegex();

    [GeneratedRegex(
        @"^(?<mob>.+?) has been struck down by (?<killer>.+)\.$",
        RegexOptions.CultureInvariant)]
    private static partial Regex StruckDownRegex();
}

internal sealed record DamageSummary(
    IReadOnlyList<MobDamage> Mobs,
    IReadOnlyList<KillSummary> Kills,
    IReadOnlyList<MobDamage> OpenEncounters,
    long TotalDamage,
    long TotalHits);

internal sealed record KillSummary(
    int LineNumber,
    string Timestamp,
    MobDamage Mob,
    string KilledBy);

internal sealed partial record LogIdentity(string CharacterName, string ServerName)
{
    public static LogIdentity? FromPath(string path)
    {
        var fileName = Path.GetFileName(path);
        var match = FileNameRegex().Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        return new LogIdentity(
            match.Groups["character"].Value,
            match.Groups["server"].Value);
    }

    [GeneratedRegex(
        @"^eqlog_(?<character>[^_]+)_(?<server>.+)\.txt$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FileNameRegex();
}

internal sealed class MobDamage(string name)
{
    public string Name { get; } = name;
    public long DirectDamage { get; private set; }
    public long YourEffectDamage { get; private set; }
    public long TotalDamage => DirectDamage + YourEffectDamage;
    public long Hits { get; private set; }

    public void Add(int damage, DamageKind kind)
    {
        Hits++;

        if (kind == DamageKind.Direct)
        {
            DirectDamage += damage;
            return;
        }

        YourEffectDamage += damage;
    }
}

internal sealed record DamageEvent(string MobName, int Amount, DamageKind Kind);

internal sealed record KillEvent(string MobName, string KilledBy);

internal enum DamageKind
{
    Direct,
    YourEffect
}
