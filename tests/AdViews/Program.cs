global using Microsoft.Maui.Controls;
using Predict.Services;
using Plugin.MauiMtAdmob.Controls;
void Check(bool ok,string message){if(!ok)throw new Exception("FAIL: "+message);}
var b=new Backend(); var g=new AdsConsentCoordinator(b);
var host=new ConsentedBanner {HeightRequest=60,Margin=new Thickness(0,0,0,12),Handler=new Handler(new Provider(g))};
host.RaiseLoaded(); Check(host.Children.Count==0 && MTAdView.Created==0 && host.HeightRequest==0,"unready host must not construct banner");
g.Start(); b.Allowed=true; b.Update!(false); b.Init!();
Check(host.Children.Count==1 && MTAdView.Created==1 && host.HeightRequest==60,"ready creates one measurable banner");
var ad=(MTAdView)host.Children[0];
Check(ad.AdsId==AdUnits.Banner && BannerAdLifecycle.GetObserve(ad),"original unit and lifecycle observed");
ad.Fail(); Check(host.HeightRequest==0 && host.Margin==new Thickness(0),"actual load failure collapses original slot");
ad.LoadedAd(); Check(host.HeightRequest==60 && host.Margin==new Thickness(0,0,0,12),"native sender load restores original slot");
g.ShowPrivacyOptions();
var handler=(Handler)ad.Handler!;var native=(Android.Gms.Ads.AdView)handler.PlatformView!;
Check(host.Children.Count==0 && native.Destroyed && native.Paused && handler.Destroyed && handler.Disconnected,"privacy removes native refresh and disconnects handler");
ad.LoadedAd(); Check(host.HeightRequest==0,"stale native event must not expand blocked slot");
b.Allowed=true; b.Privacy!(); Check(host.Children.Count==1 && MTAdView.Created==2,"allowed privacy callback recreates once");
host.RaiseUnloaded(); Check(host.Children.Count==0,"unloading tears down current banner");
g.Pause();g.Resume();Check(host.Children.Count==0,"unloaded host must not react to service events");
host.RaiseLoaded();Check(host.Children.Count==1 && host.HeightRequest==60,"recycled host restores height on load");
var failingNative=(Android.Gms.Ads.AdView)((MTAdView)host.Children[0]).Handler!.PlatformView;
failingNative.ThrowOnPause=true;
g.Pause();
Check(host.Children.Count==0 && failingNative.Destroyed,"a native pause error must not strand banner refresh or app lifecycle");
MTAdView.FailCreation=true;
g.Resume(); Check(host.Children.Count==0,"banner SDK unavailable must leave app usable with empty slot");
MTAdView.FailCreation=false; g.Pause();g.Resume();Check(host.Children.Count==1,"later lifecycle can retry optional banner");
Console.WriteLine("Banner host tests: 9 passed, 0 failed (empty until ready; original units; failure/load; native destroy; stale event; unload/recycle)");
sealed class Backend:IAdsConsentBackend {
 public bool Allowed;public bool CanRequestAds=>Allowed;public bool PrivacyOptionsRequired=>true;
 public Action<bool>? Update;public Action? Init,Privacy;
 public void RequestUpdate(Action<bool> d)=>Update=d;public void InitializeAds(Action d)=>Init=d;
 public void GatherConsent(Action d)=>d();public void ShowPrivacyOptions(Action d)=>Privacy=d;
 public void SetAdsEnabled(bool b){} public void ClearInterstitial(){}public void LoadInterstitial(){}public bool IsInterstitialLoaded()=>false;public void ShowInterstitial(Action d)=>d();
}
public sealed class Provider(AdsConsentCoordinator gate):IServiceProvider{public object? GetService(Type t)=>t==typeof(AdsConsentCoordinator)?gate:null;}
public sealed class Handler:Plugin.MauiMtAdmob.Interfaces.IMTAdViewHandler {
 public Handler(IServiceProvider? p=null){MauiContext=new MauiContext(p);PlatformView=new Android.Gms.Ads.AdView();}
 public MauiContext MauiContext{get;}public object PlatformView{get;}public bool Destroyed,Disconnected;
 public void Destroy()=>Destroyed=true;public void DisconnectHandler()=>Disconnected=true;
}
public sealed record MauiContext(IServiceProvider? Services);
namespace Microsoft.Extensions.DependencyInjection {public static class Extensions {public static T? GetService<T>(this IServiceProvider? p)=>p?.GetService(typeof(T)) is T t?t:default;}}
namespace Microsoft.Maui.ApplicationModel {public static class MainThread{public static void BeginInvokeOnMainThread(Action a)=>a();}}
namespace Microsoft.Maui.Controls {
 public readonly record struct Thickness(double Left,double Top,double Right,double Bottom){public Thickness(double all):this(all,all,all,all){}}
 public readonly record struct LayoutOptions {public static LayoutOptions Fill=>new();}
 public sealed class BindableProperty {
  public object DefaultValue=false; public Action<BindableObject,object,object>? Changed;
  public static BindableProperty CreateAttached(string n,Type t,Type o,object d,Action<BindableObject,object,object>? propertyChanged=null)=>new(){DefaultValue=d,Changed=propertyChanged};
 }
 public class BindableObject {
  readonly Dictionary<BindableProperty,object> values=[];
  public object GetValue(BindableProperty p)=>values.GetValueOrDefault(p,p.DefaultValue);
  public void SetValue(BindableProperty p,object v){var old=GetValue(p);values[p]=v;p.Changed?.Invoke(this,old,v);}
 }
 public class VisualElement:BindableObject {public Grid? Parent;public Handler? Handler{get;set;}public double HeightRequest{get;set;}public Thickness Margin{get;set;}public LayoutOptions HorizontalOptions{get;set;}public LayoutOptions VerticalOptions{get;set;}}
 public class Grid:VisualElement {
  public Grid(){Children=new Children(this);} public Children Children{get;}
  public event EventHandler? Loaded,Unloaded;public void RaiseLoaded()=>Loaded?.Invoke(this,EventArgs.Empty);public void RaiseUnloaded()=>Unloaded?.Invoke(this,EventArgs.Empty);
 }
 public sealed class Children(Grid owner):System.Collections.ObjectModel.Collection<VisualElement>{
  protected override void InsertItem(int i,VisualElement item){base.InsertItem(i,item);item.Parent=owner;}
  protected override void RemoveItem(int i){this[i].Parent=null;base.RemoveItem(i);}
 }
}
namespace Plugin.MauiMtAdmob.Extra {public enum BannerSize{Banner}public sealed class MTEventArgs:EventArgs{public string ErrorMessage=>"test";public int ErrorCode=>1;}}
namespace Plugin.MauiMtAdmob.Interfaces {public interface IMTAdViewHandler{void Destroy();}}
namespace Plugin.MauiMtAdmob.Controls {
 using Plugin.MauiMtAdmob.Extra;
 public sealed class MTAdView:VisualElement {
  public static int Created; public static bool FailCreation; public MTAdView(){if(FailCreation)throw new Exception("banner SDK unavailable");Created++;Handler=new Handler();}
  public string? AdsId{get;set;}public BannerSize AdSize{get;set;}
  public event EventHandler? AdsLoaded;public event EventHandler<MTEventArgs>? AdsFailedToLoad;
  public void LoadedAd()=>AdsLoaded?.Invoke(new object(),EventArgs.Empty);public void Fail()=>AdsFailedToLoad?.Invoke(new object(),new());
 }
}
namespace Android.Gms.Ads {public sealed class AdView{public bool Destroyed,Paused,ThrowOnPause;public object? AdListener{get;set;}public void Pause(){if(ThrowOnPause)throw new Exception("native teardown unavailable");Paused=true;}public void Destroy()=>Destroyed=true;}}
namespace Android.Util {public static class Log{public static void Info(string tag,string message){}}}
