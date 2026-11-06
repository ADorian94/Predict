using System.Globalization;
using System.Text.Json;
using Predict.Models;
using Predict.Services;
using Predict.ViewModels;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var failures = 0;
void Test(string name, Action body)
{
    try { body(); Console.WriteLine($"PASS: {name}"); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL: {name}: {e.Message}"); }
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"expected <{expected}>, actual <{actual}>");
}
string Num(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);

// A legacy record exactly as stored by older builds: no IsRounded member at all.
string LegacyJson(double weight, int reps, double rpe, double best, bool lbs = false) =>
    "[{\"Date\":\"2026-10-25T11:16:00\"," +
    $"\"Weight\":{Num(weight)},\"Reps\":{reps},\"Rpe\":{Num(rpe)}," +
    $"\"BestEstimate\":{Num(best)},\"Unit\":\"{(lbs ? "lbs" : "kg")}\",\"IsLbs\":{(lbs ? "true" : "false")}" +
    "}]";

const string Pref = Predict.AppConstants.PrefHistory;

// ---------------------------------------------------------------- new records
Test("raw saved entry reopens with the same raw best estimate", () =>
{
    Preferences.Default.Set(Pref, "");
    var result = OneRmCalculator.Calculate(130, 5, 9.5, false, false);
    Equal(150.5, result.BestEstimate);
    HistoryService.SaveAsync(result).GetAwaiter().GetResult();
    var entry = HistoryService.Load().Single();
    Equal(false, entry.IsRounded);
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Console.WriteLine($"  raw 130kg/5/9.5: saved={entry.BestEstimate} reopened={ctx.Current!.BestEstimate} note={ctx.Current.RoundingNote}");
    Equal(150.5, ctx.Current.BestEstimate);
    Equal("(avg of 8 formulas)", ctx.Current.RoundingNote);
    Equal(false, ctx.Current.IsRounded);
});

Test("rounded saved entry reopens with the same rounded best estimate", () =>
{
    Preferences.Default.Set(Pref, "");
    var result = OneRmCalculator.Calculate(110, 6, 7, false, true);
    Equal(135.0, result.BestEstimate);
    HistoryService.SaveAsync(result).GetAwaiter().GetResult();
    var entry = HistoryService.Load().Single();
    Equal(true, entry.IsRounded);
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Console.WriteLine($"  rounded 110kg/6/7: saved={entry.BestEstimate} reopened={ctx.Current!.BestEstimate} note={ctx.Current.RoundingNote}");
    Equal(135.0, ctx.Current.BestEstimate);
    Equal("(avg of 8 formulas, rounded to 2.5)", ctx.Current.RoundingNote);
    Equal(true, ctx.Current.IsRounded);
});

Test("every entry keeps its own rounding mode independently", () =>
{
    Preferences.Default.Set(Pref, "");
    var raw = OneRmCalculator.Calculate(130, 5, 9.5, false, false);
    var rounded = OneRmCalculator.Calculate(130, 5, 9.5, false, true);
    HistoryService.SaveAsync(raw).GetAwaiter().GetResult();
    HistoryService.SaveAsync(rounded).GetAwaiter().GetResult();
    var entries = HistoryService.Load();
    Equal(2, entries.Count);
    var rawEntry = entries.Single(e => e.IsRounded == false);
    var roundedEntry = entries.Single(e => e.IsRounded == true);
    var rawCtx = new ResultContext();
    new HistoryViewModel(rawCtx).OpenEntry(rawEntry).GetAwaiter().GetResult();
    var roundedCtx = new ResultContext();
    new HistoryViewModel(roundedCtx).OpenEntry(roundedEntry).GetAwaiter().GetResult();
    Equal(false, rawCtx.Current!.IsRounded);
    Equal(raw.BestEstimate, rawCtx.Current.BestEstimate);
    Equal(true, roundedCtx.Current!.IsRounded);
    Equal(rounded.BestEstimate, roundedCtx.Current.BestEstimate);
});

