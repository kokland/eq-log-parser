using EqLogParser.Core;
using EqLogParser.Core.Domain;

namespace EqLogParser.Presentation.Cli;

public sealed class ConsoleDamageReportRenderer(TextWriter? output = null) : IDamageReportRenderer
{
    private readonly TextWriter _output = output ?? Console.Out;

    public void Render(DamageReport report)
    {
        WriteHeader(report, _output);
        WriteMobTotals(report.Summary.Mobs, _output);
        WriteKills(report.Summary.Kills, _output);
        WriteOpenEncounters(report.Summary.OpenEncounters, _output);
    }

    public void RenderWatch(
        DamageReport       initialReport,
        Func<DamageReport> refresh,
        TimeSpan           interval,
        CancellationToken  cancellationToken = default)
    {
        Render(initialReport);

        while (!cancellationToken.IsCancellationRequested)
        {
            _output.WriteLine();
            _output.WriteLine($"Watching. Next refresh in {interval.TotalSeconds:N0}s. Press Ctrl+C to stop.");

            if (cancellationToken.WaitHandle.WaitOne(interval))
                break;

            if (!Console.IsOutputRedirected)
                Console.Clear();

            Render(refresh());
        }
    }

    // -------------------------------------------------------------------------

    private static void WriteHeader(DamageReport report, TextWriter output)
    {
        output.WriteLine($"Parsed damage from: {report.LogPath}");
        if (report.Identity is not null)
        {
            output.WriteLine($"Character: {report.Identity.CharacterName}");
            output.WriteLine($"Server: {report.Identity.ServerName}");
        }

        output.WriteLine($"Total damage: {report.Summary.TotalDamage:N0}");
        output.WriteLine($"Damage lines: {report.Summary.TotalHits:N0}");
        output.WriteLine($"Updated: {report.UpdatedAt:yyyy-MM-dd HH:mm:ss zzz}");
        output.WriteLine();
    }

    private static void WriteMobTotals(IReadOnlyList<MobDamage> mobs, TextWriter output)
    {
        if (mobs.Count == 0)
        {
            output.WriteLine("No outgoing player damage was found.");
            return;
        }

        var mobWidth = Math.Max("Mob".Length, mobs.Max(mob => mob.Name.Length));
        WriteDamageTableHeader(mobWidth, output, totalWidth: 12, directWidth: 12, yourWidth: 12, hitsWidth: 8);

        foreach (var mob in mobs)
        {
            output.WriteLine(
                $"{mob.Name.PadRight(mobWidth)}  {mob.TotalDamage,12:N0}  {mob.DirectDamage,12:N0}  {mob.YourEffectDamage,12:N0}  {mob.Hits,8:N0}");
        }
    }

    private static void WriteKills(IReadOnlyList<KillSummary> kills, TextWriter output)
    {
        if (kills.Count == 0) return;

        output.WriteLine();
        output.WriteLine($"Individual kills: {kills.Count:N0}");

        var mobWidth = Math.Max("Mob".Length, kills.Max(kill => kill.Mob.Name.Length));
        var killedByWidth = Math.Max("Killed by".Length, kills.Max(kill => kill.KilledBy.Length));
        output.WriteLine(
            $"{ "Line",8 }  { "Time",-24 }  { "Mob".PadRight(mobWidth) }  { "Total",10 }  { "Direct",10 }  { "YOUR",8 }  { "Hits",6 }  { "Killed by".PadRight(killedByWidth) }");
        output.WriteLine(
            $"{new string('-', 8)}  {new string('-', 24)}  {new string('-', mobWidth)}  {new string('-', 10)}  {new string('-', 10)}  {new string('-', 8)}  {new string('-', 6)}  {new string('-', killedByWidth)}");

        foreach (var kill in kills)
        {
            output.WriteLine(
                $"{kill.LineNumber,8:N0}  {kill.Timestamp,-24}  {kill.Mob.Name.PadRight(mobWidth)}  {kill.Mob.TotalDamage,10:N0}  {kill.Mob.DirectDamage,10:N0}  {kill.Mob.YourEffectDamage,8:N0}  {kill.Mob.Hits,6:N0}  {kill.KilledBy.PadRight(killedByWidth)}");
        }
    }

    private static void WriteOpenEncounters(IReadOnlyList<MobDamage> openEncounters, TextWriter output)
    {
        if (openEncounters.Count == 0) return;

        output.WriteLine();
        output.WriteLine($"Damage without a matching kill line: {openEncounters.Count:N0}");

        var mobWidth = Math.Max("Mob".Length, openEncounters.Max(mob => mob.Name.Length));
        WriteDamageTableHeader(mobWidth, output, totalWidth: 12, directWidth: 12, yourWidth: 12, hitsWidth: 8);

        foreach (var mob in openEncounters)
        {
            output.WriteLine(
                $"{mob.Name.PadRight(mobWidth)}  {mob.TotalDamage,12:N0}  {mob.DirectDamage,12:N0}  {mob.YourEffectDamage,12:N0}  {mob.Hits,8:N0}");
        }
    }

    private static void WriteDamageTableHeader(
        int mobWidth, TextWriter output,
        int totalWidth, int directWidth, int yourWidth, int hitsWidth)
    {
        output.WriteLine(
            $"{ "Mob".PadRight(mobWidth) }  { "Total".PadLeft(totalWidth) }  { "Direct".PadLeft(directWidth) }  { "YOUR".PadLeft(yourWidth) }  { "Hits".PadLeft(hitsWidth) }");
        output.WriteLine(
            $"{new string('-', mobWidth)}  {new string('-', totalWidth)}  {new string('-', directWidth)}  {new string('-', yourWidth)}  {new string('-', hitsWidth)}");
    }
}
