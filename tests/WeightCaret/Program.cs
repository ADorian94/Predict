using Android.Text;
using Predict.Platforms.Android;
using System.Text.RegularExpressions;

int passed = 0, failed = 0;
void Eq<T>(T want, T got, string name)
{
    if (Equals(want, got)) { passed++; return; }
    failed++; Console.WriteLine($"FAIL {name}: expected {want}, actual {got}");
}
var filter = new WeightLeadingSeparatorFilter();
(string text, int cursor) Edit(string old, int a, int b, string input)
{
    var source = new SpannableStringBuilder(input);
    var replacement = filter.FilterFormatted(source, 0, input.Length, new SpannableStringBuilder(old), a, b)?.ToString() ?? input;
    var text = old[..a] + replacement + old[b..];
    // Model the native transaction's insertion length, which is chosen before
    // text-change callbacks. A later binding rewrite is a separate text update;
    // it does not make the original one-character insertion a two-character edit.
    var cursor = a + replacement.Length;
    if (!Regex.IsMatch(text, @"\A[0-9]*(?:[.,][0-9]{0,2})?\z")) return (old, Math.Min(cursor, old.Length));
    if (text.StartsWith('.') || text.StartsWith(',')) text = "0" + text;
    return (text, cursor);
}
foreach (var sep in new[] { ".", "," })
{
    var first = Edit("", 0, 0, sep);
    Eq("0" + sep, first.text, "normalized separator");
    Eq(2, first.cursor, "caret after separator");
    Eq(("0" + sep + "5", 3), Edit(first.text, first.cursor, first.cursor, "5"), "next digit is fractional");
    Eq(("0" + sep + "52", 4), Edit("", 0, 0, sep + "52"), "pasted leading fraction");
    Eq(("0" + sep + "5", 3), Edit("123", 0, 3, sep + "5"), "selection replacement");
    Eq(("0" + sep + "5", 2), Edit("5", 0, 0, sep), "prefix before existing digit");
}
Eq(("12.5", 2), Edit("1.5", 1, 1, "2"), "mid-string insertion not moved to end");
Eq(("17.5", 2), Edit("12.5", 1, 2, "7"), "mid-string selection replacement");
Eq(("12.5", 1), Edit("172.5", 1, 2, ""), "mid-string delete");
foreach (var text in new[] {".123", ",123", ".x", "-1", "1.234", "1..", "1,2.3"})
    Eq<Java.Lang.ICharSequence?>(null, filter.FilterFormatted(new SpannableStringBuilder(text), 0, text.Length, new SpannableStringBuilder(""), 0, 0), "rejected candidate delegated unchanged " + text);
Eq(("1.23", 4), Edit("1.23", 4, 4, "4"), "rejected third decimal unchanged");
Eq<Java.Lang.ICharSequence?>(null, filter.FilterFormatted(new SpannableStringBuilder("2"), 0, 1, new SpannableStringBuilder("1.5"), 1, 1), "ordinary edit preserves original spans");
var composed = new SpannableStringBuilder("x.5y") { CompositionStart = 1, CompositionEnd = 3 };
var result = filter.FilterFormatted(composed, 1, 3, new SpannableStringBuilder(""), 0, 0) as SpannableStringBuilder;
Eq("0.5", result?.ToString(), "source subrange");
Eq<int?>(1, result?.CompositionStart, "composition start shifted, not stripped");
Eq<int?>(3, result?.CompositionEnd, "composition end shifted, not stripped");
Eq<Java.Lang.ICharSequence?>(null, filter.FilterFormatted(new SpannableStringBuilder(""), 0, 0, new SpannableStringBuilder("0.5"), 1, 2), "separator deletion not intercepted");
var composingMarker = new NoCopySpan();
var selectionMarker = new NoCopySpan();
var composingStyle = new Java.Lang.Object();
var realComposition = new SpannableStringBuilder("x.5y");
realComposition.SetSpan(composingMarker, 1, 3, SpanTypes.Composing | SpanTypes.ExclusiveExclusive);
realComposition.SetSpan(selectionMarker, 2, 2, SpanTypes.PointPoint);
realComposition.SetSpan(composingStyle, 0, 4, SpanTypes.Composing | SpanTypes.ExclusiveExclusive);
var withSpans = (SpannableStringBuilder)filter.FilterFormatted(realComposition, 1, 3, new SpannableStringBuilder(""), 0, 0)!;
Eq(1, withSpans.GetSpanStart(composingMarker), "NoCopySpan composing marker preserved");
Eq(3, withSpans.GetSpanEnd(composingMarker), "composing marker end shifted");
Eq(SpanTypes.Composing | SpanTypes.ExclusiveExclusive, withSpans.GetSpanFlags(composingMarker), "composing marker flags preserved");
Eq(-1, withSpans.GetSpanStart(selectionMarker), "selection NoCopySpan not copied");
Eq(1, withSpans.GetSpanStart(composingStyle), "overlapping composition clipped and shifted");
Eq(3, withSpans.GetSpanEnd(composingStyle), "overlapping composition end clipped");
Console.WriteLine($"{passed} passed / {failed} failed");
return failed == 0 ? 0 : 1;
