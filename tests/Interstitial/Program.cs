using Predict.Services;
using Predict.ViewModels;
var backend = new Backend(); var gate = new AdsConsentCoordinator(backend);
var ctor = typeof(ResultViewModel).GetConstructor([typeof(ResultContext), typeof(AdsConsentCoordinator)]);
if(ctor is null) throw new Exception("FAIL: actual ResultViewModel does not use the consent gate.");
var vm = (ResultViewModel)ctor.Invoke([new ResultContext(),gate]);
if(backend.Loads != 0 || backend.Queries != 0) throw new Exception("FAIL constructor touched uninitialized SDK");
await vm.GoBackCommand.ExecuteAsync(null); await vm.NewEstimationCommand.ExecuteAsync(null); await vm.GoBackCommand.ExecuteAsync(null);
if(Shell.Current.Routes.Count != 3 || backend.Queries != 0) throw new Exception("FAIL gated navigation");
gate.Start(); backend.Allowed = true; backend.Update!(false); backend.Init!();
if(backend.Loads != 1) throw new Exception("FAIL delayed preload");
await vm.GoBackCommand.ExecuteAsync(null); await vm.NewEstimationCommand.ExecuteAsync(null); await vm.GoBackCommand.ExecuteAsync(null);
if(backend.Shows != 1 || backend.Loads != 2 || Shell.Current.Routes.Count != 6) throw new Exception("FAIL third exit/show/reload/navigation");
backend.Allowed = false;
await vm.GoBackCommand.ExecuteAsync(null); await vm.GoBackCommand.ExecuteAsync(null); await vm.GoBackCommand.ExecuteAsync(null);
if(backend.Shows != 1 || backend.Queries != 1 || backend.Loads != 2) throw new Exception("FAIL live withdrawal gate");
Console.WriteLine("Interstitial VM tests: 4 passed, 0 failed (constructor, deferred preload, third-exit reload, withdrawal navigation)");
sealed class Backend : IAdsConsentBackend {
 public bool Allowed; public bool CanRequestAds=>Allowed; public bool PrivacyOptionsRequired=>false;
 public int Loads, Queries, Shows; public Action<bool>? Update; public Action? Init;
 public void RequestUpdate(Action<bool> done)=>Update=done;
 public void InitializeAds(Action done)=>Init=done;
 public void GatherConsent(Action done)=>done(); public void ShowPrivacyOptions(Action done)=>done();
 public void SetAdsEnabled(bool enabled) {} public void ClearInterstitial() {}
 public void LoadInterstitial()=>Loads++;
 public bool IsInterstitialLoaded(){Queries++;return true;}
 public void ShowInterstitial(Action done){Shows++;done();}
}
public sealed class Shell {
 public static Shell Current {get;}=new(); public List<string> Routes=[];
 public Task GoToAsync(string route){Routes.Add(route);return Task.CompletedTask;}
}
namespace Predict.Services { public static class HapticService { public static void Click(){} } public static class MixpanelService { public static void Track(string n){} } }
namespace Microsoft.Maui.ApplicationModel.DataTransfer {
 public sealed class ShareTextRequest { public string? Title {get;set;} public string? Text {get;set;} }
 public sealed class Share { public static Share Default {get;}=new(); public Task RequestAsync(ShareTextRequest r)=>Task.CompletedTask; }
}
