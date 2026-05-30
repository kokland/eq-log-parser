using EqLogParser.Domain;
using EqLogParser.Rendering.Dialogs;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Rendering;

public sealed class TerminalGuiDamageReportRenderer(AppConfig config) : IDamageReportRenderer
{
    public TerminalGuiDamageReportRenderer() : this(new AppConfig()) { }

    public void Render(DamageReport report) =>
        RenderCore(report, refreshReport: null, refreshInterval: null);

    public void RenderWatch(
        DamageReport       initialReport,
        Func<DamageReport> refresh,
        TimeSpan           interval,
        CancellationToken  cancellationToken = default) =>
        RenderCore(initialReport, refresh, interval);

    // -------------------------------------------------------------------------

    private void RenderCore(
        DamageReport        report,
        Func<DamageReport>? refreshReport,
        TimeSpan?           refreshInterval)
    {
        using IApplication app = Application.Create();
        app.Init();

        using var window = new Window { Title = "EQ Log Parser - Esc to quit" };

        var header = new Label
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = 5,
            Text = TuiTableFactory.BuildHeader(report)
        };

        var totalsFrame   = new FrameView { Title = "Damage by mob",                            X = 0,                    Y = Pos.Bottom(header), Width = Dim.Percent(45), Height = Dim.Fill(1) };
        var killsFrame    = new FrameView { Title = $"Individual kills ({report.Summary.Kills.Count:N0})",   X = Pos.Right(totalsFrame),   Y = Pos.Bottom(header), Width = Dim.Percent(28), Height = Dim.Fill(1) };
        var lootFrame     = new FrameView { Title = $"Loot ({report.Summary.Loot.Count:N0})",   X = Pos.Right(killsFrame),    Y = Pos.Bottom(header), Width = Dim.Fill(),       Height = Dim.Fill(1) };
        var sessionsFrame = new FrameView { Title = TuiTableFactory.SessionsFrameTitle(report.Summary.Sessions), X = Pos.Right(lootFrame),     Y = Pos.Bottom(header), Width = Dim.Fill(),       Height = Dim.Fill(1) };
        var xpFrame       = new FrameView { Title = TuiTableFactory.XpFrameTitle(report.Summary.Xp),            X = Pos.Right(sessionsFrame), Y = Pos.Bottom(header), Width = Dim.Fill(),       Height = Dim.Fill(1) };

        var footer = new Label
        {
            X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1,
            Text = "Tab switches panels. Arrow keys/PageUp/PageDown scroll tables. Esc exits. Use --text for plain output."
        };

        // ---- state ----
        List<KillSummary>    allKillsSorted    = [];
        List<LootSummary>    allLootSorted     = [];
        List<XpEvent>        allXpSorted       = [];
        List<SessionSummary> allSessionsSorted = [];
        Dictionary<int, List<LootSummary>> lootByKillLine = [];

        void RebuildCaches(DamageReport r)
        {
            allKillsSorted    = r.Summary.Kills   .OrderByDescending(k => k.LineNumber).ToList();
            allLootSorted     = r.Summary.Loot    .OrderByDescending(l => l.LineNumber).ToList();
            allXpSorted       = r.Summary.Xp      .OrderByDescending(x => x.LineNumber).ToList();
            allSessionsSorted = r.Summary.Sessions.OrderByDescending(s => s.Number).ToList();
            lootByKillLine    = [];
            foreach (var l in r.Summary.Loot)
            {
                if (l.KillLineNumber is int kl)
                {
                    if (!lootByKillLine.TryGetValue(kl, out var bucket))
                        lootByKillLine[kl] = bucket = [];
                    bucket.Add(l);
                }
            }
        }

        RebuildCaches(report);

        var totalsTable   = TuiTableFactory.CreateTableView(TuiTableFactory.CreateMobTotalsTable(report.Summary.Mobs));
        var killsTable    = TuiTableFactory.CreateTableView(TuiTableFactory.CreateKillsTable(allKillsSorted));
        var lootTable     = TuiTableFactory.CreateTableView(TuiTableFactory.CreateLootTable(allLootSorted));
        var sessionsTable = TuiTableFactory.CreateTableView(TuiTableFactory.CreateSessionsTable(allSessionsSorted));
        var xpTable       = TuiTableFactory.CreateTableView(TuiTableFactory.CreateXpTable(allXpSorted));

        totalsFrame  .Add(totalsTable);
        killsFrame   .Add(killsTable);
        lootFrame    .Add(lootTable);
        sessionsFrame.Add(sessionsTable);
        xpFrame      .Add(xpTable);

        bool showTotals   = config.ShowTotals;
        bool showKills    = config.ShowKills;
        bool showLoot     = config.ShowLoot;
        bool showSessions = config.ShowSessions;
        bool showXp       = config.ShowXp;
        string currentFilter = string.Empty;
        DamageReport lastReport = report;
        IReadOnlyList<KillSummary>    displayedKills    = allKillsSorted;
        IReadOnlyList<SessionSummary> displayedSessions = allSessionsSorted;
        object? timeoutToken = null;
        var currentInterval = refreshInterval ?? TimeSpan.Zero;

