namespace Predict.Models;

public record HistoryEntry(
    DateTime Date, double Weight, int Reps, double Rpe,
    double BestEstimate, string Unit, bool IsLbs = false)
{
    public string DateDisplay  => Date.ToString("yyyy.MM.dd HH:mm");
    public string InputDisplay => $"{Weight:0.#} {Unit} x {Reps} rep @ {Rpe:0.0}";
    public string ResultDisplay => $"Best: {BestEstimate:0.0} {Unit}";
}
