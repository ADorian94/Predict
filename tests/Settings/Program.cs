using System.Windows.Input;
using Predict.Services;
using Predict.ViewModels;

var vm = new SettingsViewModel(new AppSettingsService());
if (MixpanelService.Writes != 0 || vm.IsAnalyticsEnabled)
    throw new Exception("FAIL: opening settings must preserve default-off without recording a new choice.");
var enable = typeof(SettingsViewModel).GetProperty("EnableAnalyticsCommand")?.GetValue(vm) as ICommand;
var disable = typeof(SettingsViewModel).GetProperty("DisableAnalyticsCommand")?.GetValue(vm) as ICommand;
if (enable is null || disable is null)
    throw new Exception("FAIL: analytics must have selectable ENABLED/DISABLED card commands.");
disable.Execute(null);
if (MixpanelService.Writes != 1)
    throw new Exception("FAIL: explicitly choosing the already-selected disabled card must remember refusal.");
enable.Execute(null);
if (!vm.IsAnalyticsEnabled || !MixpanelService.IsAnalyticsEnabled || MixpanelService.Writes != 2)
    throw new Exception("FAIL: enabled card must record explicit opt-in.");
disable.Execute(null);
if (vm.IsAnalyticsEnabled || MixpanelService.IsAnalyticsEnabled || MixpanelService.Writes != 3)
    throw new Exception("FAIL: disabled card must withdraw analytics.");
Console.WriteLine("PASS: opening settings does not write consent; both analytics card commands work.");
var changed = new List<string?>();
vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
vm.SelectLightCommand.Execute(null);
foreach (var name in new[] { "AnalyticsEnabledBackground", "AnalyticsDisabledBackground", "AnalyticsEnabledBorder", "AnalyticsDisabledBorder", "AnalyticsEnabledTextColor", "AnalyticsDisabledTextColor" })
    if (!changed.Contains(name)) throw new Exception("FAIL: theme change did not refresh " + name);
Console.WriteLine("PASS: analytics card colors refresh with the existing theme design.");
var details = typeof(SettingsViewModel).GetProperty("ShowAnalyticsDetailsCommand")?.GetValue(vm)
    as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;
if (details is null) throw new Exception("FAIL: detailed analytics disclosure must open in a separate popup.");
await details.ExecuteAsync(null);
if (Shell.Current.LastMessage != MixpanelService.AnalyticsDisclosure || MixpanelService.Writes != 3)
    throw new Exception("FAIL: disclosure popup must show full text without changing consent.");
Console.WriteLine("PASS: details popup shows full disclosure without enabling analytics.");

var ads = new AdsConsentCoordinator();
var adsCtor = typeof(SettingsViewModel).GetConstructor([typeof(AppSettingsService), typeof(AdsConsentCoordinator)]);
if(adsCtor is null) throw new Exception("FAIL: settings advertising privacy does not use production consent gate");
var advertisingVm = (SettingsViewModel)adsCtor.Invoke([new AppSettingsService(), ads]);
var advertisingCommand = typeof(SettingsViewModel).GetProperty("ShowAdvertisingPrivacyCommand")?.GetValue(advertisingVm) as ICommand;
if(advertisingCommand is null) throw new Exception("FAIL: generated advertising privacy command missing");
if((bool)typeof(SettingsViewModel).GetProperty("AdvertisingPrivacyRequired")!.GetValue(advertisingVm)!)
 throw new Exception("FAIL: advertising choices visible without UMP requirement");
advertisingCommand.Execute(null);
if(MixpanelService.Writes != 3) throw new Exception("FAIL: advertising choices changed analytics consent");
Console.WriteLine("PASS: generated advertising privacy command is separate; hidden when UMP not required.");

var advertisingBackend = new AdvertisingBackend();
var liveAds = new AdsConsentCoordinator(advertisingBackend);
var liveSettings = new SettingsViewModel(new AppSettingsService(), liveAds);
var privacyChanges = new List<string?>();
liveSettings.PropertyChanged += (_,e) => privacyChanges.Add(e.PropertyName);
liveAds.Start(); advertisingBackend.Required = true; advertisingBackend.Update!(false);
if(!liveSettings.AdvertisingPrivacyRequired || !privacyChanges.Contains("AdvertisingPrivacyRequired"))
 throw new Exception("FAIL: actual UMP requirement changes must refresh Settings visibility");
liveSettings.ShowAdvertisingPrivacyCommand.Execute(null);
liveSettings.ShowAdvertisingPrivacyCommand.Execute(null);
if(advertisingBackend.Forms != 1 || MixpanelService.Writes != 3)
 throw new Exception("FAIL: generated Settings command must reopen UMP once and never change analytics");
advertisingBackend.Required=false; advertisingBackend.Closed!();
if(liveSettings.AdvertisingPrivacyRequired) throw new Exception("FAIL: Settings must reread UMP requirement after dismissal");
Console.WriteLine("PASS: actual generated advertising command reopens once; live UMP visibility; analytics unchanged.");
Console.WriteLine("Settings tests: 5 passed, 0 failed");

sealed class AdvertisingBackend : IAdsConsentBackend {
 public bool Required; public bool CanRequestAds=>false;public bool PrivacyOptionsRequired=>Required;
 public Action<bool>? Update;public Action? Closed;public int Forms;
 public void RequestUpdate(Action<bool> d)=>Update=d;public void InitializeAds(Action d)=>throw new Exception("No SDK init allowed");
 public void GatherConsent(Action d)=>d();public void ShowPrivacyOptions(Action d){Forms++;Closed=d;}
 public void SetAdsEnabled(bool b){}public void ClearInterstitial()=>throw new Exception("No SDK before init");
 public void LoadInterstitial()=>throw new Exception("No SDK before init");public bool IsInterstitialLoaded()=>throw new Exception("No SDK before init");public void ShowInterstitial(Action d)=>throw new Exception("No SDK before init");
}
public record Color(string Value);
public abstract class Brush;
public sealed class SolidColorBrush(Color color) : Brush { public Color Color { get; } = color; }
public static class Preferences
{
    public static FakePreferences Default { get; } = new();
}
public sealed class FakePreferences
{
    readonly Dictionary<string, object> data = [];
    public T Get<T>(string k, T fallback) => data.TryGetValue(k, out var v) ? (T)v : fallback;
    public void Set<T>(string k, T v) => data[k] = v!;
}
public sealed class Shell
{
    public static Shell Current { get; } = new();
    public Task GoToAsync(string route) => Task.CompletedTask;
    public string? LastMessage { get; private set; }
    public Task DisplayAlert(string title, string message, string cancel)
    { LastMessage = message; return Task.CompletedTask; }
}
namespace Predict.Services
{
    public static class MixpanelService
    {
        public const string AnalyticsDisclosure = "test disclosure";
        public static bool IsAnalyticsEnabled { get; private set; }
        public static int Writes;
        public static void SetAnalyticsEnabled(bool v) { IsAnalyticsEnabled = v; Writes++; }
        public static void Track(string name, Dictionary<string, object>? props = null) { }
    }
    public static class ThemeService
    {
        public static Color CardBg => new("card");
        public static Color AccentCardBg => new("active");
        public static Color CardBorderCol => new("border");
        public static Color AccentLight => new("accent");
        public static Color MutedText => new("muted");
        public static void Apply(string theme) { }
    }
}
