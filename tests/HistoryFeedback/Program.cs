using System.Globalization;
using Predict.Models;
using Predict.Services;
using Predict.ViewModels;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var failures = 0;
void Test(string name, Action body)
{
    try { body(); Console.WriteLine($"PASS: {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL: {name}: {ex.Message}"); }
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"expected <{expected}>, actual <{actual}>");
}

Test("canonical kg history converts to saved lbs unit", () =>
{
    var entry = new HistoryEntry(new DateTime(2026, 10, 25), 100, 5, 8, 260, "lbs", true);
    Equal("220.46 lbs x 5 rep @ 8.0", entry.InputDisplay);
    Equal(100d, entry.Weight);
    Equal("Best: 260.0 lbs", entry.ResultDisplay);
});
Test("kg history retains entered hundredths", () =>
{
    var entry = new HistoryEntry(new DateTime(2026, 10, 25), 100.25, 5, 8, 120, "kg");
    Equal("100.25 kg x 5 rep @ 8.0", entry.InputDisplay);
    Equal("100 kg x 5 rep @ 8.0", (entry with { Weight = 100 }).InputDisplay);
});
foreach (var lbs in new[] { false, true })
{
    Test($"entered hundredths survive detail and share ({(lbs ? "lbs" : "kg")})", () =>
    {
        var kg = lbs ? 220.46 * 0.45359237 : 100.25;
        var result = OneRmCalculator.Calculate(kg, 5, 8, lbs);
        var vm = new ResultViewModel(new ResultContext { Current = result }, new AdsConsentCoordinator());
        vm.CancelAnimation();
        var expected = lbs ? "220.46 lbs" : "100.25 kg";
        Equal(expected, vm.WeightDisplay);
        vm.ShareCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var shared = Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.LastRequest?.Text;
        if (shared is null || !shared.Contains($"Weight: {expected} | Reps: 5 | RPE: 8.0"))
            throw new Exception($"share text lost entered precision: {shared}");
        var entry = new HistoryEntry(DateTime.Now, kg, 5, 8, result.BestEstimate, result.Unit, lbs);
        Equal($"{expected} x 5 rep @ 8.0", entry.InputDisplay);
        Equal(kg, result.Weight);
    });
}
Test("old JSON loads without migration, retains saved units and round-trips", () =>
{
    const string legacy = """[{"Date":"2026-10-25T11:16:00","Weight":100,"Reps":5,"Rpe":8,"BestEstimate":260,"Unit":"lbs","IsLbs":true},{"Date":"2026-10-25T10:16:00","Weight":100.25,"Reps":5,"Rpe":8,"BestEstimate":120,"Unit":"kg"}]""";
    Preferences.Default.Set(Predict.AppConstants.PrefHistory, legacy);
    var entries = HistoryService.Load();
    Equal(2, entries.Count);
    Equal(legacy, Preferences.Default.Get(Predict.AppConstants.PrefHistory, ""));
    Equal("220.46 lbs x 5 rep @ 8.0", entries[0].InputDisplay);
    Equal("100.25 kg x 5 rep @ 8.0", entries[1].InputDisplay);
    var restored = System.Text.Json.JsonSerializer.Deserialize<List<HistoryEntry>>(
        System.Text.Json.JsonSerializer.Serialize(entries))!;
    if (!entries.SequenceEqual(restored)) throw new Exception("persisted fields changed during JSON roundtrip");
    foreach (var entry in restored)
    {
        var result = OneRmCalculator.Calculate(entry.Weight, entry.Reps, entry.Rpe, entry.IsLbs);
        var vm = new ResultViewModel(new ResultContext { Current = result }, new AdsConsentCoordinator());
        vm.CancelAnimation();
        Equal($"{vm.WeightDisplay} x {entry.Reps} rep @ {entry.Rpe:0.0}", entry.InputDisplay);
    }
    var next = OneRmCalculator.Calculate(220.46 * 0.45359237, 5, 8, true);
    HistoryService.SaveAsync(next).GetAwaiter().GetResult();
    var saved = HistoryService.Load();
    Equal(3, saved.Count);
    if (!saved.Skip(1).SequenceEqual(entries)) throw new Exception("saving a new entry altered legacy entries");
    Equal(next.Weight, saved[0].Weight);
    Equal("220.46 lbs x 5 rep @ 8.0", saved[0].InputDisplay);
});
Test("Hungarian culture retains hundredths consistently in history, detail and share", () =>
{
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("hu-HU");
        foreach (var lbs in new[] { false, true })
        {
            var kg = lbs ? 220.46 * 0.45359237 : 100.25;
            var result = OneRmCalculator.Calculate(kg, 5, 8, lbs);
            var vm = new ResultViewModel(new ResultContext { Current = result }, new AdsConsentCoordinator());
            vm.CancelAnimation();
            var expected = lbs ? "220,46 lbs" : "100,25 kg";
            Equal(expected, vm.WeightDisplay);
            Equal($"{expected} x 5 rep @ 8,0", new HistoryEntry(DateTime.Now, kg, 5, 8, 120, result.Unit, lbs).InputDisplay);
            vm.ShareCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            if (!Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.LastRequest!.Text!.Contains($"Weight: {expected}"))
                throw new Exception("localized share lost precision");
        }
    }
    finally { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; }
});
Test("formula displays, best estimate and percentages match immutable baseline", () =>
{
    var cases = 0;
    foreach (var weight in new[] { 20.01, 100.25, 220.46 * 0.45359237 })
    foreach (var reps in Enumerable.Range(1, 10))
    foreach (var rpe in new[] { 6.0, 8.5, 10.0 })
    foreach (var lbs in new[] { false, true })
    foreach (var rounded in new[] { false, true })
    {
        var actual = OneRmCalculator.Calculate(weight, reps, rpe, lbs, rounded);
        var before = Baseline.Services.OneRmCalculator.Calculate(weight, reps, rpe, lbs, rounded);
        var expected = new[] { before.RpeTableDisplay, before.EpleyDisplay, before.BrzycziDisplay,
            before.LanderDisplay, before.LombardiDisplay, before.MayhewDisplay, before.OConnerDisplay, before.WathanDisplay };
        var displays = new double?[] { actual.RpeTableDisplay, actual.EpleyDisplay, actual.BrzycziDisplay,
            actual.LanderDisplay, actual.LombardiDisplay, actual.MayhewDisplay, actual.OConnerDisplay, actual.WathanDisplay };
        if (!expected.Select(v => (double?)v).SequenceEqual(displays)) throw new Exception("formula numeric output changed");
        Equal(before.BestEstimate, actual.BestEstimate);
        if (!expected.Select(value => $"{value:0.0} {before.Unit}").SequenceEqual(actual.Formulas.Select(f => f.ResultDisplay)))
            throw new Exception("formula text output changed");
        if (!before.Percentages.Select(p => p.Display).SequenceEqual(actual.Percentages.Select(p => p.Display)))
            throw new Exception("percentage output changed");
        foreach (var formula in actual.Formulas)
            if (!System.Text.RegularExpressions.Regex.IsMatch(formula.ResultDisplay, @"^\d+\.\d (kg|lbs)$"))
                throw new Exception("formula must still display exactly one decimal");
        cases++;
    }
    Console.WriteLine($"PASS: {cases} formula/estimate/percentage baseline combinations");
});
return failures == 0 ? 0 : 1;
