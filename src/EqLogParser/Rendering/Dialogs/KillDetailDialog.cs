using EqLogParser.Domain;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Rendering.Dialogs;

public static class KillDetailDialog
{
    public static void Show(
        IApplication               app,
        KillSummary                kill,
        IReadOnlyList<LootSummary> killLoot)
    {
        var sources   = kill.Mob.BySource;
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

        var youSource = sources.FirstOrDefault(s => s.Source.Equals("You", StringComparison.OrdinalIgnoreCase));
        var spellRows = youSource?.BySpell ?? [];

        int tableRows  = sources.Count  > 0 ? Math.Min(sources.Count  + 2, 10) : 0;
        int barRows    = sources.Count  > 0 ? sources.Count + 1 : 0;
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
            var detailTable = new TableView(new DataTableSource(TuiTableFactory.CreateSourceBreakdownTable(sources, total)))
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
            var spellView = new TableView(new DataTableSource(TuiTableFactory.CreateSpellBreakdownTable(spellRows, total)))
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
            var lootView = new TableView(new DataTableSource(TuiTableFactory.CreateLootTable(killLoot)))
            {
                X = 1, Y = Pos.Bottom(lootLabel),
                Width = Dim.Fill(1), Height = lootRows, FullRowSelect = true
            };
            dialog.Add(lootLabel, lootView);
        }

        dialog.AddButton(new Button { Text = "_Close" });
        app.Run(dialog);
    }
}
