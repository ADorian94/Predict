namespace Predict.Services;

public static class HapticService
{
    public static void Click()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
    }
}
