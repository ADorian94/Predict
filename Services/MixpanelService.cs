using System.Text;
using System.Text.Json;

namespace Predict.Services;

public static class MixpanelService
{
    private const string Token  = "89db93a097fc84d4900ab50048a2f234";
    private const string ApiUrl = "https://api-eu.mixpanel.com/track";

    private static readonly HttpClient _http = new();

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
        _ = TrackAsync(eventName, props);
    }

    private static async Task TrackAsync(string eventName, Dictionary<string, object>? props)
    {
        try
        {
            var properties = new Dictionary<string, object>(props ?? [])
            {
                ["token"]       = Token,
                ["distinct_id"] = DeviceId,
                ["time"]        = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["$os"]         = "Android",
                ["app_version"] = AppInfo.Current.VersionString,
            };

            var payload = new[] { new Dictionary<string, object>
            {
                ["event"]      = eventName,
                ["properties"] = properties,
            }};

            var json    = JsonSerializer.Serialize(payload);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            var content = new FormUrlEncodedContent([new("data", encoded)]);
            await _http.PostAsync(ApiUrl, content);
        }
        catch { }
    }
}
