global using HttpClient = PrivacyTests.RecordingHttpClient;
using System.Net;
using Predict.Services;

Preferences.Default.Reset();
Preferences.Default.Set("analytics_consent_v1", true);
Preferences.Default.Set("mp_device_id", "legacy-id");
MixpanelService.Track("app_opened");
if (MixpanelService.IsAnalyticsEnabled || PrivacyTests.RecordingHandler.Requests != 0
    || Preferences.Default.ContainsKey("mp_device_id"))
    throw new Exception("FAIL: legacy enabled preference without explicit decision must be blocked and ID removed.");
Console.WriteLine("PASS: legacy unknown consent fails closed before startup tracking.");
Preferences.Default.Reset();
MixpanelService.Track("app_opened");
if (PrivacyTests.RecordingHandler.Requests != 0)
    throw new Exception("FAIL: new installation must not send analytics without explicit opt-in.");
if (Preferences.Default.ContainsKey("mp_device_id"))
    throw new Exception("FAIL: opted-out installation must not create an analytics identifier.");
Console.WriteLine("PASS: analytics off by default; no identifier created.");

var setConsent = typeof(MixpanelService).GetMethod("SetAnalyticsEnabled");
if (setConsent is null)
    throw new Exception("FAIL: settings must be able to explicitly enable/withdraw analytics consent.");
setConsent.Invoke(null, [true]);
MixpanelService.Track("app_opened");
if (PrivacyTests.RecordingHandler.Requests != 1 || !Preferences.Default.ContainsKey("mp_device_id"))
    throw new Exception("FAIL: explicit opt-in must persist consent and allow a usage event.");
var previousId = Preferences.Default.Get("mp_device_id", "");
setConsent.Invoke(null, [false]);
MixpanelService.Track("app_opened");
if (PrivacyTests.RecordingHandler.Requests != 1 || Preferences.Default.ContainsKey("mp_device_id"))
    throw new Exception("FAIL: withdrawal must block further requests and remove the analytics identifier.");
setConsent.Invoke(null, [true]);
MixpanelService.Track("app_opened");
if (Preferences.Default.Get("mp_device_id", "") == previousId)
    throw new Exception("FAIL: renewed consent must not reuse the withdrawn analytics identifier.");
Console.WriteLine("PASS: opt-in, withdrawal and identifier rotation.");

MixpanelService.Track("estimate_calculated", new()
{
    ["weight_kg"] = 120.0, ["reps"] = 6, ["rpe"] = 8.0,
    ["best_est_kg"] = 145.0, ["email"] = "must-not-leak@example.invalid",
    ["unit"] = "kg", ["is_rounded"] = true
});
var body = PrivacyTests.RecordingHandler.LastBody;
var form = body.Split('&').Select(x => x.Split('=', 2))
    .ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1].Replace('+', ' ')));
var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(form["data"]));
using var doc = System.Text.Json.JsonDocument.Parse(json);
var sent = doc.RootElement[0].GetProperty("properties");
foreach (var key in new[] { "weight_kg", "reps", "rpe", "best_est_kg", "email" })
    if (sent.TryGetProperty(key, out _))
        throw new Exception($"FAIL: analytics must never transmit sensitive/unapproved property {key}.");
if (sent.GetProperty("unit").GetString() != "kg" || !sent.GetProperty("is_rounded").GetBoolean())
    throw new Exception("FAIL: allowlisted usage preferences should survive filtering.");
Console.WriteLine("PASS: no workout or arbitrary personal data in analytics payload.");

var beforeUnknown = PrivacyTests.RecordingHandler.Requests;
MixpanelService.Track("user-or-workout-data@example.invalid");
if (PrivacyTests.RecordingHandler.Requests != beforeUnknown)
    throw new Exception("FAIL: arbitrary event names could carry personal or workout data.");
Console.WriteLine("PASS: unknown event names cannot bypass the privacy filter.");

MixpanelService.Track("theme_changed", new()
{
    ["unit"] = "user-entered-workout", ["theme"] = "private note",
    ["is_rounded"] = "120 kg", ["distinct_id"] = "injected-identifier",
    ["token"] = "injected-token", ["app_version"] = "private value"
});
var encodedData = PrivacyTests.RecordingHandler.LastBody.Split('&')
    .Select(x => x.Split('=', 2)).Single(x => x[0] == "data")[1];
using var badPropsDoc = System.Text.Json.JsonDocument.Parse(
    Convert.FromBase64String(Uri.UnescapeDataString(encodedData.Replace('+', ' '))));
