using EqLogParser.Domain;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Rendering.Dialogs;

public static class FilterDialog
{
    /// <summary>
    /// Shows the filter dialog. Returns the new filter string if the user
    /// applied, or null if the user cancelled (caller should restore the
    /// previous filter in that case).
    /// </summary>
    public static string? Show(
        IApplication       app,
        string             currentFilter,
        DamageReport       lastReport,
        Action<string>     applyFilter)
    {
        var dialog    = new Dialog { Title = "Filter by mob name", Width = 52, Height = 9 };
        var label     = new Label { Text = "Name contains (empty = clear filter):", X = 1, Y = 1 };
        var textField = new TextField { Text = currentFilter, X = 1, Y = 3, Width = Dim.Fill(2) };

        object? debounce = null;
        textField.TextChanged += (_, _) =>
        {
            if (debounce is not null) app.RemoveTimeout(debounce);
            debounce = app.AddTimeout(TimeSpan.FromMilliseconds(150), () =>
            {
                debounce = null;
                applyFilter(textField.Text?.Trim() ?? string.Empty);
                return false;
            });
        };

        dialog.Add(label, textField);
        dialog.AddButton(new Button { Text = "_Cancel" });
        dialog.AddButton(new Button { Text = "_Apply" });

        textField.SetFocus();
        app.Run(dialog);

        return dialog.Canceled ? null : (textField.Text?.Trim() ?? string.Empty);
    }
}
