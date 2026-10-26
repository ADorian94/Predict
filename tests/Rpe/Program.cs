using Predict.Services;

var result = OneRmCalculator.Calculate(100, 11, 10);
object? rpe = result.RpeTableResult;
if (rpe is not null)
    throw new Exception("FAIL: 11 reps must not silently reuse the ten-repetition RTS RPE row.");
Console.WriteLine("PASS: unsupported RPE estimate is explicitly absent above 10 reps.");

var formulaProperty = typeof(Predict.Models.OneRmResult).GetProperty("Formulas");
var visibleRows = formulaProperty?.GetValue(result) as IReadOnlyList<Predict.Models.FormulaEntry>;
if (visibleRows is null || visibleRows.Count != 7 || visibleRows.Any(row => row.Name == "RTS RPE" || row.ResultDisplay.Contains("N/A")))
    throw new Exception("FAIL: unsupported RTS RPE must be omitted from the actual formula list, not shown as N/A.");
Console.WriteLine("PASS: unsupported RTS RPE row omitted from screen/share formula list.");

var count = typeof(Predict.Models.OneRmResult).GetProperty("FormulaCount");
if (count?.GetValue(result) is not 7)
    throw new Exception("FAIL: result presentation must report seven supported formulas above ten reps.");
var rpeText = typeof(Predict.Models.OneRmResult).GetProperty("RpeTableText")?.GetValue(result) as string;
var note = typeof(Predict.Models.OneRmResult).GetProperty("RpeAvailabilityNote")?.GetValue(result) as string;
var roundingNote = typeof(Predict.Models.OneRmResult).GetProperty("RoundingNote")?.GetValue(result) as string;
if (rpeText != "N/A" || note is null || !note.Contains("10 reps") || !note.Contains("not used")
    || roundingNote is null || !roundingNote.Contains("7 formulas"))
    throw new Exception("FAIL: unavailable RPE and its exclusion from the seven-formula estimate must be explicit.");
Console.WriteLine("PASS: explicit N/A, seven-formula caption and RPE exclusion notice.");

foreach (var bad in new (double Weight, int Reps, double Rpe)[]
{
    (100, 0, 8), (100, 21, 8), (100, 10, 5.5), (100, 10, 10.5),
    (100, 10, double.NaN), (100, 10, double.PositiveInfinity),
    (0, 10, 8), (-1, 10, 8), (double.NaN, 10, 8), (double.PositiveInfinity, 10, 8)
})
{
    try { OneRmCalculator.Calculate(bad.Weight, bad.Reps, bad.Rpe); }
    catch (ArgumentOutOfRangeException) { continue; }
    throw new Exception($"FAIL: invalid calculator input must be rejected: {bad}.");
}
Console.WriteLine("PASS: invalid repetition/RPE bounds and non-finite/invalid weights rejected.");

