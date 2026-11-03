namespace Predict.Models;

public record HistoryEntry(
    DateTime Date, double Weight, int Reps, double Rpe,
    double BestEstimate, string Unit, bool IsLbs = false)
{
    public string DateDisplay  => Date.ToString("yyyy.MM.dd HH:mm");
    // Weight is persisted in canonical kg; IsLbs preserves the entry's original display unit.
    public string InputDisplay => $"{(IsLbs ? Weight / 0.45359237 : Weight):0.##} {Unit} x {Reps} rep @ {Rpe:0.0}";
    public string ResultDisplay => $"Best: {BestEstimate:0.0} {Unit}";
}
