"""Source-wiring checks complement runtime HTTP-payload privacy tests."""
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parent.parent

class PrivacyWiringTests(unittest.TestCase):
    def test_calculation_caller_does_not_pass_workout_data_to_analytics(self):
        source = (ROOT / "ViewModels/MainViewModel.cs").read_text(encoding="utf-8-sig")
        match = re.search(r'MixpanelService\.Track\("estimate_calculated", new\(\)\s*\{(.*?)\}\);', source, re.S)
        self.assertIsNotNone(match, "calculation usage event must still be wired")
        assert match is not None  # Narrow the regex result for static type checking.
        for key in ("weight_kg", "reps", "rpe", "best_est_kg"):
            self.assertNotIn(f'["{key}"]', match.group(1), f"caller still passes {key} to analytics")

if __name__ == "__main__":
    unittest.main()
