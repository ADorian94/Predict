using System.Text.Json;
using Predict.Models;

namespace Predict.Services;

public static class HistoryService
{
    private const int MaxEntries = 50;

    public static List<HistoryEntry> Load()
    {
        var json = Preferences.Default.Get(AppConstants.PrefHistory, string.Empty);
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? []; }
        catch { return []; }
    }

    public static Task SaveAsync(OneRmResult result) => Task.Run(() =>
    {
        var entries = Load();
        entries.Insert(0, new HistoryEntry(
            DateTime.Now, result.Weight, result.Reps, result.Rpe,
            result.BestEstimate, result.Unit, result.IsLbs));
        if (entries.Count > MaxEntries)
            entries = entries.Take(MaxEntries).ToList();
        Preferences.Default.Set(AppConstants.PrefHistory, JsonSerializer.Serialize(entries));
    });
}
