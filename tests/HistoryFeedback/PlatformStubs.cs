// Platform boundaries only; models, calculator, view model and MVVM commands are real linked code.
namespace Predict.Services
{
    public sealed class AdsConsentCoordinator
    {
        public void PreloadInterstitial() { }
        public Task TryShowInterstitialAsync() => Task.CompletedTask;
    }
    public static class HapticService { public static void Click() { } }
    public static class MixpanelService { public static void Track(string name) { } }
}
namespace Microsoft.Maui.ApplicationModel.DataTransfer
{
    public sealed class ShareTextRequest { public string? Title { get; set; } public string? Text { get; set; } }
    public sealed class Share
    {
        public static Share Default { get; } = new();
        public ShareTextRequest? LastRequest { get; private set; }
        public Task RequestAsync(ShareTextRequest request) { LastRequest = request; return Task.CompletedTask; }
    }
}
public sealed class Shell
{
    public static Shell Current { get; } = new();
    public Task GoToAsync(string route) => Task.CompletedTask;
}
public static class Preferences { public static FakePreferences Default { get; } = new(); }
public sealed class FakePreferences
{
    private readonly Dictionary<string, object> values = [];
    public T Get<T>(string key, T fallback) => values.TryGetValue(key, out var value) ? (T)value : fallback;
    public void Set<T>(string key, T value) => values[key] = value!;
}
