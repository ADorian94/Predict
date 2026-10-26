using Predict.Services;

var failed = 0;
void Check(string name, bool condition)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}: {name}");
    if (!condition) failed++;
}
var state = new BannerSlotState(60);
Check("Pending slot reserves height so its native view can attach", state.Height == 60 && !state.IsCollapsed);
state.OnFailed();
Check("Failed slot collapses", state.Height == 0 && state.IsCollapsed);
state.OnFailed();
state.OnLoaded();
Check("Success after repeated failures restores original height", state.Height == 60 && !state.IsCollapsed);
state.OnLoaded();
Check("Repeated success preserves original height", state.Height == 60 && !state.IsCollapsed);
var second = new BannerSlotState(50);
Check("Separate slots keep independent state", second.Height == 50 && !second.IsCollapsed);
var auto = new BannerSlotState(-1);
auto.OnFailed();
auto.OnLoaded();
Check("Auto-sized slots restore the auto sentinel", auto.Height == -1 && !auto.IsCollapsed);
#if PREDICT_TEST_ADS || DEBUG
Check("Test mode uses Google's fixed banner unit", AdUnits.Banner == "ca-app-pub-3940256099942544/6300978111");
Check("Test mode uses Google's interstitial unit", AdUnits.Interstitial == "ca-app-pub-3940256099942544/1033173712");
Check("Test mode indicator", AdUnits.IsTestMode);
#else
Check("Normal Release preserves production banner unit", AdUnits.Banner == "ca-app-pub-3012910791046560/3302176099");
Check("Normal Release preserves production interstitial unit", AdUnits.Interstitial == "ca-app-pub-3012910791046560/3511582164");
Check("Normal Release is not test mode", !AdUnits.IsTestMode);
#endif
return failed == 0 ? 0 : 1;
