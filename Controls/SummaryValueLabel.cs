namespace Predict.Controls;

/// <summary>Keep the complete summary value on one line, adapting the native font to available width.</summary>
public class SummaryValueLabel : Label
{
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if ANDROID
        if (Handler?.PlatformView is global::Android.Widget.TextView native)
        {
            native.SetMaxLines(1);
            native.Ellipsize = null;
            native.SetHorizontallyScrolling(false);
            // Android API 26+ (the app minimum): layout-aware fitting uses the actual
            // typeface/glyphs and scaled pixels, including accessibility font scale.
            // Never shorten digits or append an ellipsis, even in very narrow layouts.
            native.SetAutoSizeTextTypeUniformWithConfiguration(1, 20, 1, (int)global::Android.Util.ComplexUnitType.Sp);
        }
#endif
    }
}
