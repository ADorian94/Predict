using Predict;
using Predict.Services;
using Plugin.MauiMtAdmob;
using Xamarin.Google.UserMesssagingPlatform;
SynchronizationContext.SetSynchronizationContext(new UiContext());
void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);}
var service=new AndroidAdsConsentBackend(); var a=new MainActivity();
service.Attach(a); service.Resume(a); var info=UserMessagingPlatform.Info;
Check(info.Updates==1 && CrossMauiMTAdmob.Current.Inits==0,"UMP must precede SDK");
info.Allowed=true; info.Failure!.OnConsentInfoUpdateFailure(new FormError());
Check(CrossMauiMTAdmob.Current.Inits==0,"native callbacks must post onto main queue");
Android.OS.Handler.Drain();
Check(service.Coordinator.IsReady && CrossMauiMTAdmob.Current.Inits==1,"initialized callback unlocks only after Init returns");
service.Coordinator.PreloadInterstitial(); Check(CrossMauiMTAdmob.Current.Loads==1,"preload");
CrossMauiMTAdmob.Current.RaiseFailed(); Android.OS.Handler.Drain();
Check(CrossMauiMTAdmob.Current.Loads==1,"failed load must not automatically retry offline/no-fill");
service.Coordinator.PreloadInterstitial();
Check(CrossMauiMTAdmob.Current.Loads==2,"later explicit result preload must retry after native load failure");
Console.WriteLine("PASS native failed load permits later explicit retry without immediate retry");
CrossMauiMTAdmob.Current.RaiseFailed(); Android.OS.Handler.Drain();
int beforeThrow=CrossMauiMTAdmob.Current.Loads;
CrossMauiMTAdmob.Current.ThrowNextLoad=true;
service.Coordinator.PreloadInterstitial();
Check(CrossMauiMTAdmob.Current.Loads==beforeThrow+1,"native load exception must not loop");
service.Coordinator.PreloadInterstitial();
Check(CrossMauiMTAdmob.Current.Loads==beforeThrow+2,"native load exception must release both backend and coordinator admission");
Console.WriteLine("PASS native load exception releases backend and coordinator for later explicit retry");
service.Coordinator.ShowPrivacyOptions();
Check(!service.Coordinator.IsReady && CrossMauiMTAdmob.Current.Clears==1,"privacy suspends and clears pending load");
info.Allowed=false; UserMessagingPlatform.Dismissed!.OnConsentFormDismissed(new FormError()); Android.OS.Handler.Drain();
CrossMauiMTAdmob.Current.RaiseLoaded(); Android.OS.Handler.Drain();
Check(!service.Coordinator.IsReady && CrossMauiMTAdmob.Current.Clears==2,"late old-consent load discarded even after form error");
Console.WriteLine("PASS native boundary order, main-thread posting, privacy error/live state, late cache invalidation");
info.Allowed=true; service.Resume(a);
int beforePrivacy=CrossMauiMTAdmob.Current.Loads;
service.Coordinator.ShowPrivacyOptions();
UserMessagingPlatform.Dismissed!.OnConsentFormDismissed(null); Android.OS.Handler.Drain();
Check(service.Coordinator.IsReady && CrossMauiMTAdmob.Current.Loads==beforePrivacy,"valid replacement waits for invalidated native load");
CrossMauiMTAdmob.Current.RaiseFailed(); Android.OS.Handler.Drain();
Check(CrossMauiMTAdmob.Current.Loads==beforePrivacy+1,"old invalidated load failure must execute the queued valid replacement");
CrossMauiMTAdmob.Current.RaiseFailed(); Android.OS.Handler.Drain();
Check(CrossMauiMTAdmob.Current.Loads==beforePrivacy+1,"replacement failure must stop rather than loop");
service.Coordinator.PreloadInterstitial(); service.Coordinator.PreloadInterstitial();
Check(CrossMauiMTAdmob.Current.Loads==beforePrivacy+2,"replacement failure releases admission for one later explicit retry");
Console.WriteLine("PASS invalidated failure executes queued replacement once; replacement failure admits later explicit retry");
CrossMauiMTAdmob.Current.RaiseLoaded(); Android.OS.Handler.Drain();
service.Detach(a); a.IsDestroyed=true;
Check(CrossMauiMTAdmob.Current.CurrentActivity is null, "plugin must release destroyed Activity, not only our weak reference");
var b=new MainActivity(); service.Attach(b); service.Resume(b);
Check(info.Updates==2,"new activity requests fresh UMP update");
Check(ReferenceEquals(CrossMauiMTAdmob.Current.CurrentActivity,b),"plugin activity updates without a second Init");
Console.WriteLine("PASS destroyed Activity replaced without retaining current activity or double SDK init");
info.Allowed=true; info.Failure!.OnConsentInfoUpdateFailure(new FormError()); Android.OS.Handler.Drain();
CrossMauiMTAdmob.Current.RaiseLoaded(); Android.OS.Handler.Drain();
CrossMauiMTAdmob.Current.AutoClose=false;
var showing=service.Coordinator.TryShowInterstitialAsync();
Check(CrossMauiMTAdmob.Current.ClosedSubscriptions==1 && !showing.IsCompleted,"native close subscribed while showing");
service.Detach(b);
Check(showing.IsCompleted,"detach must release navigation without waiting for a native terminal event");
foreach(bool oldFailed in new[]{false,true}) {
 var replacement=new MainActivity(); service.Attach(replacement); service.Resume(replacement);
 // Finish any invalidated load and its queued replacement before testing presentation.
 CrossMauiMTAdmob.Current.RaiseLoaded(); Android.OS.Handler.Drain();
 CrossMauiMTAdmob.Current.RaiseLoaded(); Android.OS.Handler.Drain();
 int nativeShows=CrossMauiMTAdmob.Current.Shows;
 var attempted=service.Coordinator.TryShowInterstitialAsync();
 bool returnedImmediately=attempted.IsCompleted && !AdsConsentPresentation.IsBusy;
 // Raise on the actual plugin singleton after the replacement show attempt,
 // before asserting, so the broken implementation actually delivers A's event to B.
 if(oldFailed) CrossMauiMTAdmob.Current.RaiseShowFailed(); else CrossMauiMTAdmob.Current.RaiseClosed();
 Android.OS.Handler.Drain();
 Check(returnedImmediately,"missing old native terminal must skip new interstitial immediately, not hold navigation or presentation scheduling");
 Check(CrossMauiMTAdmob.Current.Shows==nativeShows && attempted.IsCompleted,
  "uncorrelated old singleton terminal must not be delivered to a replacement native show; skip replacement until retirement");
 Check(!AdsConsentPresentation.IsBusy,"retiring old native presentation must not strand calculator/privacy scheduling");
 Check(CrossMauiMTAdmob.Current.ClosedSubscriptions==0,"retired native handler must unsubscribe");
 var next=service.Coordinator.TryShowInterstitialAsync();
 Check(CrossMauiMTAdmob.Current.Shows==nativeShows+1 && !next.IsCompleted && AdsConsentPresentation.IsBusy,
  "conclusive old terminal must permit a later native show with its own navigation/lease");
 if(oldFailed) CrossMauiMTAdmob.Current.RaiseShowFailed(); else CrossMauiMTAdmob.Current.RaiseClosed();
 Android.OS.Handler.Drain();
 Check(next.IsCompleted && !AdsConsentPresentation.IsBusy,"current native terminal completes current navigation");
 // Leave another native presentation pending for the next replacement iteration.
 CrossMauiMTAdmob.Current.RaiseLoaded(); Android.OS.Handler.Drain();
 var abandoned=service.Coordinator.TryShowInterstitialAsync();
 Check(!abandoned.IsCompleted,"start presentation to be retired across Activity destruction");
 service.Detach(replacement);
 Check(abandoned.IsCompleted && !AdsConsentPresentation.IsBusy,"missing native terminal cannot block navigation or scheduler indefinitely");
}
CrossMauiMTAdmob.Current.RaiseClosed(); Android.OS.Handler.Drain();
Console.WriteLine("PASS singleton close/failure quarantined across Activity replacement; navigation remains usable without native terminal");
Console.WriteLine("Android boundary scenarios passed, 0 failed (platform APIs doubled, NOT device integration)");
sealed class UiContext:SynchronizationContext { public override void Post(SendOrPostCallback d,object? state)=>Android.OS.Handler.Queue.Enqueue(()=>d(state)); }
namespace Predict { public sealed class MainActivity {public bool IsFinishing, IsDestroyed;} }
namespace Microsoft.Maui.ApplicationModel { public sealed class Placeholder; }
namespace Java.Lang { public class Object; }
namespace Android.App { public sealed class Application {public static object Context {get;}=new();} }
namespace Android.OS {
 public sealed class Looper {public static Looper MainLooper {get;}=new();}
 public sealed class Handler {
  public Handler(Looper looper) { _ = looper; }
  public static Queue<Action> Queue=[]; public void Post(Action a)=>Queue.Enqueue(a);
  public static void Drain(){int n=0;while(Queue.TryDequeue(out var a)){a();if(++n>100)throw new Exception("callback loop");}}
 }
}
namespace Xamarin.Google.UserMesssagingPlatform {
 public sealed class FormError;
 public enum ConsentInformationPrivacyOptionsRequirementStatus {Required,NotRequired,Unknown}
 public sealed class ConsentRequestParameters {public sealed class Builder {public ConsentRequestParameters Build()=>new();}}
 public interface IConsentInformationOnConsentInfoUpdateSuccessListener {void OnConsentInfoUpdateSuccess();}
 public interface IConsentInformationOnConsentInfoUpdateFailureListener {void OnConsentInfoUpdateFailure(FormError e);}
 public interface IConsentFormOnConsentFormDismissedListener {void OnConsentFormDismissed(FormError? e);}
 public interface IConsentInformation {
  bool CanRequestAds(); ConsentInformationPrivacyOptionsRequirementStatus PrivacyOptionsRequirementStatus {get;}
  void RequestConsentInfoUpdate(MainActivity a,ConsentRequestParameters p,IConsentInformationOnConsentInfoUpdateSuccessListener s,IConsentInformationOnConsentInfoUpdateFailureListener f);
 }
 public sealed class Info:IConsentInformation {
  public bool Allowed; public int Updates; public IConsentInformationOnConsentInfoUpdateSuccessListener? Success; public IConsentInformationOnConsentInfoUpdateFailureListener? Failure;
  public bool CanRequestAds()=>Allowed; public ConsentInformationPrivacyOptionsRequirementStatus PrivacyOptionsRequirementStatus=>ConsentInformationPrivacyOptionsRequirementStatus.Required;
  public void RequestConsentInfoUpdate(MainActivity a,ConsentRequestParameters p,IConsentInformationOnConsentInfoUpdateSuccessListener s,IConsentInformationOnConsentInfoUpdateFailureListener f){Updates++;Success=s;Failure=f;}
 }
 public static class UserMessagingPlatform {
  public static Info Info {get;}=new(); public static IConsentFormOnConsentFormDismissedListener? Dismissed;
  public static IConsentInformation GetConsentInformation(object context)=>Info;
  public static void ShowPrivacyOptionsForm(MainActivity a,IConsentFormOnConsentFormDismissedListener l)=>Dismissed=l;
  public static void LoadAndShowConsentFormIfRequired(MainActivity a,IConsentFormOnConsentFormDismissedListener l)=>Dismissed=l;
 }
}
namespace Plugin.MauiMtAdmob.Extra {public sealed class MTEventArgs:EventArgs;}
namespace Plugin.MauiMtAdmob {
 using Plugin.MauiMtAdmob.Extra;
 public static class CrossMauiMTAdmob {public static Fake Current {get;}=new();}
 public sealed class Fake {
  public bool AutoClose=true; public int ClosedSubscriptions=>OnInterstitialClosed?.GetInvocationList().Length??0;
  public bool IsEnabled, Returned; public int Inits, Loads, Clears;
  public MainActivity? CurrentActivity {get; private set;}
  public event EventHandler? OnMobileAdsInitialized,OnInterstitialLoaded,OnInterstitialClosed;
  public event EventHandler<MTEventArgs>? OnInterstitialFailedToLoad,OnInterstitialFailedToShow;
  public void Init(MainActivity a,string appId,bool initialiseConsentAtStartup){Inits++;OnMobileAdsInitialized?.Invoke(this,EventArgs.Empty);Returned=true;CurrentActivity=a;}
  public bool ThrowNextLoad;
  public void LoadInterstitial(string id){if(!Returned)throw new Exception("SDK used before Init returned");Loads++;if(ThrowNextLoad){ThrowNextLoad=false;throw new InvalidOperationException("native load unavailable");}}
  private bool loaded;
  public void EmptyInterstitialAdsList(){Clears++;loaded=false;}
  public bool IsInterstitialLoaded()=>loaded; public int Shows;
  public void ShowInterstitial(){if(!loaded)throw new InvalidOperationException("show without loaded ad");loaded=false;Shows++;if(AutoClose)OnInterstitialClosed?.Invoke(this,EventArgs.Empty);}
  public void RaiseClosed()=>OnInterstitialClosed?.Invoke(this,EventArgs.Empty);
  public void RaiseLoaded(){loaded=true;OnInterstitialLoaded?.Invoke(this,EventArgs.Empty);}
  public void RaiseFailed(){loaded=false;OnInterstitialFailedToLoad?.Invoke(this,new());}
  public void RaiseShowFailed()=>OnInterstitialFailedToShow?.Invoke(this,new());
 }
}
