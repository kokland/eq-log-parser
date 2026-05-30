using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace EqLogParser.Presentation.Tui;

/// <summary>
/// Shows a Terminal.Gui <see cref="OpenDialog"/> and returns the selected
/// EverQuest log file path, or <c>null</c> if the user cancelled.
/// </summary>
public static class LogFilePicker
{
    /// <summary>
    /// Runs a standalone yes/no dialog asking whether to resume reading
    /// <paramref name="lastPath"/>. Returns <c>true</c> if the user picks
    /// "Resume", <c>false</c> if they pick "Open different file" or close.
    /// </summary>
    public static bool AskResume(string lastPath)
    {
        bool resume = false;

        using IApplication app = Application.Create();
        app.Init();

        var fileName = Path.GetFileName(lastPath);

        var dlg = new Dialog
        {
            Title  = "Resume last session?",
            Width  = 70,
            Height = 9,
        };

        var label = new Label
        {
            X    = 1,
            Y    = 1,
            Width = Dim.Fill(1),
            Text = $"Last file: {fileName}\n{lastPath}",
        };

        var btnResume = new Button
        {
            Text      = "Resume",
            X         = Pos.Center() - 18,
            Y         = Pos.AnchorEnd(2),
            IsDefault = true,
        };

        var btnOpen = new Button
        {
            Text = "Open different file",
            X    = Pos.Center() + 2,
            Y    = Pos.AnchorEnd(2),
        };

        btnResume.Accepted += (_, _) => { resume = true;  app.RequestStop(); };
        btnOpen  .Accepted += (_, _) => { resume = false; app.RequestStop(); };

        dlg.Add(label, btnResume, btnOpen);
        app.Run(dlg);

        return resume;
    }

    private static readonly List<IAllowedType> AllowedTypes =
    [
        new AllowedType("EverQuest log", ".txt"),
        new AllowedTypeAny()
    ];

    /// <summary>
    /// Runs the open-file dialog inside an existing TUI session.
    /// Returns the chosen path, or <c>null</c> if cancelled.
    /// </summary>
    public static string? Pick(IApplication app, string? initialPath = null)
    {
        var dlg = new OpenDialog
        {
            Title        = "Open EverQuest log file",
            OpenMode     = OpenMode.File,
            MustExist    = true,
            AllowedTypes = AllowedTypes,
        };

        if (initialPath is not null)
            dlg.Path = initialPath;

        app.Run(dlg);
        return dlg.Canceled ? null : dlg.Path;
    }

    /// <summary>
    /// Runs the open-file dialog as a standalone TUI session
    /// (used before the main renderer is initialised).
    /// Returns the chosen path, or <c>null</c> if cancelled.
    /// </summary>
    public static string? PickStandalone(string? initialPath = null)
    {
        using IApplication app = Application.Create();
        app.Init();
        var path = Pick(app, initialPath);
        return path;
    }
}