Test("reopened entry keeps its own display unit", () =>
{
    Preferences.Default.Set(Pref, "");
    var result = OneRmCalculator.Calculate(100, 5, 8, true, false);
    HistoryService.SaveAsync(result).GetAwaiter().GetResult();
    var entry = HistoryService.Load().Single();
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Equal(true, ctx.Current!.IsLbs);
    Equal(result.BestEstimate, ctx.Current.BestEstimate);
});

// ------------------------------------------------------------ legacy records
Test("legacy record without flag reopens raw when the saved best matches the raw candidate", () =>
{
    Preferences.Default.Set(Pref, LegacyJson(130, 5, 9.5, 150.5));
    var entry = HistoryService.Load().Single();
    Equal(null, entry.IsRounded);
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Console.WriteLine($"  legacy raw 130kg/5/9.5: saved={entry.BestEstimate} reopened={ctx.Current!.BestEstimate} note={ctx.Current.RoundingNote}");
    Equal(150.5, ctx.Current.BestEstimate);
    Equal("(avg of 8 formulas)", ctx.Current.RoundingNote);
    Equal(false, ctx.Current.IsRounded);
});

Test("legacy record without flag reopens raw when the saved best matches the raw candidate (110kg/6/7)", () =>
{
    Preferences.Default.Set(Pref, LegacyJson(110, 6, 7, 133.9));
    var entry = HistoryService.Load().Single();
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Console.WriteLine($"  legacy raw 110kg/6/7: saved={entry.BestEstimate} reopened={ctx.Current!.BestEstimate} note={ctx.Current.RoundingNote}");
    Equal(133.9, ctx.Current.BestEstimate);
    Equal(false, ctx.Current.IsRounded);
});

Test("legacy record without flag reopens rounded when the saved best matches the rounded candidate", () =>
{
    Preferences.Default.Set(Pref, LegacyJson(110, 6, 7, 135.0));
    var entry = HistoryService.Load().Single();
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Console.WriteLine($"  legacy rounded 110kg/6/7: saved={entry.BestEstimate} reopened={ctx.Current!.BestEstimate} note={ctx.Current.RoundingNote}");
    Equal(135.0, ctx.Current.BestEstimate);
    Equal("(avg of 8 formulas, rounded to 2.5)", ctx.Current.RoundingNote);
    Equal(true, ctx.Current.IsRounded);
});

Test("legacy equal-display candidates use the documented rounded fallback", () =>
{
    // Find a fixture where the raw and rounded candidates are exactly the same number.
    double weight = 0; int reps = 0; double rpe = 0; double best = 0;
    for (double w = 100; w <= 200 && best == 0; w += 0.5)
        for (int r = 1; r <= 10 && best == 0; r++)
            for (double p = 6; p <= 10 && best == 0; p += 0.5)
            {
                var raw = OneRmCalculator.Calculate(w, r, p, false, false);
                var rounded = OneRmCalculator.Calculate(w, r, p, false, true);
                if (Math.Abs(raw.BestEstimate - rounded.BestEstimate) < 1e-9) { weight = w; reps = r; rpe = p; best = raw.BestEstimate; }
            }
    if (best == 0) throw new Exception("no tie fixture found");
    Preferences.Default.Set(Pref, LegacyJson(weight, reps, rpe, best));
    var entry = HistoryService.Load().Single();
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Console.WriteLine($"  legacy tie {weight}/{reps}/{rpe}: saved={entry.BestEstimate} reopened={ctx.Current!.BestEstimate} note={ctx.Current.RoundingNote}");
    Equal(best, ctx.Current.BestEstimate);
    Equal(true, ctx.Current.IsRounded);
});

