using System.Globalization;
using Predict;
using Predict.Models;
using Predict.Services;
using Predict.ViewModels;

int passed=0, failed=0;
void Check(bool condition, string label) { if(condition) { passed++; Console.WriteLine("PASS "+label); } else { failed++; Console.WriteLine("FAIL "+label); } }
foreach(var culture in new[]{"en-US","hu-HU"}) {
 CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
 var context = new ResultContext(); var vm=new MainViewModel(context,new AppSettingsService());
 vm.RepsText="1"; Check(!vm.DecrementRepsCommand.CanExecute(null),culture+" lower bound disabled");
 vm.RepsText="20"; Check(!vm.IncrementRepsCommand.CanExecute(null),culture+" upper bound disabled");
 vm.RepsText="5"; Check(vm.DecrementRepsCommand.CanExecute(null)&&vm.IncrementRepsCommand.CanExecute(null),culture+" middle enabled");
 int changes=0; vm.DecrementRepsCommand.CanExecuteChanged+=(_,_)=>changes++; vm.RepsText="1";
 Check(changes>0,culture+" command state notifies");
 vm.RepsText=""; Check(vm.IncrementRepsCommand.CanExecute(null)&&!vm.DecrementRepsCommand.CanExecute(null),culture+" empty recovery");
 vm.IncrementRepsCommand.Execute(null); Check(vm.RepsText=="1",culture+" empty plus recovers to one");
 foreach(var bad in new[]{"0","21","junk"}) { vm.RepsText=bad; Check(!vm.IncrementRepsCommand.CanExecute(null)&&!vm.DecrementRepsCommand.CanExecute(null),culture+" invalid reps disabled "+bad); }
 vm.RepsText="5";
 foreach(var text in new[]{"100.25","100,25","0.01","999.99"}) { vm.WeightText=text; await vm.CalculateCommand.ExecuteAsync(null); Check(context.Current!=null && Math.Abs(context.Current.Weight-double.Parse(text.Replace(',','.'),CultureInfo.InvariantCulture))<1e-10,culture+" exact calculation "+text); }
 vm.WeightText=".5"; Check(vm.WeightText=="0.5",culture+" dot leading zero");
 vm.WeightText=",5"; Check(vm.WeightText=="0,5",culture+" comma leading zero");
 foreach(var bad in new[]{"1.234","1,234","1e2","NaN","Infinity","1.2.3","-1","+2","1 000"}) { vm.WeightText="10.25"; vm.WeightText=bad; Check(vm.WeightText=="10.25" && vm.WeightError=="",culture+" rejects edit "+bad); }
 vm.WeightText="1000.01"; int saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null); Check(HistoryService.Saves==saves && vm.WeightError.Length>0,culture+" kg upper bound");
 vm.IsLbs=true; vm.WeightText="220.46"; await vm.CalculateCommand.ExecuteAsync(null); Check(Math.Abs(context.Current!.Weight-220.46*0.45359237)<1e-10,culture+" lbs retains both decimals");
 vm.WeightText="2204.01"; saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null); Check(HistoryService.Saves==saves,culture+" lbs upper bound");
 vm.WeightText="0"; saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null); Check(HistoryService.Saves==saves,culture+" zero invalid");
}
foreach(var culture in new[]{"en-US","hu-HU"}) {
 CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
 foreach(var lbs in new[]{false,true}) {
  var vm=new MainViewModel(new ResultContext(),new AppSettingsService()); vm.IsLbs=lbs; vm.RepsText="5";
  var unit=lbs?"lbs":"kg"; var max=lbs?AppConstants.MaxWeightLbs:AppConstants.MaxWeightKg;
  foreach(var text in new[]{"0","0.00","0,00"}) {
   vm.WeightText=text; int saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null);
   Check(vm.WeightError==$"Enter a weight greater than 0 {unit}!" && HistoryService.Saves==saves,$"{culture} {unit} lower error {text}: {vm.WeightError}");
  }
  vm.WeightText=(max+0.01).ToString("0.00",CultureInfo.InvariantCulture); await vm.CalculateCommand.ExecuteAsync(null);
  Check(vm.WeightError==$"Enter a weight no greater than {max} {unit}!",$"{culture} {unit} upper error: {vm.WeightError}");
  foreach(var negative in new[]{"-1","-0.01","-0,01"}) {
   vm.WeightText="10.25"; vm.WeightText=negative;
   Check(vm.WeightText=="10.25" && vm.WeightError=="",$"{culture} {unit} negative rejected with lower error {negative}: {vm.WeightError}");
  }
  foreach(var text in new[]{"0.01",max.ToString(CultureInfo.InvariantCulture)}) {
   vm.WeightText=text; int saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null);
   Check(vm.WeightError=="" && HistoryService.Saves==saves+1,$"{culture} {unit} valid boundary {text}");
  }
 }
}
foreach(var culture in new[]{"en-US","hu-HU"}) {
 CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
 foreach(var startLbs in new[]{false,true}) foreach(var viaRefresh in new[]{false,true}) {
  foreach(var invalid in new[]{"0","9999","","1.234","-1"}) {
   var settings=new AppSettingsService { IsLbs=startLbs }; var vm=new MainViewModel(new ResultContext(),settings);
   vm.WeightText="0"; vm.RepsText="0";
   if(invalid is "1.234" or "-1") { vm.WeightText=invalid; await vm.CalculateCommand.ExecuteAsync(null); }
   else { vm.WeightText=invalid; await vm.CalculateCommand.ExecuteAsync(null); }
   Check(vm.WeightError.Length>0,$"{culture} initial error {invalid}");
   var originalText=vm.WeightText; var originalRepsError=vm.RepsError; var saves=HistoryService.Saves;
   int notifications=0; vm.PropertyChanged+=(_,e)=> { if(e.PropertyName==nameof(vm.WeightError)) notifications++; };
   if(viaRefresh) { settings.IsLbs=!startLbs; vm.RefreshUnit(); } else vm.IsLbs=!startLbs;
   Check(vm.WeightError=="" && notifications>0,$"{culture} switch clears visible error {invalid} refresh={viaRefresh} fromLbs={startLbs}");
   Check(vm.WeightText==originalText && vm.RepsError==originalRepsError && HistoryService.Saves==saves,$"{culture} switch preserves input/reps and does not calculate {invalid}");
   vm.RefreshUnit();
   Check(vm.WeightError=="",$"{culture} refresh does not revalidate {invalid}");
   await vm.CalculateCommand.ExecuteAsync(null);
   var expected=invalid==""?"Enter a weight!":invalid=="9999"?$"Enter a weight no greater than {(vm.IsLbs?AppConstants.MaxWeightLbs:AppConstants.MaxWeightKg)} {vm.WeightUnit}!":$"Enter a weight greater than 0 {vm.WeightUnit}!";
   Check(vm.WeightError==expected && HistoryService.Saves==saves,$"{culture} Estimate revalidates selected unit {invalid}: {vm.WeightError}");
  }
 }
}
foreach(var lbs in new[]{false,true}) foreach(var sep in new[]{".",","}) {
 var ctx=new ResultContext(); var vm=new MainViewModel(ctx,new AppSettingsService()); vm.IsLbs=lbs;
 vm.WeightText="0"+sep+"56";
 foreach(var bad in new[]{"0"+sep+"567","junk","0..56","-0.56"}) {
  vm.WeightText=bad;
  Check(vm.WeightText=="0"+sep+"56" && vm.WeightError=="", "accepted visible fraction has no format error "+bad);
  int saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null);
  Check(HistoryService.Saves==saves+1 && vm.WeightError=="", "Estimate uses retained valid fraction");
 }
}
Console.WriteLine($"Input feedback tests: {passed} passed, {failed} failed");
return failed==0?0:1;

public record Color(string Value) { public static Color FromArgb(string value)=>new(value); }
public static class Preferences { public static FakePreferences Default {get;}=new(); }
public sealed class FakePreferences { readonly Dictionary<string,object> data=[]; public T Get<T>(string k,T fallback)=>data.TryGetValue(k,out var v)?(T)v:fallback; public void Set<T>(string k,T value)=>data[k]=value!; }
public sealed class Shell { public static Shell Current {get;}=new(); public Task GoToAsync(string route)=>Task.CompletedTask; }
namespace Predict { public class ResultPage; public class HistoryPage; public class SettingsPage; }
namespace Predict.Services {
 public static class HapticService { public static void Click() {} }
 public static class HistoryService { public static int Saves; public static Task SaveAsync(OneRmResult r) { Saves++; return Task.CompletedTask; } }
 public static class MixpanelService { public static void Track(string name,Dictionary<string,object>? args=null) {} }
}
