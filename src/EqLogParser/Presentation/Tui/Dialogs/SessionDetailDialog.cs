using EqLogParser.Core;
using EqLogParser.Core.Domain;
using EqLogParser.Presentation.Tui;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Presentation.Tui.Dialogs;

public static class SessionDetailDialog
{
    public static void Show(
        IApplication               app,
        SessionSummary             session,
        IReadOnlyList<KillSummary> allKills,
        IReadOnlyList<LootSummary> allLoot,
        IReadOnlyList<XpEvent>     allXp)
    {
        var sKills = allKills.Where(k => k.LineNumber >= session.StartLine && k.LineNumber <= session.EndLine).ToList();
        var sLoot  = allLoot .Where(l => l.LineNumber >= session.StartLine && l.LineNumber <= session.EndLine).ToList();
        var sXp    = allXp   .Where(x => x.LineNumber >= session.StartLine && x.LineNumber <= session.EndLine).ToList();

        var (xpLevels, xpProgress) = TuiTableFactory.ComputeXpProgress(sXp);

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

        int mobRows  = mobTotals.Count > 0 ? Math.Min(mobTotals.Count + 2, 10) : 0;
        int lootRows = sLoot.Count     > 0 ? Math.Min(sLoot.Count     + 2,  8) : 0;
        int xpRows   = sXp.Count       > 0 ? Math.Min(sXp.Count       + 2,  8) : 0;
        int dialogH  = Math.Clamp(4 + mobRows + lootRows + xpRows + 10, 20, 52);

        var zoneStr = session.Zone is not null ? $"  Zone: {session.Zone}" : "";
        var dpsStr  = session.Dps  > 0         ? $"  {session.Dps:N0} dps" : "";
        var dialog  = new Dialog
        {
            Title  = $"Session #{session.Number}  {session.StartTime:MM/dd HH:mm} → {session.EndTime:HH:mm}{zoneStr}",
            Width  = 76,
            Height = dialogH
        };

        var statsLabel = new Label
        {
            Text =
                $"Kills: {session.KillCount}  Loot: {session.LootCount}  Deaths: {session.Deaths}" +
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
            var mobView = new TableView(new DataTableSource(TuiTableFactory.CreateMobSessionTable(mobTotals)))
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
            var lootView = new TableView(new DataTableSource(TuiTableFactory.CreateLootTable(sLoot)))
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
            var xpView = new TableView(new DataTableSource(TuiTableFactory.CreateXpTable(sXp)))
            {
                X = 1, Y = Pos.Bottom(xpHeader),
                Width = Dim.Fill(1), Height = xpRows, FullRowSelect = true
            };
            dialog.Add(xpHeader, xpView);
        }

        dialog.AddButton(new Button { Text = "_Close" });
        app.Run(dialog);
    }
}