        void SaveConfig()
        {
            config.ShowTotals   = showTotals;
            config.ShowKills    = showKills;
            config.ShowLoot     = showLoot;
            config.ShowSessions = showSessions;
            config.ShowXp       = showXp;
            if (refreshReport is not null)
                config.WatchIntervalSeconds = (int)currentInterval.TotalSeconds;
            ConfigStore.Default().Save(config);
        }

        void UpdateLayout()
        {
            var visible = new List<FrameView>(5);
            if (showTotals)   visible.Add(totalsFrame);
            if (showKills)    visible.Add(killsFrame);
            if (showLoot)     visible.Add(lootFrame);
            if (showSessions) visible.Add(sessionsFrame);
            if (showXp)       visible.Add(xpFrame);

            if (visible.Count == 0)
            {
                showTotals = showKills = showLoot = showSessions = showXp = true;
                UpdateLayout();
                return;
            }

            totalsFrame.Visible   = showTotals;
            killsFrame.Visible    = showKills;
            lootFrame.Visible     = showLoot;
            sessionsFrame.Visible = showSessions;
            xpFrame.Visible       = showXp;

            int pct = 100 / visible.Count;
            for (int i = 0; i < visible.Count; i++)
            {
                var frame = visible[i];
                if (i == 0)
                {
                    frame.X = 0;
                    frame.Width = visible.Count == 1 ? Dim.Fill() : Dim.Percent(pct);
                }
                else if (i == visible.Count - 1)
                {
                    frame.X = Pos.Right(visible[i - 1]);
                    frame.Width = Dim.Fill();
                }
                else
                {
                    frame.X = Pos.Right(visible[i - 1]);
                    frame.Width = Dim.Percent(pct * (i + 1)) - Dim.Percent(pct * i);
                }
            }
            window.SetNeedsDraw();
        }

