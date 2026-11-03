// Contract doubles: selection advancement models Android Editable.replace; not a device/IME test.
namespace Java.Lang
{
    public class Object { }
    public class Math { } // Mirror the Android namespace collision: production must use System.Math.
    public class Class { public static Class FromType(System.Type type) => new(); }
    public interface ICharSequence { int Length(); string ToString(); }
}
namespace Android.Text
{
    [System.Flags]
    public enum SpanTypes { ExclusiveExclusive = 0x21, PointPoint = 0x22, Composing = 0x100 }
    public class NoCopySpan : Java.Lang.Object { }
    public interface ISpanned : Java.Lang.ICharSequence
    {
        Java.Lang.Object[] GetSpans(int start, int end, Java.Lang.Class kind);
        int GetSpanStart(Java.Lang.Object span);
        int GetSpanEnd(Java.Lang.Object span);
        SpanTypes GetSpanFlags(Java.Lang.Object span);
    }
    public interface IInputFilter
    {
        Java.Lang.ICharSequence? FilterFormatted(Java.Lang.ICharSequence? source, int start, int end, ISpanned? dest, int dstart, int dend);
    }
    public class SpannableStringBuilder : Java.Lang.Object, ISpanned
    {
        private readonly Dictionary<Java.Lang.Object, (int start, int end, SpanTypes flags)> _spans = new();
        public void SetSpan(Java.Lang.Object span, int start, int end, SpanTypes flags) => _spans[span] = (start, end, flags);
        public Java.Lang.Object[] GetSpans(int start, int end, Java.Lang.Class kind) => _spans.Where(p => p.Value.start <= end && p.Value.end >= start).Select(p => p.Key).ToArray();
        public int GetSpanStart(Java.Lang.Object span) => _spans.TryGetValue(span, out var s) ? s.start : -1;
        public int GetSpanEnd(Java.Lang.Object span) => _spans.TryGetValue(span, out var s) ? s.end : -1;
        public SpanTypes GetSpanFlags(Java.Lang.Object span) => _spans.TryGetValue(span, out var s) ? s.flags : 0;
        private string _text;
        public int? CompositionStart, CompositionEnd;
        public SpannableStringBuilder(string text) { _text = text; }
        public SpannableStringBuilder(Java.Lang.ICharSequence source, int start, int end)
        {
            _text = source.ToString()[start..end];
            if (source is ISpanned spanned)
                foreach (var span in spanned.GetSpans(start, end, Java.Lang.Class.FromType(typeof(Java.Lang.Object))))
                    if (span is not NoCopySpan)
                        SetSpan(span, Math.Clamp(spanned.GetSpanStart(span) - start, 0, end - start), Math.Clamp(spanned.GetSpanEnd(span) - start, 0, end - start), spanned.GetSpanFlags(span));
            if (source is SpannableStringBuilder b && b.CompositionStart >= start && b.CompositionEnd <= end)
            { CompositionStart = b.CompositionStart - start; CompositionEnd = b.CompositionEnd - start; }
        }
        public SpannableStringBuilder Insert(int where, string text)
        {
            _text = _text.Insert(where, text);
            foreach (var key in _spans.Keys.ToArray())
            {
                var s = _spans[key];
                // Model exclusive composing endpoints and point spans used here.
                _spans[key] = (s.start >= where ? s.start + text.Length : s.start, s.end >= where ? s.end + text.Length : s.end, s.flags);
            }
            if (CompositionStart >= where) CompositionStart += text.Length;
            if (CompositionEnd >= where) CompositionEnd += text.Length;
            return this;
        }
        public int Length() => _text.Length;
        public override string ToString() => _text;
    }
}
