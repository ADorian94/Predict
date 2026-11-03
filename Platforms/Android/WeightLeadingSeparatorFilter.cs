using Android.Text;
using Java.Lang;

namespace Predict.Platforms.Android;

// Normalize inside Android's edit transaction, before MAUI's TextChanged binding.
internal sealed class WeightLeadingSeparatorFilter : Java.Lang.Object, IInputFilter
{
    public ICharSequence? FilterFormatted(ICharSequence? source, int start, int end,
        ISpanned? dest, int dstart, int dend)
    {
        if (source is null || dest is null || dstart != 0) return null;
        var inserted = source.ToString()![start..end];
        var candidate = inserted + dest.ToString()![dend..];
        if (candidate.Length == 0 || (candidate[0] != '.' && candidate[0] != ',')) return null;
        // Leave invalid edits to the existing VM rejection/error path unchanged.
        if (!System.Text.RegularExpressions.Regex.IsMatch(candidate, @"\A[.,][0-9]{0,2}\z")) return null;
        // The builder copies ordinary spans, but deliberately excludes NoCopySpan.
        var normalized = new SpannableStringBuilder(source, start, end);
        normalized.Insert(0, "0");
        // BaseInputConnection's composing marker is a NoCopySpan: restore only
        // composing spans, never selection/watchers or unrelated NoCopySpans.
        if (source is ISpanned spans)
        {
            foreach (var span in spans.GetSpans(start, end, Java.Lang.Class.FromType(typeof(Java.Lang.Object))))
            {
                var flags = spans.GetSpanFlags(span);
                if ((flags & SpanTypes.Composing) == 0) continue;
                var a = Math.Clamp(spans.GetSpanStart(span) - start, 0, end - start) + 1;
                var b = Math.Clamp(spans.GetSpanEnd(span) - start, 0, end - start) + 1;
                normalized.SetSpan(span, a, b, flags);
            }
        }
        return normalized;
    }
}
