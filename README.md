# EQ Log Parser

.NET 10 console parser for EverQuest log files named like:

```text
eqlog_CharacterName_ServerName.txt
```

Run it from the repository root:

```powershell
dotnet run --project src\EqLogParser\EqLogParser.csproj -- eqlog_Sika_test.txt
```

The default report opens an interactive Terminal.Gui interface with scrollable tables.
For plain console output, use:

```powershell
dotnet run --project src\EqLogParser\EqLogParser.csproj -- --text eqlog_Sika_test.txt
```

The parser reports:

- total outgoing damage grouped by mob name
- direct player damage from `You ...`
- self-owned effect damage from `YOUR ...`
- individual kill summaries when a matching kill line is found

Kill isolation is based on mob name because the log does not include unique mob IDs. If multiple mobs with the same name are damaged at the same time, their damage can be grouped into the next kill for that name.
