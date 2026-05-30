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
Parsing/            Log file parsing — DamageLineParser, KillLineParser, MobNameNormalizer,
                    LogIdentityParser, EqDamageParser (orchestrator)
Domain/             Plain data types — DamageEvent, KillEvent, MobDamage, DamageSummary,
                    KillSummary, LogIdentity, DamageKind
Rendering/          DamageReport (data), ConsoleDamageReportRenderer (text),
                    TerminalGuiDamageReportRenderer (TUI)
```

- `EqDamageParser` is the top-level parser; it composes `DamageLineParser`, `KillLineParser`, and `MobNameNormalizer`.
- TUI (`Terminal.Gui`) is skipped automatically when stdin/stdout are redirected (`CanRunTerminalUi()`).
- Kill isolation is by mob name — no unique mob IDs in EQ logs. Multiple same-name mobs active simultaneously will have damage merged.

## Sample log

`eqlog_Sika_test.txt` in the repo root is the canonical test input. Use it for manual verification.