var clean = badPropsDoc.RootElement[0].GetProperty("properties");
if (clean.EnumerateObject().Count() != 5
    || clean.GetProperty("distinct_id").GetString() == "injected-identifier"
    || clean.GetProperty("token").GetString() == "injected-token"
    || clean.GetProperty("app_version").GetString() != "1.0-test")
    throw new Exception("FAIL: caller input bypassed enum/type filtering or overrode reserved metadata.");
Console.WriteLine("PASS: free text, wrong types and reserved metadata overrides are rejected.");

foreach (var ev in new[] { "app_opened", "estimate_calculated", "history_opened", "settings_opened",
                          "result_shared", "unit_changed", "rounding_changed", "theme_changed" })
{
    var before = PrivacyTests.RecordingHandler.Requests;
    MixpanelService.Track(ev);
    if (PrivacyTests.RecordingHandler.Requests != before + 1)
        throw new Exception($"FAIL: legitimate usage event {ev} was lost.");
}
Console.WriteLine("PASS: all eight existing usage event names remain supported.");

// Pause payload preparation after admission/ID capture, then withdraw on another thread.
MixpanelService.SetAnalyticsEnabled(true);
using (var preparing = new ManualResetEventSlim())
using (var resume = new ManualResetEventSlim())
{
    AppInfo.Current.BeforeVersionRead = () =>
    {
        preparing.Set();
        if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Payload preparation timed out.");
    };
    var before = PrivacyTests.RecordingHandler.Requests;
    var tracking = Task.Run(() => MixpanelService.Track("app_opened"));
    if (!preparing.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Track never reached preparation.");
    try { MixpanelService.SetAnalyticsEnabled(false); }
    finally { resume.Set(); }
    await tracking.WaitAsync(TimeSpan.FromSeconds(10));
    AppInfo.Current.BeforeVersionRead = null;
    if (PrivacyTests.RecordingHandler.Requests != before || Preferences.Default.ContainsKey("mp_device_id"))
        throw new Exception("FAIL: withdrawal during preparation must prevent request admission and identifier recreation.");
}
Console.WriteLine("PASS: deterministic concurrent withdrawal blocks prepared requests and removes identifier.");

MixpanelService.SetAnalyticsEnabled(true);
var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
CancellationToken pendingToken = default;
PrivacyTests.RecordingHandler.OnSend = ct => { pendingToken = ct; return pendingResponse.Task.WaitAsync(ct); };
var pendingTrack = TrackForTest();
MixpanelService.SetAnalyticsEnabled(false);
var wasCancelled = pendingToken.IsCancellationRequested;
if (!wasCancelled) pendingResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
await pendingTrack.WaitAsync(TimeSpan.FromSeconds(10));
PrivacyTests.RecordingHandler.OnSend = null;
if (!wasCancelled) throw new Exception("FAIL: withdrawal must cancel pending HTTP work.");
Console.WriteLine("PASS: withdrawal cancels pending HTTP work.");

// Hold an old generation's cancellation callback while opting back in.
MixpanelService.SetAnalyticsEnabled(true);
using (var cancelling = new ManualResetEventSlim())
using (var finishCancellation = new ManualResetEventSlim())
{
    var oldResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
    CancellationToken oldToken = default;
    PrivacyTests.RecordingHandler.OnSend = ct => { oldToken = ct; return oldResponse.Task; };
    var oldTrack = TrackForTest();
    var oldId = Preferences.Default.Get("mp_device_id", "");
    using var registration = oldToken.Register(() =>
    {
        cancelling.Set();
        if (!finishCancellation.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Cancellation callback timed out.");
    });
    var withdrawal = Task.Run(() => MixpanelService.SetAnalyticsEnabled(false));
    if (!cancelling.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Cancellation callback not reached.");
    Task? newTrack = null;
    var newResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
    CancellationToken newToken = default;
    try
    {
        var renewed = Task.Run(() =>
        {
            MixpanelService.SetAnalyticsEnabled(true);
            PrivacyTests.RecordingHandler.OnSend = ct => { newToken = ct; return newResponse.Task; };
            newTrack = TrackForTest();
        });
        await renewed.WaitAsync(TimeSpan.FromSeconds(5));
        if (newToken.IsCancellationRequested || Preferences.Default.Get("mp_device_id", "") == oldId)
            throw new Exception("FAIL: renewed generation must have an uncancelled request and rotated ID.");
    }
    finally { finishCancellation.Set(); }
    await withdrawal.WaitAsync(TimeSpan.FromSeconds(10));
    if (newToken.IsCancellationRequested || !MixpanelService.IsAnalyticsEnabled)
        throw new Exception("FAIL: old cancellation must not affect renewed consent/request.");
    oldResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
    newResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
    await Task.WhenAll(oldTrack, newTrack!).WaitAsync(TimeSpan.FromSeconds(10));
    PrivacyTests.RecordingHandler.OnSend = null;
}
Console.WriteLine("PASS: old cancellation runs outside consent lock and cannot cancel renewed generation.");

MixpanelService.SetAnalyticsEnabled(true);
foreach (var theme in new[] { "dark", "light" })
{
    MixpanelService.Track("theme_changed", new() { ["unit"] = "lbs", ["is_rounded"] = false, ["theme"] = theme });
    var data = PrivacyTests.RecordingHandler.LastBody.Split('=', 2)[1];
    using var positiveDoc = System.Text.Json.JsonDocument.Parse(
        Convert.FromBase64String(Uri.UnescapeDataString(data.Replace('+', ' '))));
    var positive = positiveDoc.RootElement[0].GetProperty("properties");
    if (positive.GetProperty("unit").GetString() != "lbs" || positive.GetProperty("is_rounded").GetBoolean()
        || positive.GetProperty("theme").GetString() != theme)
        throw new Exception("FAIL: allowed lbs, false, dark/light properties must survive filtering.");
}
Console.WriteLine("PASS: allowed lbs, false, dark and light property values survive filtering.");

// Exercise the production prompt entry point without any UI or real network.
MixpanelService.SetAnalyticsEnabled(false);
Preferences.Default.Reset();
var ensure = typeof(MixpanelService).GetMethod("EnsureAnalyticsConsentAsync");
if (ensure is null) throw new Exception("FAIL: first-launch consent prompt entry point is missing.");
Task Prompt(Func<Task<bool>> callback) => (Task)ensure.Invoke(null, [callback])!;
var promptCalls = 0;
await Prompt(() => { promptCalls++; return Task.FromResult(true); });
if (!MixpanelService.IsAnalyticsEnabled || promptCalls != 1)
    throw new Exception("FAIL: first-launch Allow must persist explicit consent.");
await Prompt(() => { promptCalls++; return Task.FromResult(false); });
if (!MixpanelService.IsAnalyticsEnabled || promptCalls != 1)
    throw new Exception("FAIL: remembered Allow must skip subsequent prompts.");
Console.WriteLine("PASS: first-launch Allow and persisted decision suppress repeated prompt.");

MixpanelService.SetAnalyticsEnabled(false);
Preferences.Default.Reset();
promptCalls = 0;
await Prompt(() => { promptCalls++; return Task.FromResult(false); });
await Prompt(() => { promptCalls++; return Task.FromResult(true); });
if (MixpanelService.IsAnalyticsEnabled || promptCalls != 1
    || Preferences.Default.Get("analytics_decision_version", 0) != 1)
    throw new Exception("FAIL: No thanks must persist independently of enabled and never ask repeatedly.");
MixpanelService.SetAnalyticsEnabled(true);
await Prompt(() => { promptCalls++; return Task.FromResult(false); });
if (!MixpanelService.IsAnalyticsEnabled || promptCalls != 1)
    throw new Exception("FAIL: Settings must allow later opt-in without prompting.");
MixpanelService.SetAnalyticsEnabled(false);
await Prompt(() => { promptCalls++; return Task.FromResult(true); });
if (MixpanelService.IsAnalyticsEnabled || promptCalls != 1)
    throw new Exception("FAIL: Settings withdrawal must persist without prompting.");
Console.WriteLine("PASS: No thanks remembered; Settings can later enable and withdraw without another prompt.");

Preferences.Default.Reset();
Preferences.Default.Set("analytics_consent_v1", true);
Preferences.Default.Set("mp_device_id", "legacy-id");
var beforePrompt = PrivacyTests.RecordingHandler.Requests;
var unsafeDuringPrompt = false;
await Prompt(() =>
{
    MixpanelService.Track("app_opened");
    unsafeDuringPrompt = MixpanelService.IsAnalyticsEnabled
        || PrivacyTests.RecordingHandler.Requests != beforePrompt
        || Preferences.Default.ContainsKey("mp_device_id");
    return Task.FromResult(false);
});
if (unsafeDuringPrompt || Preferences.Default.Get("analytics_decision_version", 0) != 1)
    throw new Exception("FAIL: prompt must not transmit or keep legacy ID before Allow.");
Console.WriteLine("PASS: legacy installation stays off throughout first post-upgrade prompt.");

Preferences.Default.Reset();
await Prompt(() => throw new InvalidOperationException("UI unavailable"));
if (MixpanelService.IsAnalyticsEnabled || Preferences.Default.Get("analytics_decision_version", 0) != 0)
    throw new Exception("FAIL: failed prompt must stay off and permit retry.");
await Prompt(() => Task.FromResult(true));
if (!MixpanelService.IsAnalyticsEnabled) throw new Exception("FAIL: prompt failure must permit later retry.");
Console.WriteLine("PASS: prompt exception leaves app usable, off and retryable.");

MixpanelService.SetAnalyticsEnabled(false);
Preferences.Default.Reset();
var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
promptCalls = 0;
var firstPrompt = Prompt(() => { promptCalls++; return answer.Task; });
await Prompt(() => { promptCalls++; return Task.FromResult(true); });
if (promptCalls != 1 || MixpanelService.IsAnalyticsEnabled)
    throw new Exception("FAIL: concurrent startup must open only one prompt and stay off while pending.");
answer.SetResult(false);
await firstPrompt;
if (MixpanelService.IsAnalyticsEnabled) throw new Exception("FAIL: dismissed/negative prompt must stay off.");
Console.WriteLine("PASS: concurrent startup admits one prompt; negative/dismissed response never enables.");

foreach (var settingsChoice in new[] { false, true })
{
    MixpanelService.SetAnalyticsEnabled(false);
    Preferences.Default.Reset();
    var lateAnswer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var pendingPrompt = Prompt(() => lateAnswer.Task);
    MixpanelService.SetAnalyticsEnabled(settingsChoice);
    lateAnswer.SetResult(!settingsChoice);
    await pendingPrompt;
    if (MixpanelService.IsAnalyticsEnabled != settingsChoice)
        throw new Exception("FAIL: stale prompt must not overwrite later Settings choice in either direction.");
}
Console.WriteLine("PASS: later Settings enable/disable beats stale prompt response.");

MixpanelService.SetAnalyticsEnabled(false);
Preferences.Default.Reset();
Preferences.Default.Set("analytics_consent_v1", true);
Preferences.Default.Set("mp_device_id", "legacy-before-prompt");
await Prompt(() =>
{
    if (Preferences.Default.Get("analytics_consent_v1", false)
        || Preferences.Default.ContainsKey("mp_device_id"))
        throw new Exception("FAIL: migration must clear untrusted legacy flag/ID before opening UI.");
    return Task.FromResult(true);
});
if (!MixpanelService.IsAnalyticsEnabled || Preferences.Default.ContainsKey("mp_device_id"))
    throw new Exception("FAIL: migration must not reuse old identifier after Allow.");
Console.WriteLine("PASS: direct prompt migration clears untrusted flag and ID before dialog.");

static Task TrackForTest() => (Task)typeof(MixpanelService)
    .GetMethod("TrackAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
    .Invoke(null, ["app_opened", null])!;

public static class Preferences
{
    public static FakePreferences Default { get; } = new();
}
public sealed class FakePreferences
{
    private readonly Dictionary<string, object> _values = [];
    public T Get<T>(string key, T fallback) => _values.TryGetValue(key, out var v) ? (T)v : fallback;
    public void Set<T>(string key, T value) => _values[key] = value!;
    public void Remove(string key) => _values.Remove(key);
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    public void Reset() => _values.Clear();
}
public static class AppInfo
{
    public static FakeAppInfo Current { get; } = new();
    public sealed class FakeAppInfo
    {
        public Action? BeforeVersionRead;
        public string VersionString { get { BeforeVersionRead?.Invoke(); return "1.0-test"; } }
    }
}
namespace PrivacyTests
{
    public sealed class RecordingHttpClient : System.Net.Http.HttpClient
    {
        public RecordingHttpClient() : base(new RecordingHandler()) { }
    }
    public sealed class RecordingHandler : HttpMessageHandler
    {
        public static int Requests;
        public static string LastBody = "";
        public static Func<CancellationToken, Task<HttpResponseMessage>>? OnSend;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastBody = request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            Interlocked.Increment(ref Requests);
            return OnSend?.Invoke(ct) ?? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