        void ApplyFilter(DamageReport r, string filter)
        {
            IReadOnlyList<MobDamage>   filteredMobs;
            IReadOnlyList<KillSummary> filteredKills;
            IReadOnlyList<LootSummary> filteredLoot;

            if (string.IsNullOrWhiteSpace(filter))
            {
                filteredMobs  = r.Summary.Mobs;
                filteredKills = allKillsSorted;
                filteredLoot  = allLootSorted;
            }
            else
            {
                filteredMobs  = r.Summary.Mobs.Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                filteredKills = allKillsSorted.Where(k => k.Mob.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                filteredLoot  = allLootSorted .Where(l => l.MobName .Contains(filter, StringComparison.OrdinalIgnoreCase)
                                                        || l.ItemName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            displayedKills    = filteredKills;
            displayedSessions = allSessionsSorted;
            killsFrame.Title    = $"Individual kills ({filteredKills.Count:N0})";
            lootFrame.Title     = $"Loot ({filteredLoot.Count:N0})";
            totalsTable  .Table = new DataTableSource(TuiTableFactory.CreateMobTotalsTable(filteredMobs));
            killsTable   .Table = new DataTableSource(TuiTableFactory.CreateKillsTable(filteredKills));
            lootTable    .Table = new DataTableSource(TuiTableFactory.CreateLootTable(filteredLoot));
            sessionsTable.Table = new DataTableSource(TuiTableFactory.CreateSessionsTable(allSessionsSorted));
            xpTable      .Table = new DataTableSource(TuiTableFactory.CreateXpTable(allXpSorted));
            sessionsFrame.Title = TuiTableFactory.SessionsFrameTitle(r.Summary.Sessions);
            xpFrame.Title       = TuiTableFactory.XpFrameTitle(r.Summary.Xp);
            totalsTable  .SetNeedsDraw();
            killsTable   .SetNeedsDraw();
            lootTable    .SetNeedsDraw();
            sessionsTable.SetNeedsDraw();
            xpTable      .SetNeedsDraw();
            killsFrame   .SetNeedsDraw();
            lootFrame    .SetNeedsDraw();
            sessionsFrame.SetNeedsDraw();
            xpFrame      .SetNeedsDraw();
        }

        void ScheduleRefresh()
        {
            if (refreshReport is null) return;
            timeoutToken = app.AddTimeout(currentInterval, () =>
            {
                var newReport = refreshReport();
                var changed   = newReport.Summary.TotalHits != lastReport.Summary.TotalHits;
                lastReport    = newReport;
                header.Text   = TuiTableFactory.BuildHeader(lastReport);
                header.SetNeedsDraw();
                if (changed)
                {
                    RebuildCaches(lastReport);
                    ApplyFilter(lastReport, currentFilter);
                    sessionsFrame.Title = TuiTableFactory.SessionsFrameTitle(lastReport.Summary.Sessions);
                    sessionsFrame.SetNeedsDraw();
                    xpFrame.Title = TuiTableFactory.XpFrameTitle(lastReport.Summary.Xp);
                    xpFrame.SetNeedsDraw();
                }
                ScheduleRefresh();
                return false;
            });
        }

        void UpdateFooter()
        {
            var filterTag = string.IsNullOrWhiteSpace(currentFilter) ? "" : $" [filter: \"{currentFilter}\"]";
            var hidden = new List<string>(5);
            if (!showTotals)   hidden.Add("totals");
            if (!showKills)    hidden.Add("kills");
            if (!showLoot)     hidden.Add("loot");
            if (!showSessions) hidden.Add("sessions");
            if (!showXp)       hidden.Add("xp");
            var viewTag = hidden.Count > 0 ? $" [hidden: {string.Join(", ", hidden)}]" : "";
            footer.Text = refreshReport is not null
                ? $"Live every {currentInterval.TotalSeconds:N0}s (+/- change). F filter{filterTag}. 1-5 panels{viewTag}. D detail (kills). S detail (sessions). Tab/Arrow/PgUp/PgDn. Esc."
                : $"F filter{filterTag}. 1-5 panels{viewTag}. D detail (kills). S detail (sessions). Tab/Arrow/PgUp/PgDn. Esc. --text plain.";
            footer.SetNeedsDraw();
        }

        bool modalActive = false;

        app.Keyboard.KeyDown += (_, e) =>
        {
            if (modalActive) return;

            if (e.KeyCode == KeyCode.Tab)
            {
                TableView[] cycle = [totalsTable, killsTable, lootTable, sessionsTable, xpTable];
                bool[]      shown = [showTotals, showKills, showLoot, showSessions, showXp];
                int current = Array.FindIndex(cycle, t => t.HasFocus);
                for (int i = 1; i <= cycle.Length; i++)
                {
                    int next = (current + i) % cycle.Length;
                    if (shown[next]) { cycle[next].SetFocus(); break; }
                }
                e.Handled = true;
                return;
            }

            if (e.KeyCode == KeyCode.F)
            {
                modalActive = true;
                var saved  = currentFilter;
                var result = FilterDialog.Show(app, currentFilter, lastReport, f => ApplyFilter(lastReport, f));
                if (result is not null)
                {
                    currentFilter = result;
                    ApplyFilter(lastReport, currentFilter);
                    UpdateFooter();
                }
                else
                {
                    ApplyFilter(lastReport, saved);
                }
                modalActive = false;
                e.Handled = true;
                return;
            }

            if (e.KeyCode == KeyCode.D && killsTable.HasFocus)
            {
                var row = killsTable.Value?.SelectedCell.Y ?? 0;
                if (row >= 0 && row < displayedKills.Count)
                {
                    modalActive = true;
                    var kill     = displayedKills[row];
                    var killLoot = lootByKillLine.TryGetValue(kill.LineNumber, out var bucket)
                        ? (IReadOnlyList<LootSummary>)bucket : [];
                    KillDetailDialog.Show(app, kill, killLoot);
                    modalActive = false;
                }
                e.Handled = true;
                return;
            }

            if (e.KeyCode == KeyCode.S && sessionsTable.HasFocus)
            {
                var row = sessionsTable.Value?.SelectedCell.Y ?? 0;
                if (row >= 0 && row < displayedSessions.Count)
                {
                    modalActive = true;
                    SessionDetailDialog.Show(app, displayedSessions[row], allKillsSorted, allLootSorted, allXpSorted);
                    modalActive = false;
                }
                e.Handled = true;
                return;
            }

            if (e.AsRune.Value is '1' or '2' or '3' or '4' or '5')
            {
                if      (e.AsRune.Value == '1') showTotals   = !showTotals;
                else if (e.AsRune.Value == '2') showKills    = !showKills;
                else if (e.AsRune.Value == '3') showLoot     = !showLoot;
                else if (e.AsRune.Value == '4') showSessions = !showSessions;
                else                            showXp       = !showXp;
                UpdateLayout();
                UpdateFooter();
                SaveConfig();
                e.Handled = true;
                return;
            }

            if (refreshReport is not null && (e.AsRune.Value is '+' or '=' or '-'))
            {
                var delta = e.AsRune.Value == '-' ? -5 : 5;
                currentInterval = TimeSpan.FromSeconds(Math.Max(5, (int)currentInterval.TotalSeconds + delta));
                if (timeoutToken is not null) app.RemoveTimeout(timeoutToken);
                ScheduleRefresh();
                UpdateFooter();
                SaveConfig();
                e.Handled = true;
            }
        };

        if (refreshReport is not null && refreshInterval is not null)
        {
            UpdateFooter();
            ScheduleRefresh();
        }

        window.Add(header, totalsFrame, killsFrame, lootFrame, sessionsFrame, xpFrame, footer);
        app.Run(window);
    }
}
