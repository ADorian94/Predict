using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Predict.Models;
using Predict.Services;
using System.Collections.ObjectModel;

namespace Predict.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private const int AdEvery = 4;

    private readonly ResultContext _resultContext;

    [ObservableProperty]
    private ObservableCollection<object> _entries = [];

    public HistoryViewModel(ResultContext resultContext)
    {
        _resultContext = resultContext;
        LoadEntries();
    }

    private void LoadEntries()
    {
        var raw = HistoryService.Load();
        var mixed = new List<object>();
        int count = 0;
        foreach (var entry in raw)
        {
            mixed.Add(entry);
            count++;
            if (count % AdEvery == 0)
                mixed.Add(new AdPlaceholder());
        }
        Entries = new ObservableCollection<object>(mixed);
    }

    [RelayCommand]
    public async Task OpenEntry(HistoryEntry entry)
    {
        _resultContext.Current = OneRmCalculator.Calculate(
            entry.Weight, entry.Reps, entry.Rpe, entry.IsLbs);
        await Shell.Current.GoToAsync("ResultPage");
    }

    [RelayCommand]
    private static async Task GoBack() =>
        await Shell.Current.GoToAsync("..");
}
