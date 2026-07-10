using System.Linq;

namespace Predict.Models;

public record OneRmResult(
    double RpeTableResult,
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

    public double RpeTableDisplay  => ToDisplay(RpeTableResult);
    public double EpleyDisplay     => ToDisplay(EpleyResult);
    public double BrzycziDisplay   => ToDisplay(BrzycziResult);
    public double LanderDisplay    => ToDisplay(LanderResult);
    public double LombardiDisplay  => ToDisplay(LombardiResult);
    public double MayhewDisplay    => ToDisplay(MayhewResult);
    public double OConnerDisplay   => ToDisplay(OConnerResult);
    public double WathanDisplay    => ToDisplay(WathanResult);

    public string Unit => IsLbs ? "lbs" : "kg";

    private double RawAverage =>
        new[] { RpeTableDisplay, EpleyDisplay, BrzycziDisplay, LanderDisplay,
                LombardiDisplay, MayhewDisplay, OConnerDisplay, WathanDisplay }.Average();

    public double BestEstimate => IsRounded
        ? Math.Round(RawAverage / 2.5) * 2.5
        : Math.Round(RawAverage, 1);

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
