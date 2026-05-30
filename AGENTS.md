# AGENTS.md

## Stack

- .NET 10 console app (`net10.0`), C# with nullable enabled and implicit usings
- Single project: `src/EqLogParser/EqLogParser.csproj`
- Solution file: `src/eq-log-parser.slnx`
- Dependencies: `System.CommandLine 2.0.8`, `Terminal.Gui 2.4.3`
- No test project exists yet

## Key commands

```powershell
# Build
dotnet build src\EqLogParser\EqLogParser.csproj

# Run against the sample log (repo root)
dotnet run --project src\EqLogParser\EqLogParser.csproj -- eqlog_Sika_test.txt

# Plain text output (no TUI)
dotnet run --project src\EqLogParser\EqLogParser.csproj -- --text eqlog_Sika_test.txt

# Watch mode (live refresh, default 30s)
dotnet run --project src\EqLogParser\EqLogParser.csproj -- --watch --interval 10 eqlog_Sika_test.txt
```

All commands run from repo root. The log file path is relative to the working directory, not the project.

## Architecture

```
Program.cs          CLI wiring (System.CommandLine), entry point, rendering dispatch
AppConfig.cs        AppConfig record + ConfigStore (load/save eqparser.json)
Parsing/            Log file parsing
                      DamageLineParser    — YOU/other direct damage + effect damage lines
                                            captures spell name from YOUR effect lines
                      KillLineParser      — "X has been slain by Y" lines
                      LootLineParser      — looted/auto-sold loot lines (2 formats)
                      DeathLineParser     — "You have been slain by X" (player deaths)
                      ZoneLineParser      — "You have entered ZoneName." zone changes
                      HealLineParser      — "You healed X for N hit points by Spell."
                      ResistLineParser    — "X resisted your SpellName!"
                      MissLineParser      — "You try to verb X, but miss!"
                      MobNameNormalizer   — canonicalises mob name casing
                      LogIdentityParser   — extracts character + server from filename
                      EqDamageParser      — orchestrator: composes all parsers, links loot
                                            to kills, detects session boundaries
Domain/             Plain data types
                      DamageEvent, DamageKind
                      MobDamage           — per-mob accumulator; holds BySource list,
                                            FirstHitTimestamp, Resists, Misses
                      SourceDamage        — per-source (player/pet) accumulator; BySpell
                      KillEvent, KillSummary (includes Dps)
                      LootEvent           — raw loot line data
                      LootSummary         — loot enriched with linked KillLineNumber
                      DeathEvent          — player death with KilledBy
                      ZoneEvent           — zone change with ZoneName
                      HealEvent           — outgoing heal with Target, Amount, SpellName
                      SessionSummary      — per-session aggregates (kills, loot, xp, dps,
                                            deaths, resists, misses, zone, healing)
                      DamageSummary       — top-level result: Mobs, Kills, OpenEncounters,
                                            Loot, Xp, Sessions, Deaths, Zones, Heals
                      LogIdentity, DamageReport
Rendering/          Output
                      ConsoleDamageReportRenderer   — plain text (--text flag)
                      TerminalGuiDamageReportRenderer — five-panel TUI
```

## TUI keybinds (TerminalGuiDamageReportRenderer)

| Key | Action |
|-----|--------|
| `Tab` | Cycle focus through visible panels |
| `F` | Filter dialog (live preview, affects totals / kills / loot panels) |
| `D` | Kill detail dialog — source table + bar chart + spell breakdown + loot (kills panel focus) |
| `S` | Session detail dialog — top mobs, loot, XP for selected session (sessions panel focus) |
| `1` / `2` / `3` / `4` / `5` | Toggle totals / kills / loot / sessions / xp panels; hiding all resets |
| `+` / `-` | Adjust live-refresh interval ±5 s (watch mode only) |
| `Esc` | Quit |

Panel visibility and watch interval are persisted to `eqparser.json` in the working directory.

## Key implementation notes

- `EqDamageParser` is the top-level parser; it composes `DamageLineParser`, `KillLineParser`, `LootLineParser`, and `MobNameNormalizer`.
- Loot linking: each `LootEvent` is matched to the most-recent preceding kill of the same (normalised) mob name by line number.
- `DamageEvent.Source` carries the attacker name; `MobDamage.BySource` holds `SourceDamage` entries sorted descending by total damage.
- `DamageLineParser` handles three patterns: YOU direct, YOUR effect, and other-character direct (group members/pets, single-token source names).
- YOUR effect regex captures `(?<spell>.+?)` between `YOUR` and `for N points`; stored as `DamageEvent.SpellName` and accumulated in `SourceDamage.BySpell`.
- `MobDamage.FirstHitTimestamp` is set on the first `Add()` call; `KillSummary.Dps` = `TotalDamage / (killTime - firstHitTime).TotalSeconds`.
- `MobDamage.Resists` / `Misses` incremented by `ResistLineParser` / `MissLineParser` in `EqDamageParser`.
- Session detection: `EqDamageParser.TrackSession()` fires on every timestamped line; gap > `SessionIdleThreshold` (default 30 min) opens a new session.
- `BuildSessions()` partitions kills/loot/xp/deaths/heals/zones by `StartLine`/`EndLine` after all lines are processed.
- TUI uses `app.Keyboard.KeyDown` (application-scoped) so keys fire before focused views consume them.
- `modalActive` bool guard prevents re-entrant modal opens.
- `displayedKills` / `displayedSessions` are kept in sync with their tables for O(1) row→item lookup.
- TUI (`Terminal.Gui`) is skipped automatically when stdin/stdout are redirected (`CanRunTerminalUi()`).
- Kill isolation is by mob name — no unique mob IDs in EQ logs. Multiple same-name mobs active simultaneously will have damage merged.

## Terminal.Gui v2 API reminders

- `IApplication app = Application.Create(); app.Init();` — use `using` for disposal
- `app.Keyboard.KeyDown` not `window.KeyDown`
- Buttons use `Text` not `Title`; `button.Accepted` not `button.Clicked`
- `app.Run(dialog)` for modals; check `dialog.Canceled` for result
- `TableView.Value?.SelectedCell.Y` for selected row index
- `KeyCode` enum: `A`–`Z`, `D0`–`D9`, `Tab`, `Esc`, `Enter` etc.; NO `Plus`/`Minus` — use `e.AsRune.Value` for `+`/`-`/`1`/`2`/`3`
- Canonical API reference: https://raw.githubusercontent.com/gui-cs/Terminal.Gui/develop/ai-v2-primer.md

## Sample log

`eqlog_Sika_test.txt` in the repo root is the canonical test input. Use it for manual verification.

Two loot line formats present in the log:
- `--You have looted a Torn Page of Magi\`kot pg. 3 from a bloodthirsty ghoul's corpse.--`
- `You looted a Fine Steel Morning Star +1 from a bloodthirsty ghoul's corpse and sold it for 4 platinum, 4 gold, 2 silver and 9 copper.`
