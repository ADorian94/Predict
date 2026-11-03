// Native API contract doubles, not an Android layout/screenshot test.
using Predict.Controls;
int passed=0;
void Check(bool b,string s) { if(!b) throw new Exception(s); passed++; }
var label=new SummaryValueLabel(); label.Attach(null);
foreach(var text in new[]{"0.56 kg","120.55 kg","999.99 kg","1000 kg","120.55 lbs","2203.99 lbs","2204 lbs","0,56 kg","120,55 kg","2203,99 lbs"})
{
 foreach(var width in new[]{64,100,120,160}) foreach(var scale in new[]{1d,1.3,2d})
 {
  var native=new Android.Widget.TextView { Text=text, AvailableWidth=width, FontScale=scale };
  label.Attach(native);
  Check(native.MaxLines==1,"native max lines");
  Check(native.Ellipsize==null,"no numeric truncation");
  Check(!native.HorizontalScrolling,"fit available width rather than offscreen scroll");
  Check(native.Fit==(1,20,1,Android.Util.ComplexUnitType.Sp),"native autosize scaled glyph contract");
  Check(native.Text==text,"complete value preserved");
 }
}
Console.WriteLine($"Summary fit contract: {passed} passed (API doubles, no native geometry/IME assertion)");
namespace Predict.Controls {
 public class Label {
  public HandlerStub? Handler {get;set;}
  protected virtual void OnHandlerChanged() { }
  public void Attach(object? platform) { Handler=new HandlerStub{PlatformView=platform};OnHandlerChanged(); }
 }
 public class HandlerStub {public object? PlatformView {get;set;}}
}
namespace Android.Util { public enum ComplexUnitType {Sp} }
namespace Android.Widget {
 public class TextView {
  public string Text=""; public int AvailableWidth; public double FontScale;
  public int MaxLines; public object? Ellipsize=new(); public bool HorizontalScrolling=true;
  public (int,int,int,Android.Util.ComplexUnitType) Fit;
  public void SetMaxLines(int x)=>MaxLines=x;
  public void SetHorizontallyScrolling(bool x)=>HorizontalScrolling=x;
  public void SetAutoSizeTextTypeUniformWithConfiguration(int min,int max,int step,Android.Util.ComplexUnitType unit)=>Fit=(min,max,step,unit);
 }
}
