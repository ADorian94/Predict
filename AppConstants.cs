namespace Predict;

public static class AppConstants
{
    // Preference keys
    public const string PrefWeight    = "predict_weight";
    public const string PrefReps      = "predict_reps";
    public const string PrefRpe       = "predict_rpe";
    public const string PrefIsLbs     = "predict_islbs";
    public const string PrefIsRounded = "predict_is_rounded";
    public const string PrefHistory   = "predict_history_v1";

    // Validation
    public const double MaxWeightKg  = 1000.0;
    public const double MaxWeightLbs = 2204.0;
    public const int    MaxReps      = 20;
    public const int    MinReps      = 1;
}
