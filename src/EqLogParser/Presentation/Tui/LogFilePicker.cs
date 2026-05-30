using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace EqLogParser.Presentation.Tui;

/// <summary>
/// Shows a Terminal.Gui <see cref="OpenDialog"/> and returns the selected
/// EverQuest log file path, or <c>null</c> if the user cancelled.
/// </summary>
public static class LogFilePicker
{
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
