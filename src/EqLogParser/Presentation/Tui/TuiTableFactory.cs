using System.Data;
using EqLogParser.Core;
using EqLogParser.Core.Domain;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Presentation.Tui;

/// <summary>Factory methods that build <see cref="DataTable"/> objects and
/// <see cref="TableView"/> wrappers for all TUI panels and dialogs.</summary>
public static class TuiTableFactory
{
    // -------------------------------------------------------------------------
    // TableView wrapper
    // -------------------------------------------------------------------------

    public static TableView CreateTableView(DataTable table) =>
        new(new DataTableSource(table))
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), FullRowSelect = true
        };

    // -------------------------------------------------------------------------
    // Panel tables
    // -------------------------------------------------------------------------

    public static DataTable CreateMobTotalsTable(IReadOnlyList<MobDamage> mobs)
    {
        var table = CreateTable("Mob", "Total", "Direct", "YOUR", "Hits", "Resists", "Misses");
        foreach (var mob in mobs)
            table.Rows.Add(mob.Name, mob.TotalDamage, mob.DirectDamage, mob.YourEffectDamage,
                           mob.Hits, mob.Resists, mob.Misses);
        return table;
    }

    public static DataTable CreateKillsTable(IReadOnlyList<KillSummary> kills)
    {
        var table = CreateTable("Line", "Time", "Mob", "Total", "Direct", "YOUR", "Hits", "DPS", "Killed by");
        foreach (var kill in kills)
            table.Rows.Add(
                kill.LineNumber, kill.Timestamp, kill.Mob.Name,
                kill.Mob.TotalDamage, kill.Mob.DirectDamage, kill.Mob.YourEffectDamage,
                kill.Mob.Hits,
                kill.Dps.HasValue ? $"{kill.Dps.Value:N0}" : "",
                kill.KilledBy);
        return table;
    }

    public static DataTable CreateSessionsTable(IReadOnlyList<SessionSummary> sessions)
    {
        var table = CreateTable("#", "Start", "End", "Duration", "Zone", "Kills", "Deaths", "Loot", "Damage", "DPS", "XP%");
        foreach (var s in sessions)
            table.Rows.Add(
                s.Number,
                s.StartTime.ToString("MM/dd HH:mm"),
                s.EndTime.ToString("MM/dd HH:mm"),
                FormatDuration(s.Duration),
                s.Zone ?? "",
                s.KillCount,
                s.Deaths,
                s.LootCount,
                s.TotalDamage,
                s.Dps > 0 ? $"{s.Dps:N0}" : "",
                $"{s.XpPercent:F3}%");
        return table;
    }

    public static DataTable CreateXpTable(IEnumerable<XpEvent> events)
    {
        var table = CreateTable("Line", "Time", "XP%", "Type", "Progress");
        var list = events.ToList();

        var levelAfter    = new int[list.Count];
        var progressAfter = new double[list.Count];
        var leveledUp     = new bool[list.Count];

        double acc = 0; int lv = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            int prev = lv;
            acc += list[i].Percent;
            while (acc >= 100.0) { lv++; acc -= 100.0; }
            levelAfter[i]    = lv;
            progressAfter[i] = acc;
            leveledUp[i]     = lv > prev;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var ev   = list[i];
            var type = (ev.IsParty ? "party" : "solo") + (leveledUp[i] ? " *** LEVEL UP" : "");
            table.Rows.Add(ev.LineNumber, ev.Timestamp, $"{ev.Percent:F3}%", type,
                           $"Lv {levelAfter[i]}  {progressAfter[i]:F3}%");
        }
        return table;
    }

    public static DataTable CreateLootTable(IEnumerable<LootSummary> loot)
    {
        var table = CreateTable("Line", "Time", "Item", "Mob", "Sold?", "Kill#");
        foreach (var l in loot)
            table.Rows.Add(
                l.LineNumber, l.Timestamp, l.ItemName, l.MobName,
                l.AutoSold ? "sold" : "",
                l.KillLineNumber.HasValue ? l.KillLineNumber.Value.ToString() : "");
        return table;
    }

    // -------------------------------------------------------------------------
    // Dialog-specific tables
    // -------------------------------------------------------------------------

    public static DataTable CreateSourceBreakdownTable(IReadOnlyList<SourceDamageSnapshot> sources, long total)
    {
        var table = CreateTable("Source", "Total", "Direct", "Effect", "Hits", "%");
        foreach (var src in sources)
        {
            var pct = total > 0 ? $"{src.TotalDamage * 100.0 / total:F1}%" : "0.0%";
            table.Rows.Add(src.Source, src.TotalDamage, src.DirectDamage, src.EffectDamage, src.Hits, pct);
        }
        return table;
    }

    public static DataTable CreateSpellBreakdownTable(IReadOnlyList<(string Spell, long Damage)> spells, long totalMobDamage)
    {
        var table = CreateTable("Spell", "Damage", "%");
        foreach (var (spell, dmg) in spells)
        {
            var pct = totalMobDamage > 0 ? $"{dmg * 100.0 / totalMobDamage:F1}%" : "0.0%";
            table.Rows.Add(spell, dmg, pct);
        }
        return table;
    }

    public static DataTable CreateMobSessionTable(IEnumerable<(string Mob, long Damage, int Kills, double Dps)> rows)
    {
        var table = CreateTable("Mob", "Damage", "Kills", "Avg DPS");
        foreach (var (mob, dmg, kills, dps) in rows)
            table.Rows.Add(mob, dmg, kills, dps > 0 ? $"{dps:N0}" : "");
        return table;
    }

    // -------------------------------------------------------------------------
    // Header / title helpers
    // -------------------------------------------------------------------------

    public static string BuildHeader(DamageReport report)
    {
        var s = report.Summary;
        var identity = report.Identity is null
            ? "Character: unknown    Server: unknown"
            : $"Character: {report.Identity.CharacterName}    Server: {report.Identity.ServerName}";

        var xpLine   = s.Xp.Count == 0 ? "XP: none" : FormatXpSummary(s.Xp);
        var healStr  = s.TotalHealing > 0 ? $"    Healing: {s.TotalHealing:N0}" : "";
        var deathStr = s.Deaths.Count > 0 ? $"    Deaths: {s.Deaths.Count}" : "";

        return
            $"{identity}{Environment.NewLine}" +
            $"Total damage: {s.TotalDamage:N0}    Hits: {s.TotalHits:N0}    Mobs: {s.Mobs.Count:N0}" +
            $"    Sessions: {s.Sessions.Count}{healStr}{deathStr}{Environment.NewLine}" +
            $"Updated: {report.UpdatedAt:yyyy-MM-dd HH:mm:ss zzz}    Log: {report.LogPath}{Environment.NewLine}" +
            xpLine;
    }

    public static string XpFrameTitle(IReadOnlyList<XpEvent> xp)
    {
        if (xp.Count == 0) return "XP gains (none)";
        var (levels, progress) = ComputeXpProgress(xp);
        return $"XP gains ({xp.Count:N0})    {levels} level{(levels == 1 ? "" : "s")} + {progress:F3}%";
    }

    public static string SessionsFrameTitle(IReadOnlyList<SessionSummary> sessions) =>
        sessions.Count == 0 ? "Sessions (none)" : $"Sessions ({sessions.Count})";

    public static (int Levels, double Progress) ComputeXpProgress(IEnumerable<XpEvent> xp)
    {
        double acc = 0; int levels = 0;
        foreach (var ev in xp.OrderBy(x => x.LineNumber))
        {
            acc += ev.Percent;
            while (acc >= 100.0) { levels++; acc -= 100.0; }
        }
        return (levels, acc);
    }

    public static string FormatDuration(TimeSpan ts) =>
        ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m" : $"{ts.Minutes}m {ts.Seconds:D2}s";

    public static string FormatXpSummary(IReadOnlyList<XpEvent> xp)
    {
        var (levels, progress) = ComputeXpProgress(xp);
        var solo  = xp.Where(x => !x.IsParty).Sum(x => x.Percent);
        var party = xp.Where(x =>  x.IsParty).Sum(x => x.Percent);
        var levelStr = levels == 1 ? "1 level" : $"{levels} levels";
        return $"XP: {levelStr} + {progress:F3}% toward next    " +
               $"(solo: {solo:F3}% / {xp.Count(x => !x.IsParty)} gains    " +
               $"party: {party:F3}% / {xp.Count(x => x.IsParty)} gains)";
    }

    // -------------------------------------------------------------------------
    // Internal helper
    // -------------------------------------------------------------------------

    public static DataTable CreateTable(params string[] columns)
    {
        var table = new DataTable();
        foreach (var col in columns) table.Columns.Add(col);
        return table;
    }
}
