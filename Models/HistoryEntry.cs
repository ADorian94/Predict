using System.Text.Json.Serialization;
using Predict.Services;

namespace Predict.Models;

public record HistoryEntry(
    DateTime Date, double Weight, int Reps, double Rpe,
    double BestEstimate, string Unit, bool IsLbs = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsRounded = null)
{
    public string DateDisplay  => Date.ToString("yyyy.MM.dd HH:mm");
    // Weight is persisted in canonical kg; IsLbs preserves the entry's original display unit.
    public string InputDisplay => $"{(IsLbs ? Weight / 0.45359237 : Weight):0.##} {Unit} x {Reps} rep @ {Rpe:0.0}";
    public string ResultDisplay => $"Best: {BestEstimate:0.0} {Unit}";

    // Reopens a stored entry with the rounding mode that produced its BestEstimate.
    // An explicit flag is authoritative. For legacy entries (no flag) the stored best is
    // matched against the raw and rounded candidates; when both match, rounded is used as a
    // stable tie fallback without claiming which mode the original entry used. When neither
    // candidate reproduces the stored best, that best is preserved instead of being replaced.
    public OneRmResult ToResult() => RestoreResult() with { EstimatedAt = Date };

    private OneRmResult RestoreResult()
    {
        if (IsRounded is bool explicitMode)
        {
            var explicitResult = OneRmCalculator.Calculate(Weight, Reps, Rpe, IsLbs, explicitMode);
            return Match(explicitResult.BestEstimate) ? explicitResult : WithStoredBest(explicitResult);
        }

        var raw = OneRmCalculator.Calculate(Weight, Reps, Rpe, IsLbs, false);
        var rounded = OneRmCalculator.Calculate(Weight, Reps, Rpe, IsLbs, true);
        var rawMatches = Match(raw.BestEstimate);
        var roundedMatches = Match(rounded.BestEstimate);

        if (rawMatches && !roundedMatches) return raw;
        if (roundedMatches && !rawMatches) return rounded;
        if (rawMatches && roundedMatches) return rounded; // indistinguishable: stable fallback
        return WithStoredBest(rounded);                    // neither reproduces it: keep the stored best

        bool Match(double candidate) => Math.Abs(candidate - BestEstimate) <= 1e-9;
        OneRmResult WithStoredBest(OneRmResult result) => result with { BestEstimateOverride = BestEstimate };
    }
}
