using System.Linq;

namespace Predict.Models;

public record OneRmResult(
    double? RpeTableResult,
    double EpleyResult,
    double BrzycziResult,
    double LanderResult,
    double LombardiResult,
    double MayhewResult,
    double OConnerResult,
    double WathanResult,
    double Weight,
    int Reps,
    double Rpe,
    bool IsLbs = false,
    bool IsRounded = true
)
{
    private const double KgToLbs = 2.20462262;

    private double ToDisplay(double kg) => IsLbs ? Math.Round(kg * KgToLbs, 1) : kg;

    public double? RpeTableDisplay => RpeTableResult is double value ? ToDisplay(value) : null;
    public double EpleyDisplay     => ToDisplay(EpleyResult);
    public double BrzycziDisplay   => ToDisplay(BrzycziResult);
    public double LanderDisplay    => ToDisplay(LanderResult);
    public double LombardiDisplay  => ToDisplay(LombardiResult);
    public double MayhewDisplay    => ToDisplay(MayhewResult);
    public double OConnerDisplay   => ToDisplay(OConnerResult);
    public double WathanDisplay    => ToDisplay(WathanResult);

    public string Unit => IsLbs ? "lbs" : "kg";

    public bool HasRpeEstimate => RpeTableResult.HasValue;
    public bool IsRpeUnavailable => !HasRpeEstimate;
    public int FormulaCount => HasRpeEstimate ? 8 : 7;
    public string RpeTableText => RpeTableDisplay is double value ? $"{value:0.0} {Unit}" : "N/A";
    public string RoundingNote => IsRounded
        ? $"(avg of {FormulaCount} formulas, rounded to 2.5)"
        : $"(avg of {FormulaCount} formulas)";
    public string RpeAvailabilityNote => HasRpeEstimate ? string.Empty
        : "RTS RPE is unavailable above 10 reps. RPE is not used in this estimate.";

    private double RawAverage =>
        new double?[] { RpeTableDisplay, EpleyDisplay, BrzycziDisplay, LanderDisplay,
                LombardiDisplay, MayhewDisplay, OConnerDisplay, WathanDisplay }
            .Where(value => value.HasValue).Average(value => value!.Value);

    public double BestEstimate => IsRounded
        ? Math.Round(RawAverage / 2.5) * 2.5
        : Math.Round(RawAverage, 1);

    public IReadOnlyList<FormulaEntry> Formulas
    {
        get
        {
            var u = Unit;
            var rows = new List<FormulaEntry>
            {
                new("Epley",    $"{EpleyDisplay:0.0} {u}",     "w*(1 + r/30)"),
                new("Brzycki",  $"{BrzycziDisplay:0.0} {u}",   "w*36/(37-r)"),
                new("Lander",   $"{LanderDisplay:0.0} {u}",    "100w/(101.3-2.671r)"),
                new("Lombardi", $"{LombardiDisplay:0.0} {u}",  "w * r^0.10"),
                new("Mayhew",   $"{MayhewDisplay:0.0} {u}",    "100w/(52.2+41.9e^-0.055r)"),
                new("O'Conner", $"{OConnerDisplay:0.0} {u}",   "w*(1 + 0.025r)"),
                new("Wathan",   $"{WathanDisplay:0.0} {u}",    "100w/(48.8+53.8e^-0.075r)"),
            };
            if (HasRpeEstimate)
                rows.Insert(0, new FormulaEntry("RTS RPE", RpeTableText, "1RM = weight / intensity%"));
            return rows;
        }
    }

    public IReadOnlyList<PercentEntry> Percentages
    {
        get
        {
            var pcts = new[] { 60, 65, 70, 75, 80, 85, 90, 95 };
            return pcts.Select(p =>
            {
                var raw = BestEstimate * p / 100.0;
                var weight = IsRounded ? Math.Round(raw / 2.5) * 2.5 : Math.Round(raw, 1);
                return new PercentEntry(p, weight, Unit);
            }).ToList();
        }
    }
}

public record PercentEntry(int Percent, double Weight, string Unit)
{
    public string Label   => $"{Percent}%";
    public string Display => $"{Weight:0.0} {Unit}";
}

public record FormulaEntry(string Name, string ResultDisplay, string FormulaText);
