using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Predict.Models;
using Predict.Services;
using System.Collections.ObjectModel;

namespace Predict.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ResultContext _resultContext;

    [ObservableProperty]
    private ObservableCollection<HistoryEntry> _entries = [];

    public HistoryViewModel(ResultContext resultContext)
    {
        _resultContext = resultContext;
        Entries = new ObservableCollection<HistoryEntry>(HistoryService.Load());
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
