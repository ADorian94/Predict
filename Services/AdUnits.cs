namespace Predict.Services;

public static class AdUnits
{
    // Explicit opt-in for Release test APKs; normal Release retains live units.
#if DEBUG || PREDICT_TEST_ADS
    public const string Banner = "ca-app-pub-3940256099942544/6300978111";
    public const string Interstitial = "ca-app-pub-3940256099942544/1033173712";
    public static bool IsTestMode => true;
#else
    public const string Banner = "ca-app-pub-3012910791046560/3302176099";
    public const string Interstitial = "ca-app-pub-3012910791046560/3511582164";
    public static bool IsTestMode => false;
#endif
}
