# EQ Log Parser

.NET 10 console parser for EverQuest log files named like:

```text
eqlog_CharacterName_ServerName.txt
```

Run it from the repository root:

```powershell
dotnet run --project src\EqLogParser\EqLogParser.csproj -- eqlog_Sika_test.txt
```

The default report opens an interactive Terminal.Gui interface with five scrollable panels.
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

| Key | Panel | Contents |
|-----|-------|----------|
| `1` | Damage by mob | Total / direct / effect damage, hit count, resists, and misses per mob group |
| `2` | Individual kills | One row per confirmed kill with DPS; press `D` for full breakdown |
| `3` | Loot | Every looted item linked to the kill it came from |
| `4` | Sessions | One row per play session detected by idle gaps; press `S` for full breakdown |
| `5` | XP gains | Each XP gain with running level progress |

## TUI keybinds

| Key | Action |
|-----|--------|
| `Tab` | Cycle focus between visible panels |
| `Arrow` / `PgUp` / `PgDn` | Scroll the focused table |
| `F` | Open filter dialog — narrows totals / kills / loot panels by mob name (live preview) |
| `D` | Kill detail dialog — source breakdown, bar chart, spell breakdown, resists/misses, loot (panel 2 focus) |
| `S` | Session detail dialog — top mobs, loot, XP, and combat stats for the selected session (panel 4 focus) |
| `1` / `2` / `3` / `4` / `5` | Toggle each panel on/off; hiding all resets to all visible |
| `+` / `-` | Increase / decrease live-refresh interval by 5 s (watch mode only) |
| `Esc` | Quit |

Panel visibility and the watch interval are persisted to `eqparser.json` in the working directory and restored on next launch.

### Kill detail dialog (`D`)

- Per-source damage table (player, pet, group members) with direct / effect / hit breakdown
- Unicode bar chart of relative contributions
- YOUR spell breakdown — damage per spell name
- Resist and miss counts for the encounter
- Loot items linked to that kill

### Session detail dialog (`S`)

- Combat stats: kills, loot, deaths, resists, misses, total healing, XP gained, DPS
- Top mobs by damage for the session
- Loot table scoped to the session
- XP gains table scoped to the session

## What the parser tracks

| Category | Detail |
|----------|--------|
| **Damage** | Direct player hits, YOUR effect damage, group member and pet damage — all grouped by mob |
| **DPS** | Per-kill DPS from first hit to kill time; per-session DPS over the full session window |
| **Kills** | Every kill confirmed by a "has been slain" line, linked to its damage accumulator |
| **Loot** | Kept and auto-sold items, each linked to the most-recent preceding kill of the same mob |
| **Sessions** | Contiguous play blocks split by idle gaps > 30 minutes |
| **Deaths** | Player deaths (`You have been slain by X`) counted per session and in the header |
| **Resists** | Spell resists (`X resisted your SpellName`) tracked per mob |
| **Misses** | Melee misses (`You try to verb X, but miss`) tracked per mob |
| **Zone changes** | `You have entered ZoneName` — first zone of each session shown in the sessions panel |
| **Healing** | All `You healed X for N hit points by Spell` lines; total shown in the header and per session |
| **Spell breakdown** | YOUR effect damage broken down by spell name, visible in the kill detail dialog |
| **XP** | Solo and party XP gains with running level + progress tracking |

> **Kill isolation note:** mob grouping is by name because the log does not include unique mob IDs. If multiple mobs with the same name are active simultaneously, their damage will be merged into the next kill for that name.
