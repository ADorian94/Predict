global using Microsoft.Maui.Controls;
using Predict.Services;
using Plugin.MauiMtAdmob.Controls;
int checks=0;
void Check(bool ok,string message){checks++;if(!ok)throw new Exception("FAIL: "+message);}
var gate = new AdsConsentCoordinator();
var banners = Enumerable.Range(0,3).Select(_=>new ConsentedBanner {HeightRequest=60,Margin=new Thickness(0,0,0,12),Handler=new Handler(new Provider(gate))}).ToArray();
foreach(var host in banners)host.RaiseLoaded();
for(int cycle=0;cycle<20;cycle++){
 gate.Start();gate.Resume();gate.PreloadInterstitial();gate.ShowPrivacyOptions();
 Check(!gate.IsReady && !gate.PrivacyOptionsRequired,"default coordinator never grants advertising readiness/privacy");
 var navigation=gate.TryShowInterstitialAsync();
 Check(navigation.IsCompletedSuccessfully,"ordinary navigation completes synchronously without waiting for ads");
 await navigation;
 foreach(var host in banners){
  Check(host.Children.Count==0 && host.HeightRequest==0 && host.Margin==new Thickness(0),"empty slot collapses height and margin to zero");
  host.RaiseUnloaded();host.RaiseLoaded();
 }
 gate.Pause();gate.Resume();gate.Detach();gate.PreloadInterstitial();
 Check(gate.TryShowInterstitialAsync().IsCompletedSuccessfully && !gate.IsReady,"detached navigation remains immediate");
}
Check(MTAdView.Created==0,"no native banner was ever constructed/requested");
Check(!AdsConsentPresentation.IsBusy,"no advertising presentation lease strands navigation");
Console.WriteLine($"Screenshot null-backend checks: {checks} passed, 0 failed (20 lifecycle cycles, three empty slots, synchronous navigation)");
public sealed class Provider(AdsConsentCoordinator gate):IServiceProvider{public object? GetService(Type t)=>t==typeof(AdsConsentCoordinator)?gate:null;}
public sealed class Handler:Plugin.MauiMtAdmob.Interfaces.IMTAdViewHandler {
 public Handler(IServiceProvider? p=null){MauiContext=new MauiContext(p);PlatformView=new Android.Gms.Ads.AdView();}
 public MauiContext? MauiContext{get;set;}public object? PlatformView{get;private set;}public bool Destroyed,Disconnected;
 public VisualElement? VirtualView;
 public void Destroy(){Destroyed=true;}
 public void DisconnectHandler(){Disconnected=true;PlatformView=null;if(VirtualView?.Handler==this)VirtualView.Handler=null;VirtualView=null;}
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
 public class VisualElement:BindableObject {public Grid? Parent;private Handler? handler;public Handler? Handler{get=>handler;set{handler=value;if(value is not null)value.VirtualView=this;}}public double HeightRequest{get;set;}public Thickness Margin{get;set;}public LayoutOptions HorizontalOptions{get;set;}public LayoutOptions VerticalOptions{get;set;}}
 public class Grid:VisualElement {
  public Grid(){Children=new Children(this);} public Children Children{get;}
  public event EventHandler? Loaded,Unloaded;public void RaiseLoaded()=>Loaded?.Invoke(this,EventArgs.Empty);public void RaiseUnloaded()=>Unloaded?.Invoke(this,EventArgs.Empty);
 }
 public sealed class Children(Grid owner):System.Collections.ObjectModel.Collection<VisualElement>{
  protected override void InsertItem(int i,VisualElement item){base.InsertItem(i,item);item.Parent=owner;if(item.Handler?.PlatformView is Android.Gms.Ads.AdView native)native.Attached=true;}
  // MAUI Layout.RemoveAt mutates managed children/logical parent before OnRemove -> LayoutHandler.Remove -> ToPlatform.
  protected override void RemoveItem(int i){var child=this[i];child.Parent=null;base.RemoveItem(i);if(owner.Handler is not null){var h=child.Handler??throw new InvalidOperationException("MauiContext should have been set on parent.");var native=h.PlatformView as Android.Gms.Ads.AdView??throw new InvalidOperationException("Unable to convert child to platform view");native.Attached=false;native.Steps.Add("remove");}AfterRemove?.Invoke(child);}
  public Action<VisualElement>? AfterRemove;
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
namespace Android.Gms.Ads {public sealed class AdView{public bool Destroyed,Paused,ThrowOnPause,Attached;public List<string> Steps=[];public object? AdListener{get;set;}public void Pause(){Steps.Add("pause");if(ThrowOnPause)throw new Exception("native teardown unavailable");Paused=true;}public void Destroy(){Steps.Add(Attached?"destroy-attached":"destroy");Destroyed=true;}}}
namespace Android.Util {public static class Log{public static void Info(string tag,string message){}}}
