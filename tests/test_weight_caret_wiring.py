"""Static lifecycle contracts; real Android compilation is a separate hosted gate."""
import pathlib
import unittest
ROOT = pathlib.Path(__file__).resolve().parents[1]

class WeightCaretWiring(unittest.TestCase):
    def test_filter_is_scoped_and_appended_not_replacing_existing_filters(self):
        code = (ROOT / "Platforms/Android/WeightEntryHandler.cs").read_text()
        self.assertIn("(native.GetFilters() ?? Array.Empty<global::Android.Text.IInputFilter>()).Append(_separatorFilter).ToArray()", code)
        self.assertIn("Where(filter => !filter.Equals(_separatorFilter))", code)
        self.assertIn("if (_disposed) return;", code)

    def test_no_global_cursor_or_composition_override(self):
        code = (ROOT / "Platforms/Android/WeightLeadingSeparatorFilter.cs").read_text()
        for forbidden in ("SetSelection", "CursorPosition", "Post(", "FinishComposing", "ClearSpans"):
            self.assertNotIn(forbidden, code)
        self.assertIn("new SpannableStringBuilder(source, start, end)", code)

    def test_existing_entry_lifecycle_owns_filter(self):
        code = (ROOT / "MainPage.xaml.cs").read_text()
        self.assertIn("new Platforms.Android.WeightEntryHandler(_weightInput, native)", code)
        self.assertIn("_weightInputObserver?.Dispose()", code)

    def test_native_filter_compiles_in_behavior_tests(self):
        code = (ROOT / "tests/WeightCaret/Predict.WeightCaretTests.csproj").read_text()
        self.assertIn('../../Platforms/Android/WeightLeadingSeparatorFilter.cs', code)
        self.assertIn('AndroidTextDoubles.cs', code)

if __name__ == '__main__':
    unittest.main()
