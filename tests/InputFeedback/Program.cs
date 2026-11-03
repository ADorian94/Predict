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
 foreach(var bad in new[]{"1.234","1,234","1e2","NaN","Infinity","1.2.3","-1","+2","1 000"}) { vm.WeightText="10.25"; vm.WeightText=bad; Check(vm.WeightText=="10.25" && !string.IsNullOrWhiteSpace(vm.WeightError),culture+" rejects edit "+bad); }
 vm.WeightText="1000.01"; int saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null); Check(HistoryService.Saves==saves && vm.WeightError.Length>0,culture+" kg upper bound");
 vm.IsLbs=true; vm.WeightText="220.46"; await vm.CalculateCommand.ExecuteAsync(null); Check(Math.Abs(context.Current!.Weight-220.46*0.45359237)<1e-10,culture+" lbs retains both decimals");
 vm.WeightText="2204.01"; saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null); Check(HistoryService.Saves==saves,culture+" lbs upper bound");
 vm.WeightText="0"; saves=HistoryService.Saves; await vm.CalculateCommand.ExecuteAsync(null); Check(HistoryService.Saves==saves,culture+" zero invalid");
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