Test("legacy record whose saved best cannot be reproduced keeps the stored best estimate", () =>
{
    Preferences.Default.Set(Pref, LegacyJson(130, 5, 9.5, 999.0));
    var entry = HistoryService.Load().Single();
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Console.WriteLine($"  legacy mismatch: saved={entry.BestEstimate} reopened={ctx.Current!.BestEstimate}");
    Equal(999.0, ctx.Current.BestEstimate);
    Equal("(saved history estimate)", ctx.Current.RoundingNote);
    Equal(999.0, HistoryService.Load().Single().BestEstimate);
});

Test("explicit flag wins over candidate matching and the stored best is preserved", () =>
{
    const string json = "[{\"Date\":\"2026-10-25T11:16:00\",\"Weight\":110,\"Reps\":6,\"Rpe\":7," +
                        "\"BestEstimate\":135,\"Unit\":\"kg\",\"IsLbs\":false,\"IsRounded\":false}]";
    Preferences.Default.Set(Pref, json);
    var entry = HistoryService.Load().Single();
    Equal(false, entry.IsRounded);
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntry(entry).GetAwaiter().GetResult();
    Console.WriteLine($"  explicit raw flag with rounded stored best: saved={entry.BestEstimate} reopened={ctx.Current!.BestEstimate}");
    Equal(false, ctx.Current.IsRounded);
    Equal(135.0, ctx.Current.BestEstimate);
    Equal("(saved history estimate)", ctx.Current.RoundingNote);
});

// Independent review R1: adjacent displayed tenths must never count as a tie.
Test("legacy kg adjacent tenth preserves raw best and caption", () =>
{
    var result = new HistoryEntry(DateTime.UnixEpoch, 50, 9, 8.5, 64.9, "kg").ToResult();
    Equal(64.9, result.BestEstimate);
    Equal(false, result.IsRounded);
    Equal("(avg of 8 formulas)", result.RoundingNote);
});
Test("legacy lbs adjacent tenth preserves raw best and caption", () =>
{
    var result = new HistoryEntry(DateTime.UnixEpoch, 50, 1, 7.5, 112.6, "lbs", true).ToResult();
    Equal(112.6, result.BestEstimate);
    Equal(false, result.IsRounded);
    Equal("(avg of 8 formulas)", result.RoundingNote);
});
Test("explicit raw adjacent stored tenth is preserved", () =>
{
    var result = new HistoryEntry(DateTime.UnixEpoch, 130, 5, 9.5, 150.6, "kg", false, false).ToResult();
    Equal(150.6, result.BestEstimate);
    Equal("(saved history estimate)", result.RoundingNote);
    Equal(false, result.IsRounded);
});
Test("explicit rounded adjacent stored tenth is preserved", () =>
{
    var result = new HistoryEntry(DateTime.UnixEpoch, 130, 5, 9.5, 150.1, "kg", false, true).ToResult();
    Equal(150.1, result.BestEstimate);
    Equal("(saved history estimate)", result.RoundingNote);
    Equal(true, result.IsRounded);
});

// --------------------------------------------------------------- persistence
Test("legacy entries keep their JSON shape while new entries persist the flag", () =>
{
    Preferences.Default.Set(Pref, LegacyJson(130, 5, 9.5, 150.5));
    var raw = OneRmCalculator.Calculate(130, 5, 9.5, false, false);
    HistoryService.SaveAsync(raw).GetAwaiter().GetResult();
    var json = Preferences.Default.Get(Pref, "");
    using var doc = JsonDocument.Parse(json);
    var items = doc.RootElement.EnumerateArray().ToArray();
    Equal(2, items.Length);
    Equal(true, items[0].TryGetProperty("IsRounded", out var newFlag));
    Equal(false, newFlag.GetBoolean());
    Equal(false, items[1].TryGetProperty("IsRounded", out _));
    Equal(150.5, items[1].GetProperty("BestEstimate").GetDouble());
    Equal(130.0, items[1].GetProperty("Weight").GetDouble());
});

Console.WriteLine(failures == 0 ? "ALL RAW HISTORY TESTS PASSED" : $"{failures} RAW HISTORY TEST(S) FAILED");
return failures == 0 ? 0 : 1;
