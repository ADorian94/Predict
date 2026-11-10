using System.Globalization;
using Predict.Models;
using Predict.Services;
using Predict.ViewModels;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var failures = 0; var executed = 0;
var filter = args.FirstOrDefault() ?? "all";
void Test(string group, string name, Action body) {
    if (filter != "all" && group != filter) return;
    executed++;
    try { body(); Console.WriteLine($"PASS: {name}"); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL: {name}: {e.Message}"); }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected <{expected}>, actual <{actual}>"); }
string Share(OneRmResult result) {
    var vm = new ResultViewModel(new ResultContext { Current = result }, new AdsConsentCoordinator());
    try {
        vm.ShareCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Equal("1RM Result", Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.LastRequest!.Title);
        return Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.LastRequest!.Text!;
    } finally { vm.CancelAnimation(); }
}
Test("header", "share starts with exact ASCII header", () => {
    var text = Share(OneRmCalculator.Calculate(170, 3, 8));
    Equal("Predict - 1RM Estimate", text.Split('\n')[0]);
});
foreach (var lbs in new[] { false, true })
foreach (var rounded in new[] { false, true })
foreach (var reps in Enumerable.Range(1, 20))
foreach (var rpe in new[] { 6.0, 8.0, 10.0 })
Test("rpe", $"share RPE boundary reps={reps}, RPE={rpe}, lbs={lbs}, rounded={rounded}", () => {
    var result = OneRmCalculator.Calculate(170, reps, rpe, lbs, rounded);
    var text = Share(result);
    var expectedInput = $"Weight: {(lbs ? $"{170 / 0.45359237:0.##} lbs" : "170 kg")} | Reps: {reps}";
    if (reps <= 10) expectedInput += $" | RPE: {rpe:0.0}";
    Equal(expectedInput, text.Split('\n')[1]);
    Equal(reps <= 10 ? 8 : 7, result.FormulaCount);
    Equal(result.FormulaCount, text.Split('\n').Count(line => line.StartsWith("  ")));
    Equal(result.FormulaCount, result.Formulas.Count);
    if (reps > 10 && (text.Contains("RPE", StringComparison.OrdinalIgnoreCase) || text.Contains("unavailable") || text.Contains("N/A"))) throw new Exception("unsupported share leaks RPE input, RTS row, or availability note");
    foreach (var formula in result.Formulas)
        if (!text.Contains($"  {formula.Name}: {formula.ResultDisplay}")) throw new Exception($"missing formula {formula.Name}");
    if (!text.Contains(result.RoundingNote)) throw new Exception("lost numeric count/rounding note");
});
foreach (var lbs in new[] { false, true })
foreach (var rounded in new[] { false, true })
Test("value", $"share uses final best even before animation completes ({lbs}, {rounded})", () => {
    var result = OneRmCalculator.Calculate(170, 3, 8, lbs, rounded);
    var text = Share(result);
    Equal($"Best estimate: {result.BestEstimate:0.0} {result.Unit}", text.Split('\n')[3]);
});
Test("numeric", "170 kg x 3 @8 has unchanged eight-formula arithmetic", () => {
    var result = OneRmCalculator.Calculate(170, 3, 8, false, false);
    var values = new[] { result.RpeTableResult!.Value, result.EpleyResult, result.BrzycziResult,
        result.LanderResult, result.LombardiResult, result.MayhewResult, result.OConnerResult, result.WathanResult };
    var expected = new[] { 206.1, 187.0, 180.0, 182.2, 189.7, 193.8, 182.8, 185.3 };
    Equal(string.Join(",", expected), string.Join(",", values));
    if (Math.Abs(values.Average() - 188.3625) > 1e-10) throw new Exception("eight-formula raw mean changed");
    Equal(188.4, result.BestEstimate);
    Equal(187.5, (result with { IsRounded = true }).BestEstimate);
    Console.WriteLine($"SAMPLE: values=[{string.Join(", ", values)}]; mean={values.Average():0.0000}; raw-display={result.BestEstimate:0.0}; rounded={(result with { IsRounded = true }).BestEstimate:0.0}");
});
foreach (var reps in Enumerable.Range(1, 20))
foreach (var lbs in new[] { false, true })
foreach (var rounded in new[] { false, true })
Test("numeric", $"mean/count and shared final value reps={reps}, lbs={lbs}, rounded={rounded}", () => {
    var result = OneRmCalculator.Calculate(170, reps, 8, lbs, rounded);
    var values = new List<double> { result.EpleyDisplay, result.BrzycziDisplay, result.LanderDisplay,
        result.LombardiDisplay, result.MayhewDisplay, result.OConnerDisplay, result.WathanDisplay };
    if (reps <= 10) values.Add(result.RpeTableDisplay!.Value);
    Equal(reps <= 10 ? 8 : 7, values.Count);
    var expected = rounded ? Math.Round(values.Average() / 2.5) * 2.5 : Math.Round(values.Average(), 1);
    Equal(expected, result.BestEstimate);
    Equal($"Best estimate: {expected:0.0} {result.Unit}", Share(result).Split('\n')[3]);
});
Test("history", "saved history override remains the shared best estimate", () => {
    var result = OneRmCalculator.Calculate(170, 11, 8) with { BestEstimateOverride = 211.25, EstimatedAt = new DateTime(2026, 10, 1) };
    var text = Share(result);
    Equal($"Best estimate: {result.BestEstimate:0.0} kg", text.Split('\n')[3]);
    if (!text.Contains("(saved history estimate)")) throw new Exception("saved-history caption lost");
    Equal(new DateTime(2026, 10, 1), result.EstimatedAt);
});
Console.WriteLine($"Share: {executed-failures} passed, {failures} failed, {executed} executed");
if (executed == 0) throw new Exception("No share tests selected");
return failures == 0 ? 0 : 1;

public sealed class Shell { public static Shell Current {get;}=new(); public Task GoToAsync(string route)=>Task.CompletedTask; }
namespace Predict.Services {
 public sealed class AdsConsentCoordinator { public void PreloadInterstitial(){} public Task TryShowInterstitialAsync()=>Task.CompletedTask; }
 public static class HapticService { public static void Click(){} }
 public static class MixpanelService { public static void Track(string name){} }
}
namespace Microsoft.Maui.ApplicationModel.DataTransfer {
 public sealed class ShareTextRequest { public string? Title{get;set;} public string? Text{get;set;} }
 public sealed class Share { public static Share Default{get;}=new(); public ShareTextRequest? LastRequest{get;private set;} public Task RequestAsync(ShareTextRequest request){LastRequest=request;return Task.CompletedTask;} }
}
