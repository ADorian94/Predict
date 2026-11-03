# History feedback regressions

Run from this directory:

```bash
'C:/Users/corta/AppData/Local/Microsoft/dotnet/dotnet.exe' run --project Predict.HistoryFeedbackTests.csproj
```

The directory-local `global.json` selects the installed .NET 9 SDK without changing the application's .NET 10 SDK pin. The executable links the real history model/service, result model/view model, calculator and MVVM source generator. Only platform boundaries (preferences, ads, haptics, telemetry, navigation and the native share target) are stubbed; the real generated share command is exercised and its outgoing text is asserted.

Coverage:
- Canonical kg history is converted to its saved lbs unit, with no data migration.
- Entered hundredths survive kg/lbs history, detail and share text.
- Integer inputs do not gain unwanted decimal zeros.
- Old JSON (including omitted optional IsLbs), serialize/deserialize, and saving a new result preserve existing entries and canonical weight values.
- Hungarian decimal-comma formatting remains consistent across displays/share.
- 360 combinations compare all formula numeric/display values, best estimates and percentage displays against the existing unchanged `tests/Rpe/Baseline` snapshots. Formula display remains one decimal.

TDD reproductions before fixes:

```text
FAIL: canonical kg history converts to saved lbs unit: expected <220.46 lbs x 5 rep @ 8.0>, actual <100 lbs x 5 rep @ 8.0>
FAIL: kg history retains entered hundredths: expected <100.25 kg x 5 rep @ 8.0>, actual <100.3 kg x 5 rep @ 8.0>
FAIL: entered hundredths survive detail and share (kg): expected <100.25 kg>, actual <100.3 kg>
FAIL: entered hundredths survive detail and share (lbs): expected <220.46 lbs>, actual <220.5 lbs>
```

Both RED stages exited 1; their subsequent GREEN stages exited 0. No Android build, signing, remote, Notion, or git operations are needed.
