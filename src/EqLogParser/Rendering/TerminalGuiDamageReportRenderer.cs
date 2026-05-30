using System.Data;
using EqLogParser.Domain;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Rendering;

public sealed class TerminalGuiDamageReportRenderer
{
    public void Render(DamageReport report)
    {
        using IApplication app = Application.Create();
        app.Init();

        using var window = new Window
        {
            Title = "EQ Log Parser - Esc to quit"
        };

        var header = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 4,
            Text = BuildHeader(report)
        };

        var totalsFrame = new FrameView
        {
            Title = "Damage by mob",
            X = 0,
            Y = Pos.Bottom(header),
            Width = Dim.Percent(45),
            Height = Dim.Fill(1)
        };

        var killsFrame = new FrameView
        {
            Title = $"Individual kills ({report.Summary.Kills.Count:N0})",
            X = Pos.Right(totalsFrame),
            Y = Pos.Bottom(header),
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        var footer = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            Text = "Arrow keys/PageUp/PageDown scroll tables. Esc exits. Use --text for plain output."
        };

        totalsFrame.Add(CreateTableView(CreateMobTotalsTable(report.Summary.Mobs)));
        killsFrame.Add(CreateTableView(CreateKillsTable(report.Summary.Kills)));

        window.Add(header, totalsFrame, killsFrame, footer);
        app.Run(window);
    }

    private static TableView CreateTableView(DataTable table)
    {
        return new TableView(new DataTableSource(table))
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true
        };
    }

    private static string BuildHeader(DamageReport report)
    {
        var identity = report.Identity is null
            ? "Character: unknown    Server: unknown"
            : $"Character: {report.Identity.CharacterName}    Server: {report.Identity.ServerName}";

        return
            $"{identity}{Environment.NewLine}" +
            $"Total damage: {report.Summary.TotalDamage:N0}    Damage lines: {report.Summary.TotalHits:N0}    Mob groups: {report.Summary.Mobs.Count:N0}{Environment.NewLine}" +
            $"Log: {report.LogPath}";
    }

    private static DataTable CreateMobTotalsTable(IReadOnlyList<MobDamage> mobs)
    {
        var table = CreateTable("Mob", "Total", "Direct", "YOUR", "Hits");

        foreach (var mob in mobs)
        {
            table.Rows.Add(
                mob.Name,
                mob.TotalDamage,
                mob.DirectDamage,
                mob.YourEffectDamage,
                mob.Hits);
        }

        return table;
    }

    private static DataTable CreateKillsTable(IReadOnlyList<KillSummary> kills)
    {
        var table = CreateTable("Line", "Time", "Mob", "Total", "Direct", "YOUR", "Hits", "Killed by");

        foreach (var kill in kills)
        {
            table.Rows.Add(
                kill.LineNumber,
                kill.Timestamp,
                kill.Mob.Name,
                kill.Mob.TotalDamage,
                kill.Mob.DirectDamage,
                kill.Mob.YourEffectDamage,
                kill.Mob.Hits,
                kill.KilledBy);
        }

        return table;
    }

    private static DataTable CreateTable(params string[] columns)
    {
        var table = new DataTable();
        foreach (var column in columns)
        {
            table.Columns.Add(column);
        }

        return table;
    }
}
