using System.Data;
using EqLogParser.Domain;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Rendering;

public sealed class TerminalGuiDamageReportRenderer
{
    public void Render(DamageReport report)
    {
        Render(report, refreshReport: null, refreshInterval: null);
    }

    public void Render(
        DamageReport report,
        Func<DamageReport>? refreshReport,
        TimeSpan? refreshInterval)
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
            Text = "Tab switches panels. Arrow keys/PageUp/PageDown scroll tables. Esc exits. Use --text for plain output."
        };

        var totalsTable = CreateTableView(CreateMobTotalsTable(report.Summary.Mobs));
        var killsTable = CreateTableView(CreateKillsTable(report.Summary.Kills));

        totalsFrame.Add(totalsTable);
        killsFrame.Add(killsTable);

        // Tab moves focus between the left and right panels.
        // +/- adjust the live-refresh interval (watch mode only).
        object? timeoutToken = null;
        var currentInterval = refreshInterval ?? TimeSpan.Zero;

        void ScheduleRefresh()
        {
            if (refreshReport is null) return;

            timeoutToken = app.AddTimeout(currentInterval, () =>
            {
                var refreshed = refreshReport();
                header.Text = BuildHeader(refreshed);
                killsFrame.Title = $"Individual kills ({refreshed.Summary.Kills.Count:N0})";
                totalsTable.Table = new DataTableSource(CreateMobTotalsTable(refreshed.Summary.Mobs));
                killsTable.Table = new DataTableSource(CreateKillsTable(refreshed.Summary.Kills));
                totalsTable.SetNeedsDraw();
                killsTable.SetNeedsDraw();
                header.SetNeedsDraw();
                killsFrame.SetNeedsDraw();

                // Return false — we reschedule manually so we always use currentInterval.
                ScheduleRefresh();
                return false;
            });
        }

        void UpdateFooter()
        {
            footer.Text = refreshReport is not null
                ? $"Live update every {currentInterval.TotalSeconds:N0}s (+/- to change). Tab switches panels. Arrow/PgUp/PgDn scroll. Esc exits."
                : "Tab switches panels. Arrow keys/PageUp/PageDown scroll tables. Esc exits. Use --text for plain output.";
            footer.SetNeedsDraw();
        }

        window.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Tab)
            {
                if (totalsTable.HasFocus)
                    killsTable.SetFocus();
                else
                    totalsTable.SetFocus();
                e.Handled = true;
                return;
            }

            if (refreshReport is not null && (e.AsRune.Value is '+' or '=' or '-'))
            {
                var delta = e.AsRune.Value == '-' ? -5 : 5;
                var newSeconds = Math.Max(5, (int)currentInterval.TotalSeconds + delta);
                currentInterval = TimeSpan.FromSeconds(newSeconds);

                if (timeoutToken is not null)
                    app.RemoveTimeout(timeoutToken);

                ScheduleRefresh();
                UpdateFooter();
                e.Handled = true;
            }
        };

        if (refreshReport is not null && refreshInterval is not null)
        {
            UpdateFooter();
            ScheduleRefresh();
        }

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
            $"Updated: {report.UpdatedAt:yyyy-MM-dd HH:mm:ss zzz}    Log: {report.LogPath}";
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

        foreach (var kill in kills.OrderByDescending(k => k.LineNumber))
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
