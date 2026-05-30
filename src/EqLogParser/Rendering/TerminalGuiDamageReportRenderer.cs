using System.Data;
using EqLogParser.Domain;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Rendering;

public sealed class TerminalGuiDamageReportRenderer(AppConfig config)
{
    public TerminalGuiDamageReportRenderer() : this(new AppConfig()) { }

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
            X = 0, Y = 0, Width = Dim.Fill(), Height = 5,
            Text = BuildHeader(report)
        };

        var totalsFrame = new FrameView
        {
            Title = "Damage by mob",
            X = 0, Y = Pos.Bottom(header),
            Width = Dim.Percent(45), Height = Dim.Fill(1)
        };
        var killsFrame = new FrameView
        {
            Title = $"Individual kills ({report.Summary.Kills.Count:N0})",
            X = Pos.Right(totalsFrame), Y = Pos.Bottom(header),
            Width = Dim.Percent(28), Height = Dim.Fill(1)
        };
        var lootFrame = new FrameView
        {
            Title = $"Loot ({report.Summary.Loot.Count:N0})",
            X = Pos.Right(killsFrame), Y = Pos.Bottom(header),
            Width = Dim.Fill(), Height = Dim.Fill(1)
        };
        var sessionsFrame = new FrameView
        {
            Title = SessionsFrameTitle(report.Summary.Sessions),
            X = Pos.Right(lootFrame), Y = Pos.Bottom(header),
            Width = Dim.Fill(), Height = Dim.Fill(1)
        };
        var xpFrame = new FrameView
        {
            Title = XpFrameTitle(report.Summary.Xp),
            X = Pos.Right(sessionsFrame), Y = Pos.Bottom(header),
            Width = Dim.Fill(), Height = Dim.Fill(1)
        };

        var footer = new Label
        {
            X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1,
            Text = "Tab switches panels. Arrow keys/PageUp/PageDown scroll tables. Esc exits. Use --text for plain output."
        };

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

        var totalsTable   = CreateTableView(CreateMobTotalsTable(report.Summary.Mobs));
        var killsTable    = CreateTableView(CreateKillsTable(allKillsSorted));
        var lootTable     = CreateTableView(CreateLootTable(allLootSorted));
        var sessionsTable = CreateTableView(CreateSessionsTable(allSessionsSorted));
        var xpTable       = CreateTableView(CreateXpTable(allXpSorted));

        totalsFrame.Add(totalsTable);
        killsFrame.Add(killsTable);
        lootFrame.Add(lootTable);
        sessionsFrame.Add(sessionsTable);
        xpFrame.Add(xpTable);

        bool showTotals   = config.ShowTotals;
        bool showKills    = config.ShowKills;
        bool showLoot     = config.ShowLoot;
        bool showSessions = config.ShowSessions;
        bool showXp       = config.ShowXp;
        string currentFilter = string.Empty;
        DamageReport lastReport = report;
        IReadOnlyList<KillSummary>   displayedKills   = allKillsSorted;
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
            ConfigStore.Save(config);
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
                filteredKills = allKillsSorted .Where(k => k.Mob.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                filteredLoot  = allLootSorted  .Where(l => l.MobName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                                         || l.ItemName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            displayedKills    = filteredKills;
            displayedSessions = allSessionsSorted; // sessions not filtered
            killsFrame.Title    = $"Individual kills ({filteredKills.Count:N0})";
            lootFrame.Title     = $"Loot ({filteredLoot.Count:N0})";
            totalsTable.Table   = new DataTableSource(CreateMobTotalsTable(filteredMobs));
            killsTable.Table    = new DataTableSource(CreateKillsTable(filteredKills));
            lootTable.Table     = new DataTableSource(CreateLootTable(filteredLoot));
            sessionsTable.Table = new DataTableSource(CreateSessionsTable(allSessionsSorted));
            xpTable.Table       = new DataTableSource(CreateXpTable(allXpSorted));
            sessionsFrame.Title = SessionsFrameTitle(r.Summary.Sessions);
            xpFrame.Title       = XpFrameTitle(r.Summary.Xp);
            totalsTable.SetNeedsDraw();
            killsTable.SetNeedsDraw();
            lootTable.SetNeedsDraw();
            sessionsTable.SetNeedsDraw();
            xpTable.SetNeedsDraw();
            killsFrame.SetNeedsDraw();
            lootFrame.SetNeedsDraw();
            sessionsFrame.SetNeedsDraw();
            xpFrame.SetNeedsDraw();
        }

        void ScheduleRefresh()
        {
            if (refreshReport is null) return;

            timeoutToken = app.AddTimeout(currentInterval, () =>
            {
                var newReport = refreshReport();
                var changed   = newReport.Summary.TotalHits != lastReport.Summary.TotalHits;
                lastReport    = newReport;

                header.Text = BuildHeader(lastReport);
                header.SetNeedsDraw();

                if (changed)
                {
                    RebuildCaches(lastReport);
                    ApplyFilter(lastReport, currentFilter);
                    sessionsFrame.Title = SessionsFrameTitle(lastReport.Summary.Sessions);
                    sessionsFrame.SetNeedsDraw();
                    xpFrame.Title = XpFrameTitle(lastReport.Summary.Xp);
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

        void OpenFilterDialog()
        {
            var dialog = new Dialog { Title = "Filter by mob name", Width = 52, Height = 9 };
            var label     = new Label { Text = "Name contains (empty = clear filter):", X = 1, Y = 1 };
            var textField = new TextField { Text = currentFilter, X = 1, Y = 3, Width = Dim.Fill(2) };

            object? filterDebounceToken = null;
            textField.TextChanged += (_, _) =>
            {
                if (filterDebounceToken is not null) app.RemoveTimeout(filterDebounceToken);
                filterDebounceToken = app.AddTimeout(TimeSpan.FromMilliseconds(150), () =>
                {
                    filterDebounceToken = null;
                    ApplyFilter(lastReport, textField.Text?.Trim() ?? string.Empty);
                    return false;
                });
            };

            dialog.Add(label, textField);
            dialog.AddButton(new Button { Text = "_Cancel" });
            dialog.AddButton(new Button { Text = "_Apply" });

            var savedFilter = currentFilter;
            textField.SetFocus();
            app.Run(dialog);

            if (!dialog.Canceled)
            {
                currentFilter = textField.Text?.Trim() ?? string.Empty;
                UpdateFooter();
            }
            else
            {
                ApplyFilter(lastReport, savedFilter);
            }
        }

        void OpenKillDetailDialog(KillSummary kill)
        {
            var sources  = kill.Mob.BySource;
            var killLoot = lootByKillLine.TryGetValue(kill.LineNumber, out var bucket)
                ? bucket : (IReadOnlyList<LootSummary>)[];

            var total     = kill.Mob.TotalDamage;
            var maxDamage = sources.Count > 0 ? sources.Max(s => s.TotalDamage) : 1L;
            const int BarWidth = 22;

            var sb = new System.Text.StringBuilder();
            foreach (var src in sources)
            {
                var pct    = total > 0 ? src.TotalDamage * 100.0 / total : 0;
                var filled = maxDamage > 0 ? (int)(src.TotalDamage * BarWidth / (double)maxDamage) : 0;
                var bar    = new string('█', filled) + new string('░', BarWidth - filled);
                sb.AppendLine($"{src.Source,-14} {bar} {src.TotalDamage,8:N0}  {pct,5:F1}%");
            }

            // Spell breakdown for "You" source.
            var youSource  = sources.FirstOrDefault(s => s.Source.Equals("You", StringComparison.OrdinalIgnoreCase));
            var spellRows  = youSource?.BySpell ?? [];

            int tableRows  = sources.Count > 0 ? Math.Min(sources.Count + 2, 10) : 0;
            int barRows    = sources.Count > 0 ? sources.Count + 1 : 0;
            int spellRows2 = spellRows.Count > 0 ? Math.Min(spellRows.Count + 2, 8) : 0;
            int lootRows   = killLoot.Count  > 0 ? Math.Min(killLoot.Count  + 2, 8) : 0;
            int resInfo    = (kill.Mob.Resists > 0 || kill.Mob.Misses > 0) ? 1 : 0;
            int dialogH    = Math.Clamp(tableRows + barRows + spellRows2 + lootRows + resInfo + 8, 16, 48);

            var dpsStr = kill.Dps.HasValue ? $"  {kill.Dps.Value:N0} dps" : "";
            var dialog = new Dialog
            {
                Title  = $"Kill: {kill.Mob.Name}  line {kill.LineNumber}  by {kill.KilledBy}{dpsStr}",
                Width  = 72,
                Height = dialogH
            };

            View lastAdded = dialog;

            if (sources.Count > 0)
            {
                var detailTable = new TableView(new DataTableSource(CreateSourceBreakdownTable(sources, total)))
                {
                    X = 1, Y = 1, Width = Dim.Fill(1), Height = tableRows, FullRowSelect = true
                };
                dialog.Add(detailTable);

                var barLabel = new Label
                {
                    Text = sb.ToString().TrimEnd(), X = 1,
                    Y = Pos.Bottom(detailTable) + 1, Width = Dim.Fill(1), Height = barRows
                };
                dialog.Add(barLabel);
                lastAdded = barLabel;
            }

            if (spellRows.Count > 0)
            {
                var spellHeader = new Label
                {
                    Text = "YOUR spell breakdown:", X = 1,
                    Y = Pos.Bottom(lastAdded) + 1, Width = Dim.Fill(1), Height = 1
                };
                var spellView = new TableView(new DataTableSource(CreateSpellBreakdownTable(spellRows, total)))
                {
                    X = 1, Y = Pos.Bottom(spellHeader),
                    Width = Dim.Fill(1), Height = spellRows2, FullRowSelect = true
                };
                dialog.Add(spellHeader, spellView);
                lastAdded = spellView;
            }

            if (resInfo > 0)
            {
                var resLabel = new Label
                {
                    Text = $"Resists: {kill.Mob.Resists}    Misses: {kill.Mob.Misses}",
                    X = 1, Y = Pos.Bottom(lastAdded) + 1, Width = Dim.Fill(1), Height = 1
                };
                dialog.Add(resLabel);
                lastAdded = resLabel;
            }

            if (killLoot.Count > 0)
            {
                var lootLabel = new Label
                {
                    Text = $"Loot ({killLoot.Count}):", X = 1,
                    Y = Pos.Bottom(lastAdded) + 1, Width = Dim.Fill(1), Height = 1
                };
                var lootView = new TableView(new DataTableSource(CreateLootTable(killLoot)))
                {
                    X = 1, Y = Pos.Bottom(lootLabel),
                    Width = Dim.Fill(1), Height = lootRows, FullRowSelect = true
                };
                dialog.Add(lootLabel, lootView);
            }

            dialog.AddButton(new Button { Text = "_Close" });
            app.Run(dialog);
        }

        void OpenSessionDetailDialog(SessionSummary session)
        {
            // Collect kills / loot / xp for this session.
            var sKills = allKillsSorted
                .Where(k => k.LineNumber >= session.StartLine && k.LineNumber <= session.EndLine)
                .ToList();
            var sLoot = allLootSorted
                .Where(l => l.LineNumber >= session.StartLine && l.LineNumber <= session.EndLine)
                .ToList();
            var sXp = allXpSorted
                .Where(x => x.LineNumber >= session.StartLine && x.LineNumber <= session.EndLine)
                .ToList();

            var (xpLevels, xpProgress) = ComputeXpProgress(sXp);

            // Per-mob damage summary for the session (from kills only).
            var mobTotals = sKills
                .GroupBy(k => k.Mob.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => (
                    Mob:    g.Key,
                    Damage: g.Sum(k => k.Mob.TotalDamage),
                    Kills:  g.Count(),
                    Dps:    g.Average(k => k.Dps ?? 0)))
                .OrderByDescending(x => x.Damage)
                .Take(12)
                .ToList();

            int mobRows  = mobTotals.Count  > 0 ? Math.Min(mobTotals.Count  + 2, 10) : 0;
            int lootRows = sLoot.Count      > 0 ? Math.Min(sLoot.Count      + 2,  8) : 0;
            int xpRows   = sXp.Count        > 0 ? Math.Min(sXp.Count        + 2,  8) : 0;
            int dialogH  = Math.Clamp(4 + mobRows + lootRows + xpRows + 10, 20, 52);

            var zoneStr  = session.Zone is not null ? $"  Zone: {session.Zone}" : "";
            var dpsStr   = session.Dps  > 0         ? $"  {session.Dps:N0} dps" : "";
            var dialog   = new Dialog
            {
                Title  = $"Session #{session.Number}  {session.StartTime:MM/dd HH:mm} → {session.EndTime:HH:mm}{zoneStr}",
                Width  = 76,
                Height = dialogH
            };

            // Stats summary line.
            var statsLabel = new Label
            {
                Text   = $"Kills: {session.KillCount}  Loot: {session.LootCount}  Deaths: {session.Deaths}" +
                         $"  Resists: {session.Resists}  Misses: {session.Misses}" +
                         $"  Healing: {session.TotalHealing:N0}  XP: {xpLevels}lv+{xpProgress:F2}%{dpsStr}",
                X = 1, Y = 1, Width = Dim.Fill(1), Height = 1
            };
            dialog.Add(statsLabel);

            View lastAdded = statsLabel;

            if (mobRows > 0)
            {
                var mobHeader = new Label
                {
                    Text = $"Top mobs by damage ({mobTotals.Count}):",
                    X = 1, Y = Pos.Bottom(lastAdded) + 1, Width = Dim.Fill(1), Height = 1
                };
                var mobTable = CreateMobSessionTable(mobTotals);
                var mobView  = new TableView(new DataTableSource(mobTable))
                {
                    X = 1, Y = Pos.Bottom(mobHeader),
                    Width = Dim.Fill(1), Height = mobRows, FullRowSelect = true
                };
                dialog.Add(mobHeader, mobView);
                lastAdded = mobView;
            }

            if (lootRows > 0)
            {
                var lootHeader = new Label
                {
                    Text = $"Loot ({sLoot.Count}):",
                    X = 1, Y = Pos.Bottom(lastAdded) + 1, Width = Dim.Fill(1), Height = 1
                };
                var lootView = new TableView(new DataTableSource(CreateLootTable(sLoot)))
                {
                    X = 1, Y = Pos.Bottom(lootHeader),
                    Width = Dim.Fill(1), Height = lootRows, FullRowSelect = true
                };
                dialog.Add(lootHeader, lootView);
                lastAdded = lootView;
            }

            if (xpRows > 0)
            {
                var xpHeader = new Label
                {
                    Text = $"XP gains ({sXp.Count}):",
                    X = 1, Y = Pos.Bottom(lastAdded) + 1, Width = Dim.Fill(1), Height = 1
                };
                var xpView = new TableView(new DataTableSource(CreateXpTable(sXp)))
                {
                    X = 1, Y = Pos.Bottom(xpHeader),
                    Width = Dim.Fill(1), Height = xpRows, FullRowSelect = true
                };
                dialog.Add(xpHeader, xpView);
            }

            dialog.AddButton(new Button { Text = "_Close" });
            app.Run(dialog);
        }

        app.Keyboard.KeyDown += (_, e) =>
        {
            if (modalActive) return;

            if (e.KeyCode == KeyCode.Tab)
            {
                TableView[] cycle = [totalsTable, killsTable, lootTable, sessionsTable, xpTable];
                bool[] shown      = [showTotals, showKills, showLoot, showSessions, showXp];
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
                OpenFilterDialog();
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
                    OpenKillDetailDialog(displayedKills[row]);
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
                    OpenSessionDetailDialog(displayedSessions[row]);
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

    // -------------------------------------------------------------------------
    // Static helpers
    // -------------------------------------------------------------------------

    private static TableView CreateTableView(DataTable table)
    {
        return new TableView(new DataTableSource(table))
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), FullRowSelect = true
        };
    }

    private static string BuildHeader(DamageReport report)
    {
        var s = report.Summary;
        var identity = report.Identity is null
            ? "Character: unknown    Server: unknown"
            : $"Character: {report.Identity.CharacterName}    Server: {report.Identity.ServerName}";

        var xpLine = s.Xp.Count == 0 ? "XP: none" : FormatXpSummary(s.Xp);

        var healStr   = s.TotalHealing > 0 ? $"    Healing: {s.TotalHealing:N0}" : "";
        var deathStr  = s.Deaths.Count > 0 ? $"    Deaths: {s.Deaths.Count}" : "";

        return
            $"{identity}{Environment.NewLine}" +
            $"Total damage: {s.TotalDamage:N0}    Hits: {s.TotalHits:N0}    Mobs: {s.Mobs.Count:N0}" +
            $"    Sessions: {s.Sessions.Count}{healStr}{deathStr}{Environment.NewLine}" +
            $"Updated: {report.UpdatedAt:yyyy-MM-dd HH:mm:ss zzz}    Log: {report.LogPath}{Environment.NewLine}" +
            xpLine;
    }

    private static string FormatXpSummary(IReadOnlyList<XpEvent> xp)
    {
        var (levels, progress) = ComputeXpProgress(xp);
        var solo  = xp.Where(x => !x.IsParty).Sum(x => x.Percent);
        var party = xp.Where(x =>  x.IsParty).Sum(x => x.Percent);
        var levelStr = levels == 1 ? "1 level" : $"{levels} levels";
        return $"XP: {levelStr} + {progress:F3}% toward next    " +
               $"(solo: {solo:F3}% / {xp.Count(x => !x.IsParty)} gains    " +
               $"party: {party:F3}% / {xp.Count(x => x.IsParty)} gains)";
    }

    private static string XpFrameTitle(IReadOnlyList<XpEvent> xp)
    {
        if (xp.Count == 0) return "XP gains (none)";
        var (levels, progress) = ComputeXpProgress(xp);
        return $"XP gains ({xp.Count:N0})    {levels} level{(levels == 1 ? "" : "s")} + {progress:F3}%";
    }

    private static string SessionsFrameTitle(IReadOnlyList<SessionSummary> sessions)
    {
        if (sessions.Count == 0) return "Sessions (none)";
        return $"Sessions ({sessions.Count})";
    }

    private static (int Levels, double Progress) ComputeXpProgress(IEnumerable<XpEvent> xp)
    {
        double acc = 0; int levels = 0;
        foreach (var ev in xp.OrderBy(x => x.LineNumber))
        {
            acc += ev.Percent;
            while (acc >= 100.0) { levels++; acc -= 100.0; }
        }
        return (levels, acc);
    }

    private static DataTable CreateMobTotalsTable(IReadOnlyList<MobDamage> mobs)
    {
        var table = CreateTable("Mob", "Total", "Direct", "YOUR", "Hits", "Resists", "Misses");
        foreach (var mob in mobs)
            table.Rows.Add(mob.Name, mob.TotalDamage, mob.DirectDamage, mob.YourEffectDamage,
                           mob.Hits, mob.Resists, mob.Misses);
        return table;
    }

    private static DataTable CreateKillsTable(IReadOnlyList<KillSummary> kills)
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

    private static DataTable CreateSessionsTable(IReadOnlyList<SessionSummary> sessions)
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

    private static DataTable CreateMobSessionTable(
        IEnumerable<(string Mob, long Damage, int Kills, double Dps)> rows)
    {
        var table = CreateTable("Mob", "Damage", "Kills", "Avg DPS");
        foreach (var (mob, dmg, kills, dps) in rows)
            table.Rows.Add(mob, dmg, kills, dps > 0 ? $"{dps:N0}" : "");
        return table;
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
        return $"{ts.Minutes}m {ts.Seconds:D2}s";
    }

    private static DataTable CreateXpTable(IEnumerable<XpEvent> events)
    {
        var table = CreateTable("Line", "Time", "XP%", "Type", "Progress");
        var list = events.ToList(); // newest-first
        var levelAfter    = new int[list.Count];
        var progressAfter = new double[list.Count];
        var leveledUp     = new bool[list.Count];

        double acc = 0; int lv = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            int prevLv = lv;
            acc += list[i].Percent;
            while (acc >= 100.0) { lv++; acc -= 100.0; }
            levelAfter[i]    = lv;
            progressAfter[i] = acc;
            leveledUp[i]     = lv > prevLv;
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

    private static DataTable CreateLootTable(IEnumerable<LootSummary> loot)
    {
        var table = CreateTable("Line", "Time", "Item", "Mob", "Sold?", "Kill#");
        foreach (var l in loot)
            table.Rows.Add(
                l.LineNumber, l.Timestamp, l.ItemName, l.MobName,
                l.AutoSold ? "sold" : "",
                l.KillLineNumber.HasValue ? l.KillLineNumber.Value.ToString() : "");
        return table;
    }

    private static DataTable CreateSourceBreakdownTable(IReadOnlyList<SourceDamage> sources, long total)
    {
        var table = CreateTable("Source", "Total", "Direct", "Effect", "Hits", "%");
        foreach (var src in sources)
        {
            var pct = total > 0 ? $"{src.TotalDamage * 100.0 / total:F1}%" : "0.0%";
            table.Rows.Add(src.Source, src.TotalDamage, src.DirectDamage, src.EffectDamage, src.Hits, pct);
        }
        return table;
    }

    private static DataTable CreateSpellBreakdownTable(
        IReadOnlyList<(string Spell, long Damage)> spells, long totalMobDamage)
    {
        var table = CreateTable("Spell", "Damage", "%");
        foreach (var (spell, dmg) in spells)
        {
            var pct = totalMobDamage > 0 ? $"{dmg * 100.0 / totalMobDamage:F1}%" : "0.0%";
            table.Rows.Add(spell, dmg, pct);
        }
        return table;
    }

    private static DataTable CreateTable(params string[] columns)
    {
        var table = new DataTable();
        foreach (var col in columns) table.Columns.Add(col);
        return table;
    }
}
