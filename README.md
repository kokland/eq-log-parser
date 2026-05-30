# EQ Log Parser

.NET 10 console parser for EverQuest log files named like:

```text
eqlog_CharacterName_ServerName.txt
```

Run it from the repository root:

```powershell
dotnet run --project src\EqLogParser\EqLogParser.csproj -- eqlog_Sika_test.txt
```

The default report opens an interactive Terminal.Gui interface with three scrollable panels.
For plain console output, use:

```powershell
dotnet run --project src\EqLogParser\EqLogParser.csproj -- --text eqlog_Sika_test.txt
```

To keep the report live while the log is being written, use `--watch`.
The default refresh interval is 30 seconds:

```powershell
dotnet run --project src\EqLogParser\EqLogParser.csproj -- --watch eqlog_Sika_test.txt
```

Use `--interval` to set a different refresh interval in seconds:

```powershell
dotnet run --project src\EqLogParser\EqLogParser.csproj -- --watch --interval 10 eqlog_Sika_test.txt
```

## TUI panels

| # | Panel | Contents |
|---|-------|----------|
| 1 | Damage by mob | Total / direct / effect damage and hit count per mob group |
| 2 | Individual kills | One row per confirmed kill with per-source breakdown available |
| 3 | Loot | Every looted item linked to the kill it came from |

## TUI keybinds

| Key | Action |
|-----|--------|
| `Tab` | Cycle focus between visible panels |
| `Arrow` / `PgUp` / `PgDn` | Scroll the focused table |
| `F` | Open filter dialog — narrows all panels by mob name (live preview) |
| `D` | Open kill detail dialog for the selected kill (focus must be on panel 2) |
| `1` / `2` / `3` | Toggle each panel on/off; hiding all resets to all visible |
| `+` / `-` | Increase / decrease live-refresh interval by 5 s (watch mode only) |
| `Esc` | Quit |

### Kill detail dialog (`D`)

Shows a per-source damage table, a unicode bar chart of relative contributions, and any loot items linked to that kill.

## What the parser tracks

- Total outgoing damage grouped by mob name
- Direct player damage (`You hit ...`) and self-owned effect damage (`YOUR ...`)
- Per-source damage breakdown — group members and pets are tracked separately
- Individual kill summaries when a matching kill line is found
- Loot events (kept items and auto-sold items), each linked to the most-recent preceding kill of the same mob

Kill isolation is based on mob name because the log does not include unique mob IDs. If multiple mobs with the same name are active simultaneously, their damage will be merged into the next kill for that name.
