using System.Globalization;
using System.Text.Json;
using Predict.Models;
using Predict.Services;
using Predict.ViewModels;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var failures = 0;
var executed = 0;
void Test(string name, Action body) { executed++; try { body(); Console.WriteLine($"PASS: {name}"); } catch(Exception e) { failures++; Console.WriteLine($"FAIL: {name}: {e.Message}"); } }
void Equal<T>(T expected, T actual) { if(!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected <{expected}>, actual <{actual}>"); }
const string Key = Predict.AppConstants.PrefHistory;
Test("saved screenshot entry date flows through production navigation to detail without storage mutation", () => {
    var json = "[{\"Date\":\"2026-11-07T18:58:00\",\"Weight\":85,\"Reps\":3,\"Rpe\":9,\"BestEstimate\":92.5,\"Unit\":\"kg\"}]";
    Preferences.Default.Set(Key, json);
    var saved = HistoryService.Load().Single();
    var ctx = new ResultContext();
    new HistoryViewModel(ctx).OpenEntryCommand.ExecuteAsync(saved).GetAwaiter().GetResult();
    var vm = new ResultViewModel(ctx, new AdsConsentCoordinator()); vm.CancelAnimation();
    Equal("ResultPage", Shell.Current.LastRoute);
    Equal(saved.DateDisplay, vm.EstimationDate);
    Equal(json, Preferences.Default.Get(Key, ""));
    Equal(1, HistoryService.Load().Count);
    Equal(92.5, ctx.Current!.BestEstimate);
});
Test("save uses result creation instant rather than save scheduling instant", () => {
    Preferences.Default.Set(Key, "");
    var created = new DateTime(2026, 11, 7, 18, 58, 37, DateTimeKind.Local);
    var result = OneRmCalculator.Calculate(85, 3, 9, false, true) with { EstimatedAt = created };
    HistoryService.SaveAsync(result).GetAwaiter().GetResult();
    Equal(created, HistoryService.Load().Single().Date);
});
foreach (var lbs in new[] { false, true })
foreach (var rounded in new[] { false, true })
{
    Test($"new calculation keeps one creation instant through save/detail/share ({lbs}, {rounded})", () => {
        Preferences.Default.Set(Key, "");
        var settings = new AppSettingsService { IsLbs = lbs, IsRounded = rounded };
        var ctx = new ResultContext();
        var before = DateTime.Now;
        var main = new MainViewModel(ctx, settings) { WeightText = "85", RepsText = "3", Rpe = 9 };
        main.CalculateCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var after = DateTime.Now;
        var result = ctx.Current!;
        if (result.EstimatedAt < before || result.EstimatedAt > after) throw new Exception("creation instant outside calculation interval");
        var saved = HistoryService.Load().Single();
        Equal(result.EstimatedAt, saved.Date);
        Equal(lbs, saved.IsLbs); Equal<bool?>(rounded, saved.IsRounded);
        Equal(result.BestEstimate, saved.BestEstimate);
        var originalJson = Preferences.Default.Get(Key, "");
        var vm = new ResultViewModel(ctx, new AdsConsentCoordinator()); vm.CancelAnimation();
        Equal(saved.DateDisplay, vm.EstimationDate);
        vm.ShareCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var share = Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.LastRequest!.Text!;
        if (!share.Contains(result.Unit) || !share.Contains("Epley") || !share.Contains(result.RoundingNote)) throw new Exception("share content regression");
        Equal(originalJson, Preferences.Default.Get(Key, ""));
        Equal(saved.DateDisplay, vm.EstimationDate);
    });
    Test($"old saved entry keeps exact date/unit/rounding after repeated navigation ({lbs}, {rounded})", () => {
        var date = new DateTime(2026, 11, 7, 18, 58, 37, DateTimeKind.Local);
        var calculated = OneRmCalculator.Calculate(85, 3, 9, lbs, rounded);
        var entry = new HistoryEntry(date, 85, 3, 9, calculated.BestEstimate, calculated.Unit, lbs, rounded);
        var json = JsonSerializer.Serialize(new[] { entry }); Preferences.Default.Set(Key, json);
        var ctx = new ResultContext(); var history = new HistoryViewModel(ctx);
        for (var i = 0; i < 2; i++) {
            history.OpenEntryCommand.ExecuteAsync(HistoryService.Load().Single()).GetAwaiter().GetResult();
            var vm = new ResultViewModel(ctx, new AdsConsentCoordinator()); vm.CancelAnimation();
            Equal(entry.DateDisplay, vm.EstimationDate); Equal(date, ctx.Current!.EstimatedAt);
            Equal(rounded, ctx.Current.IsRounded); Equal(lbs, ctx.Current.IsLbs);
            Equal(entry.BestEstimate, ctx.Current.BestEstimate); Equal(calculated.RoundingNote, vm.RoundingNote);
            vm.ShareCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        }
        Equal(json, Preferences.Default.Get(Key, "")); Equal(1, HistoryService.Load().Count);
    });
}
Test("creation instant is stable even when viewing an older result later", () => {
    var old = new DateTime(2026, 11, 7, 18, 58, 37);
    var result = OneRmCalculator.Calculate(85, 3, 9) with { EstimatedAt = old };
    var vm = new ResultViewModel(new ResultContext { Current = result }, new AdsConsentCoordinator()); vm.CancelAnimation();
    Equal("2026.11.07 18:58", vm.EstimationDate);
    Equal(old, (result with { BestEstimateOverride = 92.5 }).EstimatedAt);
    Equal(vm.EstimationDate, vm.EstimationDate);
});
Test("switching entries never leaks previous date or rewrites legacy JSON", () => {
    var json = "[{\"Date\":\"2026-11-07T18:58:00\",\"Weight\":85,\"Reps\":3,\"Rpe\":9,\"BestEstimate\":92.5,\"Unit\":\"kg\"},{\"Date\":\"2020-01-02T03:04:05Z\",\"Weight\":100,\"Reps\":5,\"Rpe\":8,\"BestEstimate\":260,\"Unit\":\"lbs\",\"IsLbs\":true}]";
    Preferences.Default.Set(Key,json); var ctx = new ResultContext(); var h = new HistoryViewModel(ctx);
    foreach(var entry in HistoryService.Load()) {
        h.OpenEntryCommand.ExecuteAsync(entry).GetAwaiter().GetResult();
        var vm=new ResultViewModel(ctx,new AdsConsentCoordinator());vm.CancelAnimation();
        Equal(entry.DateDisplay,vm.EstimationDate);Equal(entry.Date,ctx.Current!.EstimatedAt);Equal(entry.Date.Kind,ctx.Current.EstimatedAt.Kind);
        Equal(entry.BestEstimate,ctx.Current.BestEstimate);
    }
    Equal(json,Preferences.Default.Get(Key,""));Equal(2,HistoryService.Load().Count);
});
Test("empty result does not invent a current estimation date", () => {
    var vm = new ResultViewModel(new ResultContext(), new AdsConsentCoordinator());vm.CancelAnimation();Equal("",vm.EstimationDate);
});
Test("midnight boundary and subsecond precision survive reopening without timezone conversion", () => {
    var json = "[{\"Date\":\"2026-11-07T23:59:59.1234567\",\"Weight\":85,\"Reps\":3,\"Rpe\":9,\"BestEstimate\":92.5,\"Unit\":\"kg\"},{\"Date\":\"2026-11-08T00:00:00.0000001\",\"Weight\":85,\"Reps\":3,\"Rpe\":9,\"BestEstimate\":92.5,\"Unit\":\"kg\"}]";
    Preferences.Default.Set(Key,json); var ctx=new ResultContext();var history=new HistoryViewModel(ctx);
    var entries=HistoryService.Load();var expected=new[]{"2026.11.07 23:59","2026.11.08 00:00"};
    for(var i=0;i<entries.Count;i++) {
        history.OpenEntryCommand.ExecuteAsync(entries[i]).GetAwaiter().GetResult();
        var vm=new ResultViewModel(ctx,new AdsConsentCoordinator());vm.CancelAnimation();
        Equal(expected[i],vm.EstimationDate);Equal(entries[i].Date.Ticks,ctx.Current!.EstimatedAt.Ticks);Equal(DateTimeKind.Unspecified,ctx.Current.EstimatedAt.Kind);
    }
    Equal(json,Preferences.Default.Get(Key,""));Equal(2,HistoryService.Load().Count);
});
Console.WriteLine($"HistoryDate: {executed-failures} passed, {failures} failed");
return failures == 0 ? 0 : 1;

// Only platform boundaries are doubles; production model, persistence, calculator,
// navigation VMs and source-generated MVVM commands are linked directly.
public record Color(string Value) { public static Color FromArgb(string value)=>new(value); }
public sealed class Shell { public static Shell Current {get;}=new(); public string? LastRoute {get;private set;} public Task GoToAsync(string route) { LastRoute=route; return Task.CompletedTask; } }
public static class Preferences { public static FakePreferences Default {get;}=new(); }
public sealed class FakePreferences { readonly Dictionary<string,object> values=[]; public T Get<T>(string key,T fallback)=>values.TryGetValue(key,out var value)?(T)value:fallback; public void Set<T>(string key,T value)=>values[key]=value!; }
namespace Predict { public class ResultPage; public class HistoryPage; public class SettingsPage; }
namespace Predict.Services {
 public sealed class AdsConsentCoordinator { public void PreloadInterstitial(){} public Task TryShowInterstitialAsync()=>Task.CompletedTask; }
 public static class HapticService { public static void Click(){} }
 public static class MixpanelService { public static void Track(string name,Dictionary<string,object>? data=null){} }
}
namespace Microsoft.Maui.ApplicationModel.DataTransfer {
 public sealed class ShareTextRequest { public string? Title{get;set;} public string? Text{get;set;} }
 public sealed class Share { public static Share Default{get;}=new(); public ShareTextRequest? LastRequest{get;private set;} public Task RequestAsync(ShareTextRequest request){LastRequest=request;return Task.CompletedTask;} }
}