var cases = 0;
foreach (var weight in new[] { 20.0, 100.0, 120.0 })
foreach (var reps in Enumerable.Range(1, 20))
foreach (var effort in new[] { 6.0, 6.5, 7.0, 7.5, 8.0, 8.5, 9.0, 9.5, 10.0 })
foreach (var lbs in new[] { false, true })
foreach (var rounded in new[] { false, true })
{
    var now = OneRmCalculator.Calculate(weight, reps, effort, lbs, rounded);
    var before = Baseline.Services.OneRmCalculator.Calculate(weight, reps, effort, lbs, rounded);
    var seven = new[] { before.EpleyDisplay, before.BrzycziDisplay, before.LanderDisplay,
        before.LombardiDisplay, before.MayhewDisplay, before.OConnerDisplay, before.WathanDisplay };
    var actualSeven = new[] { now.EpleyDisplay, now.BrzycziDisplay, now.LanderDisplay,
        now.LombardiDisplay, now.MayhewDisplay, now.OConnerDisplay, now.WathanDisplay };
    if (!seven.SequenceEqual(actualSeven)) throw new Exception("FAIL: a non-RPE formula changed.");
    var expected = reps <= 10 ? before.BestEstimate
        : rounded ? Math.Round(seven.Average() / 2.5) * 2.5 : Math.Round(seven.Average(), 1);
    if (Math.Abs(now.BestEstimate - expected) > 0.000001
        || now.FormulaCount != (reps <= 10 ? 8 : 7)
        || now.HasRpeEstimate != (reps <= 10))
        throw new Exception($"FAIL: wrong supported-formula average for {weight}/{reps}/{effort}/{lbs}/{rounded}.");
    if (reps <= 10 && now.RpeTableResult != before.RpeTableResult)
        throw new Exception("FAIL: supported RTS table output changed.");
    if (reps <= 10 && now.IsRpeUnavailable || reps > 10 && now.RpeTableText != "N/A")
        throw new Exception("FAIL: unsupported-state presentation is inconsistent.");
    if (now.Formulas.Count != now.FormulaCount
        || now.Formulas.Any(row => row.Name == "RTS RPE") != (reps <= 10)
        || now.Formulas.Any(row => row.ResultDisplay.Contains("N/A")))
        throw new Exception("FAIL: RTS RPE row must remain for 1–10 and be omitted only for 11–20 reps.");
    if (!now.RoundingNote.Contains($"{now.FormulaCount} formulas"))
        throw new Exception("FAIL: caption does not match the averaging denominator.");
    foreach (var p in now.Percentages)
    {
        var raw = expected * p.Percent / 100.0;
        var expectedPercent = rounded ? Math.Round(raw / 2.5) * 2.5 : Math.Round(raw, 1);
        if (p.Weight != expectedPercent || p.Unit != now.Unit)
            throw new Exception("FAIL: percentage or kg/lbs regression.");
    }
    cases++;
}
Console.WriteLine($"PASS: {cases} range/baseline cases (1–20 reps, all RPE half-steps, kg/lbs, rounded/raw).");

var legacy = """[{"Date":"2026-10-25T11:16:00","Weight":120,"Reps":20,"Rpe":8,"BestEstimate":202.5,"Unit":"kg","IsLbs":false}]""";
Preferences.Default.Set(Predict.AppConstants.PrefHistory, legacy);
var stored = HistoryService.Load();
if (stored.Count != 1 || stored[0].BestEstimate != 202.5
    || Preferences.Default.Get(Predict.AppConstants.PrefHistory, "") != legacy)
    throw new Exception("FAIL: reading existing history changed or lost stored data.");
var reopened = OneRmCalculator.Calculate(stored[0].Weight, stored[0].Reps, stored[0].Rpe, stored[0].IsLbs);
if (reopened.HasRpeEstimate || reopened.FormulaCount != 7)
    throw new Exception("FAIL: reopening old high-rep entries must use corrected semantics.");
await HistoryService.SaveAsync(reopened);
var saved = HistoryService.Load();
if (saved.Count != 2 || saved[0].BestEstimate != reopened.BestEstimate || saved[1] != stored[0])
    throw new Exception("FAIL: save must retain the original legacy entry and the new corrected estimate.");
Console.WriteLine("PASS: legacy history preserved and new corrected results save/reload.");
foreach (var reps in new[] { 10, 11, 12, 20 })
{
    var sample = OneRmCalculator.Calculate(120, reps, 8);
    Console.WriteLine($"SAMPLE: 120kg/{reps}reps/RPE8 => RTS {sample.RpeTableText}; Best {sample.BestEstimate:0.0}kg; formulas {sample.FormulaCount}");
}

public static class Preferences
{
    public static FakePreferences Default { get; } = new();
}
public sealed class FakePreferences
{
    private readonly Dictionary<string, object> _values = [];
    public T Get<T>(string key, T fallback) => _values.TryGetValue(key, out var v) ? (T)v : fallback;
    public void Set<T>(string key, T value) => _values[key] = value!;
}
