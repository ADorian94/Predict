using Predict.Models;

namespace Predict.Services;

public static class OneRmCalculator
{
    private static readonly double[] RpeValues = [6.0, 6.5, 7.0, 7.5, 8.0, 8.5, 9.0, 9.5, 10.0];

    private static readonly double[,] RtsTable =
    {
        { 0.780, 0.800, 0.830, 0.850, 0.880, 0.900, 0.930, 0.965, 1.000 },
        { 0.750, 0.775, 0.800, 0.825, 0.850, 0.875, 0.900, 0.935, 0.965 },
        { 0.720, 0.745, 0.775, 0.800, 0.825, 0.850, 0.875, 0.900, 0.935 },
        { 0.695, 0.720, 0.745, 0.775, 0.800, 0.825, 0.850, 0.875, 0.900 },
        { 0.670, 0.695, 0.720, 0.745, 0.775, 0.800, 0.825, 0.850, 0.875 },
        { 0.650, 0.670, 0.695, 0.720, 0.745, 0.775, 0.800, 0.825, 0.850 },
        { 0.625, 0.650, 0.670, 0.695, 0.720, 0.745, 0.775, 0.800, 0.825 },
        { 0.600, 0.625, 0.650, 0.670, 0.695, 0.720, 0.745, 0.775, 0.800 },
        { 0.575, 0.600, 0.625, 0.650, 0.670, 0.695, 0.720, 0.745, 0.775 },
        { 0.550, 0.575, 0.600, 0.625, 0.650, 0.670, 0.695, 0.720, 0.745 },
    };

    public static OneRmResult Calculate(double weightKg, int reps, double rpe, bool isLbs = false, bool isRounded = true)
    {
        return new OneRmResult(
            RpeTable(weightKg, reps, rpe),
            Epley(weightKg, reps),
            Brzyczi(weightKg, reps),
            Lander(weightKg, reps),
            Lombardi(weightKg, reps),
            Mayhew(weightKg, reps),
            OConner(weightKg, reps),
            Wathan(weightKg, reps),
            weightKg, reps, rpe, isLbs, isRounded);
    }

    private static double RpeTable(double weight, int reps, double rpe)
    {
        var repIdx = Math.Clamp(reps - 1, 0, RtsTable.GetLength(0) - 1);
        var rpeIdx = FindRpeIndex(rpe);
        return Math.Round(weight / RtsTable[repIdx, rpeIdx], 1);
    }

    private static double Epley(double weight, int reps)
    {
        if (reps == 1) return weight;
        return Math.Round(weight * (1.0 + reps / 30.0), 1);
    }

    private static double Brzyczi(double weight, int reps)
    {
        if (reps == 1) return weight;
        var denom = 37.0 - reps;
        if (denom <= 0) return weight;
        return Math.Round(weight * 36.0 / denom, 1);
    }

    private static double Lander(double weight, int reps)
    {
        if (reps == 1) return weight;
        var denom = 101.3 - 2.67123 * reps;
        if (denom <= 0) return weight;
        return Math.Round(100.0 * weight / denom, 1);
    }

    private static double Lombardi(double weight, int reps)
    {
        if (reps == 1) return weight;
        return Math.Round(weight * Math.Pow(reps, 0.10), 1);
    }

    private static double Mayhew(double weight, int reps)
    {
        if (reps == 1) return weight;
        return Math.Round(100.0 * weight / (52.2 + 41.9 * Math.Exp(-0.055 * reps)), 1);
    }

    private static double OConner(double weight, int reps)
    {
        if (reps == 1) return weight;
        return Math.Round(weight * (1.0 + 0.025 * reps), 1);
    }

    private static double Wathan(double weight, int reps)
    {
        if (reps == 1) return weight;
        return Math.Round(100.0 * weight / (48.8 + 53.8 * Math.Exp(-0.075 * reps)), 1);
    }

    private static int FindRpeIndex(double rpe)
    {
        var minDist = double.MaxValue;
        var bestIndex = 0;
        for (var i = 0; i < RpeValues.Length; i++)
        {
            var dist = Math.Abs(RpeValues[i] - rpe);
            if (dist < minDist) { minDist = dist; bestIndex = i; }
        }
        return bestIndex;
    }
}
