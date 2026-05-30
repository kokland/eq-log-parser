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
            Width = Dim.Percent(28),
            Height = Dim.Fill(1)
        };

        var lootFrame = new FrameView
        {
            Title = $"Loot ({report.Summary.Loot.Count:N0})",
            X = Pos.Right(killsFrame),
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

        // Pre-sorted views of the full data set — rebuilt only when the report changes.
        // ApplyFilter filters from these; no re-sort needed inside ApplyFilter.
        List<KillSummary>   allKillsSorted = [];
        List<LootSummary>   allLootSorted  = [];
        // Kill line number → loot items for that kill; rebuilt with the report.
        Dictionary<int, List<LootSummary>> lootByKillLine = [];

        void RebuildCaches(DamageReport r)
        {
            allKillsSorted = r.Summary.Kills.OrderByDescending(k => k.LineNumber).ToList();
            allLootSorted  = r.Summary.Loot.OrderByDescending(l => l.LineNumber).ToList();
            lootByKillLine = [];
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

        var totalsTable = CreateTableView(CreateMobTotalsTable(report.Summary.Mobs));
        var killsTable  = CreateTableView(CreateKillsTable(allKillsSorted));
        var lootTable   = CreateTableView(CreateLootTable(allLootSorted));

        totalsFrame.Add(totalsTable);
        killsFrame.Add(killsTable);
        lootFrame.Add(lootTable);

        // Tab moves focus between the left and right panels.
        // +/- adjust the live-refresh interval (watch mode only).
        // F opens a filter dialog to narrow tables by mob name.
        // 1/2/3 toggle the totals/kills/loot panels.
        // D opens a kill detail breakdown dialog.
        bool showTotals = true;
        bool showKills  = true;
        bool showLoot   = true;
        string currentFilter = string.Empty;
        DamageReport lastReport = report;
        // Tracks the kill rows currently visible in killsTable (respects active filter + sort).
        IReadOnlyList<KillSummary> displayedKills = allKillsSorted;
        object? timeoutToken = null;
        var currentInterval = refreshInterval ?? TimeSpan.Zero;

        void UpdateLayout()
        {
            // Determine which panels are visible.
            var visible = new List<FrameView>(3);
            if (showTotals) visible.Add(totalsFrame);
            if (showKills)  visible.Add(killsFrame);
            if (showLoot)   visible.Add(lootFrame);

            // If all hidden, reset to all visible.
            if (visible.Count == 0)
            {
                showTotals = showKills = showLoot = true;
                UpdateLayout();
                return;
            }

            // Hide panels not in the visible list.
            totalsFrame.Visible = showTotals;
            killsFrame.Visible  = showKills;
            lootFrame.Visible   = showLoot;

            // Lay out visible panels as equal-width columns.
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
                filteredKills = allKillsSorted;   // already sorted descending
                filteredLoot  = allLootSorted;    // already sorted descending
            }
            else
            {
                filteredMobs  = r.Summary.Mobs .Where(m => m.Name     .Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                filteredKills = allKillsSorted  .Where(k => k.Mob.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                filteredLoot  = allLootSorted   .Where(l => l.MobName .Contains(filter, StringComparison.OrdinalIgnoreCase)
                                                          || l.ItemName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            displayedKills = filteredKills;
            killsFrame.Title = $"Individual kills ({filteredKills.Count:N0})";
            lootFrame.Title  = $"Loot ({filteredLoot.Count:N0})";
            totalsTable.Table = new DataTableSource(CreateMobTotalsTable(filteredMobs));
            killsTable.Table  = new DataTableSource(CreateKillsTable(filteredKills));
            lootTable.Table   = new DataTableSource(CreateLootTable(filteredLoot));
            totalsTable.SetNeedsDraw();
            killsTable.SetNeedsDraw();
            lootTable.SetNeedsDraw();
            killsFrame.SetNeedsDraw();
            lootFrame.SetNeedsDraw();
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
                }

                // Return false — we reschedule manually so we always use currentInterval.
                ScheduleRefresh();
                return false;
            });
        }

        void UpdateFooter()
        {
            var filterTag = string.IsNullOrWhiteSpace(currentFilter)
                ? ""
                : $" [filter: \"{currentFilter}\"]";
            var hidden = new List<string>(3);
            if (!showTotals) hidden.Add("totals");
            if (!showKills)  hidden.Add("kills");
            if (!showLoot)   hidden.Add("loot");
            var viewTag = hidden.Count > 0 ? $" [hidden: {string.Join(", ", hidden)}]" : "";
            footer.Text = refreshReport is not null
                ? $"Live every {currentInterval.TotalSeconds:N0}s (+/- change). F filter{filterTag}. 1/2/3 panels{viewTag}. D detail. Tab/Arrow/PgUp/PgDn. Esc exits."
                : $"F filter{filterTag}. 1/2/3 panels{viewTag}. D detail (kills panel). Tab/Arrow/PgUp/PgDn. Esc. --text plain.";
            footer.SetNeedsDraw();
        }

        // Guard so our app-level handler doesn't re-fire while a modal is already open.
        bool modalActive = false;

        void OpenFilterDialog()
        {
            var dialog = new Dialog
            {
                Title = "Filter by mob name",
                Width = 52,
                Height = 9
            };

            var label = new Label
            {
                Text = "Name contains (empty = clear filter):",
                X = 1,
                Y = 1
            };

            var textField = new TextField
            {
                Text = currentFilter,
                X = 1,
                Y = 3,
                Width = Dim.Fill(2)
            };

            // Live preview: debounced so a DataTable rebuild fires at most once per 150 ms
            // rather than on every individual keystroke.
            object? filterDebounceToken = null;
            textField.TextChanged += (_, _) =>
            {
                if (filterDebounceToken is not null)
                    app.RemoveTimeout(filterDebounceToken);
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
                // Restore the previous filter if the user cancelled.
                ApplyFilter(lastReport, savedFilter);
            }
        }

        // Use app.Keyboard.KeyDown (application-scoped, fires before any view sees the key)
        void OpenKillDetailDialog(KillSummary kill)
        {
            var sources  = kill.Mob.BySource;
            var killLoot = lootByKillLine.TryGetValue(kill.LineNumber, out var bucket)
                ? bucket
                : (IReadOnlyList<LootSummary>)[];

            var total      = kill.Mob.TotalDamage;
            var maxDamage  = sources.Count > 0 ? sources.Max(s => s.TotalDamage) : 1L;
            const int BarWidth = 22;

            // Text bar chart
            var sb = new System.Text.StringBuilder();
            foreach (var src in sources)
            {
                var pct    = total > 0 ? src.TotalDamage * 100.0 / total : 0;
                var filled = maxDamage > 0 ? (int)(src.TotalDamage * BarWidth / (double)maxDamage) : 0;
                var bar    = new string('█', filled) + new string('░', BarWidth - filled);
                sb.AppendLine($"{src.Source,-14} {bar} {src.TotalDamage,8:N0}  {pct,5:F1}%");
            }

            int tableRows  = sources.Count > 0 ? Math.Min(sources.Count + 2, 10) : 0;
            int barRows    = sources.Count > 0 ? sources.Count + 1 : 0;
            int lootRows   = killLoot.Count > 0 ? Math.Min(killLoot.Count + 2, 8) : 0;
            int dialogH    = Math.Clamp(tableRows + barRows + lootRows + 8, 16, 42);

            var dialog = new Dialog
            {
                Title  = $"Kill: {kill.Mob.Name}  line {kill.LineNumber}  killed by {kill.KilledBy}",
                Width  = 72,
                Height = dialogH
            };

            View lastAdded = dialog;
            int  lastY     = 1;

            if (sources.Count > 0)
            {
                var detailTable = new TableView(new DataTableSource(CreateSourceBreakdownTable(sources, total)))
                {
                    X = 1, Y = lastY,
                    Width = Dim.Fill(1),
                    Height = tableRows,
                    FullRowSelect = true
                };
                dialog.Add(detailTable);

                var barLabel = new Label
                {
                    Text   = sb.ToString().TrimEnd(),
                    X      = 1,
                    Y      = Pos.Bottom(detailTable) + 1,
                    Width  = Dim.Fill(1),
                    Height = barRows
                };
                dialog.Add(barLabel);
                lastAdded = barLabel;
                lastY     = 0; // unused when using Pos.Bottom
            }

            if (killLoot.Count > 0)
            {
                var lootLabel = new Label
                {
                    Text   = $"Loot ({killLoot.Count}):",
                    X      = 1,
                    Y      = sources.Count > 0 ? Pos.Bottom(lastAdded) + 1 : lastY,
                    Width  = Dim.Fill(1),
                    Height = 1
                };
                var lootView = new TableView(new DataTableSource(CreateLootTable(killLoot)))
                {
                    X = 1,
                    Y = Pos.Bottom(lootLabel),
                    Width = Dim.Fill(1),
                    Height = lootRows,
                    FullRowSelect = true
                };
                dialog.Add(lootLabel, lootView);
            }

            dialog.AddButton(new Button { Text = "_Close" });
            app.Run(dialog);
        }

        app.Keyboard.KeyDown += (_, e) =>
        {
            if (modalActive) return;

            if (e.KeyCode == KeyCode.Tab)
            {
                // Cycle focus through visible panels: totals -> kills -> loot -> totals ...
                if (totalsTable.HasFocus)
                    (showKills ? killsTable : showLoot ? lootTable : totalsTable).SetFocus();
                else if (killsTable.HasFocus)
                    (showLoot ? lootTable : showTotals ? totalsTable : killsTable).SetFocus();
                else
                    (showTotals ? totalsTable : showKills ? killsTable : lootTable).SetFocus();
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

            if (e.AsRune.Value is '1' or '2' or '3')
            {
                if (e.AsRune.Value == '1')      showTotals = !showTotals;
                else if (e.AsRune.Value == '2') showKills  = !showKills;
                else                            showLoot   = !showLoot;
                UpdateLayout();
                UpdateFooter();
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

        window.Add(header, totalsFrame, killsFrame, lootFrame, footer);
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

    private static DataTable CreateLootTable(IEnumerable<LootSummary> loot)
    {
        var table = CreateTable("Line", "Time", "Item", "Mob", "Sold?", "Kill#");

        foreach (var l in loot)
        {
            table.Rows.Add(
                l.LineNumber,
                l.Timestamp,
                l.ItemName,
                l.MobName,
                l.AutoSold ? "sold" : "",
                l.KillLineNumber.HasValue ? l.KillLineNumber.Value.ToString() : "");
        }

        return table;
    }

    private static DataTable CreateSourceBreakdownTable(IReadOnlyList<SourceDamage> sources, long total)    {
        var table = CreateTable("Source", "Total", "Direct", "Effect", "Hits", "%");

        foreach (var src in sources)
        {
            var pct = total > 0 ? $"{src.TotalDamage * 100.0 / total:F1}%" : "0.0%";
            table.Rows.Add(src.Source, src.TotalDamage, src.DirectDamage, src.EffectDamage, src.Hits, pct);
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
