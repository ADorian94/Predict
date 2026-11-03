using Rect = global::Android.Graphics.Rect;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Microsoft.Maui.Controls;

namespace Predict.Platforms.Android;

// A scoped observer, not a global MAUI handler mapping. No touch/insets listener
// is replaced, so MAUI retains ownership of scrolling, slider and accessibility.
internal sealed class WeightEntryHandler : IDisposable
{
    private readonly Entry _entry;
    private readonly EditText _native;
    private readonly ViewTreeObserver? _observer;
    private readonly WeightLeadingSeparatorFilter _separatorFilter = new();
    private bool _keyboardWasVisible;
    private bool _disposed;

    internal static void RemoveUnderline(Entry entry)
    {
        if (entry.Handler?.PlatformView is EditText native)
            native.Background = null;
    }

    internal WeightEntryHandler(Entry entry, EditText native)
    {
        _entry = entry;
        _native = native;
        // Scoped to this weight Entry; keep MAUI/keyboard filters in their order.
        native.SetFilters((native.GetFilters() ?? Array.Empty<global::Android.Text.IInputFilter>()).Append(_separatorFilter).ToArray());
        _native.FocusChange += OnFocusChange;
        _observer = native.ViewTreeObserver;
        if (_observer?.IsAlive == true)
            _observer.GlobalLayout += OnGlobalLayout;
        // Do not treat a hidden IME at attachment as a dismissal. The user must
        // first have opened the keyboard while this particular Entry has focus.
    }

    private void OnFocusChange(object? sender, global::Android.Views.View.FocusChangeEventArgs e)
    {
        // Never carry a previous editing session's visible IME into a new tap.
        _keyboardWasVisible = false;
    }

    private void OnGlobalLayout(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (!_native.HasFocus)
        {
            _keyboardWasVisible = false;
            return;
        }

        var visible = IsKeyboardVisible();
        var wasVisible = _keyboardWasVisible;
        _keyboardWasVisible = visible;
        if (wasVisible && !visible)
        {
            _entry.Unfocus();
            _native.ClearFocus();
        }
    }

    private bool IsKeyboardVisible()
    {
        // AndroidX implements IME visibility on API 23+, including our min 26.
        // No API30-only platform call or replacement insets callback is needed.
        var insets = ViewCompat.GetRootWindowInsets(_native);
        if (insets is not null)
            return insets.IsVisible(WindowInsetsCompat.Type.Ime());

        // Some windows temporarily lack root insets. Use the visible frame only
        // then; a substantial occlusion avoids mistaking system bars for an IME.
        using var frame = new Rect();
        var root = _native.RootView;
        if (root is null || root.Height <= 0) return false;
        root.GetWindowVisibleDisplayFrame(frame);
        var density = _native.Resources?.DisplayMetrics?.Density ?? 1f;
        return root.Height - frame.Bottom > Math.Max(100 * density, root.Height * 0.15);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _keyboardWasVisible = false;
        _native.FocusChange -= OnFocusChange;
        _native.SetFilters((_native.GetFilters() ?? Array.Empty<global::Android.Text.IInputFilter>()).Where(filter => !filter.Equals(_separatorFilter)).ToArray());
        if (_observer is not null && _observer.IsAlive)
            _observer.GlobalLayout -= OnGlobalLayout;
    }
}
