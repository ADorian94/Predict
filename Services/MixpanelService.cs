using System.Text;
using System.Text.Json;

namespace Predict.Services;

public static class MixpanelService
{
    public const string AnalyticsDisclosure =
        "Optional: send usage events (app_opened, estimate_calculated, history_opened, settings_opened, "
        + "result_shared, unit_changed, rounding_changed, theme_changed), unit/rounding/theme preferences, "
        + "a random installation ID (pseudonymous), app version, Android platform and event time to Mixpanel EU. "
        + "No weight, reps, RPE or estimates are sent. You can use every feature without analytics. "
        + "Change this anytime in Settings. This is separate from advertising consent.";

    private const string Token  = "89db93a097fc84d4900ab50048a2f234";
    private const string ApiUrl = "https://api-eu.mixpanel.com/track";

    private const string ConsentKey = "analytics_consent_v1";
    private const string DecisionKey = "analytics_decision_version";
    private const int DecisionVersion = 1;
    private static readonly HttpClient _http = new();

    private static readonly object ConsentGate = new();
    private sealed class ConsentGeneration
    {
        public readonly CancellationTokenSource Cancellation = new();
        public int Pending;
        public bool CancellationCompleted;
    }
    private static ConsentGeneration? _generation;

    public static bool IsAnalyticsEnabled
    {
        get { lock (ConsentGate) return HasDecisionLocked && Preferences.Default.Get(ConsentKey, false); }
    }

    public static void SetAnalyticsEnabled(bool enabled)
    {
        ConsentGeneration? withdrawn = null;
        lock (ConsentGate)
        {
            Preferences.Default.Set(DecisionKey, DecisionVersion);
            Preferences.Default.Set(ConsentKey, enabled);
            if (!enabled)
            {
                withdrawn = _generation;
                _generation = null;
                Preferences.Default.Remove("mp_device_id");
            }
        }
        if (withdrawn is null) return;
        // Cancellation callbacks are external code: never run them under ConsentGate.
        // Only the detached generation is cancelled, even if consent is renewed now.
        try { withdrawn.Cancellation.Cancel(); }
        finally
        {
            lock (ConsentGate)
            {
                withdrawn.CancellationCompleted = true;
                if (withdrawn.Pending == 0) withdrawn.Cancellation.Dispose();
            }
        }
    }

    private static bool _promptOpen;

    public static async Task EnsureAnalyticsConsentAsync(Func<Task<bool>> prompt)
    {
        lock (ConsentGate)
        {
            if (HasDecisionLocked || _promptOpen) return;
            // A legacy enabled flag is not evidence of an informed decision.
            Preferences.Default.Set(ConsentKey, false);
            Preferences.Default.Remove("mp_device_id");
            _promptOpen = true;
        }
        try
        {
            var enabled = await prompt();
            lock (ConsentGate)
            {
                // Any explicit Settings choice made while the dialog was open wins.
                if (HasDecisionLocked) return;
                Preferences.Default.Set(ConsentKey, enabled);
                Preferences.Default.Set(DecisionKey, DecisionVersion);
                if (!enabled) Preferences.Default.Remove("mp_device_id");
            }
        }
        catch { /* No decision on UI failure; keep optional analytics off and retry later. */ }
        finally { lock (ConsentGate) _promptOpen = false; }
    }

    private static bool HasDecisionLocked => Preferences.Default.Get(DecisionKey, 0) == DecisionVersion;

    private static string DeviceId
    {
        get
        {
            var id = Preferences.Default.Get("mp_device_id", string.Empty);
            if (!string.IsNullOrEmpty(id)) return id;
            id = Guid.NewGuid().ToString();
            Preferences.Default.Set("mp_device_id", id);
            return id;
        }
    }

    public static void Track(string eventName, Dictionary<string, object>? props = null)
    {
        if (eventName is not ("app_opened" or "estimate_calculated" or "history_opened"
            or "settings_opened" or "result_shared" or "unit_changed"
            or "rounding_changed" or "theme_changed"))
            return;

        _ = TrackAsync(eventName, props);
    }

    private static async Task TrackAsync(string eventName, Dictionary<string, object>? props)
    {
        ConsentGeneration? generation = null;
        try
        {
            string deviceId;
            lock (ConsentGate)
            {
                if (!HasDecisionLocked || !Preferences.Default.Get(ConsentKey, false))
                {
                    Preferences.Default.Remove("mp_device_id");
                    return;
                }
                generation = _generation ??= new ConsentGeneration();
                ++generation.Pending;
                deviceId = DeviceId;
            }

            // Never copy arbitrary caller properties into an outbound request.
            var properties = new Dictionary<string, object>
            {
                ["token"]       = Token,
                ["distinct_id"] = deviceId,
                ["time"]        = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["$os"]         = "Android",
                ["app_version"] = AppInfo.Current.VersionString,
            };

            foreach (var (name, value) in props ?? [])
            {
                if (name == "unit" && value is string unit && unit is "kg" or "lbs")
                    properties[name] = unit;
                else if (name == "is_rounded" && value is bool rounded)
                    properties[name] = rounded;
                else if (name == "theme" && value is string theme && theme is "dark" or "light")
                    properties[name] = theme;
            }

            var payload = new[] { new Dictionary<string, object>
            {
                ["event"]      = eventName,
                ["properties"] = properties,
            }};

            var json    = JsonSerializer.Serialize(payload);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            using var content = new FormUrlEncodedContent([new("data", encoded)]);
            Task<HttpResponseMessage> pending;
            lock (ConsentGate)
            {
                if (generation != _generation || !Preferences.Default.Get(ConsentKey, false)) return;
                // Admission is atomic with withdrawal; transmitted bytes cannot be retracted.
                pending = _http.PostAsync(ApiUrl, content, generation.Cancellation.Token);
            }
            using var response = await pending;
        }
        catch { }
        finally
        {
            if (generation is not null)
            {
                lock (ConsentGate)
                {
                    if (--generation.Pending == 0 && generation.CancellationCompleted)
                        generation.Cancellation.Dispose();
                }
            }
        }
    }
}
