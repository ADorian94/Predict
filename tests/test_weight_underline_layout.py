"""Source layout contracts, NOT rendered MAUI/Android or phone acceptance."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
M = "{http://schemas.microsoft.com/dotnet/2021/maui}"
X = "{http://schemas.microsoft.com/winfx/2009/xaml}"


class WeightUnderlineLayoutTests(unittest.TestCase):
    def setUp(self):
        self.page = ET.parse(ROOT / "MainPage.xaml").getroot()
        self.parents = {child: parent for parent in self.page.iter() for child in parent}
        self.card = self.named("WeightCard")
        self.entry = self.named("WeightInput")

    def named(self, name):
        nodes = [n for n in self.page.iter() if n.get(X + "Name") == name]
        self.assertEqual(1, len(nodes), f"Expected exactly one {name}")
        return nodes[0]

    def test_line_is_below_number_inside_existing_56_unit_slot_not_an_extra_stack_row(self):
        line = self.named("WeightUnderline")
        self.assertEqual(M + "BoxView", line.tag)
        slot = self.parents[self.entry]
        self.assertEqual(M + "Grid", slot.tag)
        self.assertIs(slot, self.parents[line])
        self.assertEqual([self.entry, line], list(slot))
        self.assertEqual("56", self.entry.get("HeightRequest"))
        self.assertEqual("Center", self.entry.get("HorizontalTextAlignment"))
        self.assertEqual("End", line.get("VerticalOptions"))  # MAUI LayoutOptions, not TextAlignment.Bottom.
        self.assertEqual("2", line.get("HeightRequest"))
        for key in ("RowDefinitions", "ColumnDefinitions", "Padding", "Margin", "HeightRequest"):
            self.assertNotIn(key, slot.attrib)
        stack = self.parents[slot]
        self.assertEqual(M + "VerticalStackLayout", stack.tag)
        self.assertEqual("10", stack.get("Spacing"))
        self.assertEqual("18,16", self.card.get("Padding"))
        self.assertEqual("{Binding WeightLabel}", list(stack)[0].get("Text"))
        error = list(stack)[2]
        self.assertEqual("{Binding WeightError}", error.get("Text"))
        self.assertEqual("13", error.get("FontSize"))
        self.assertEqual("{DynamicResource ErrorColor}", error.get("TextColor"))
        self.assertEqual("{Binding WeightError, Converter={StaticResource StringNotEmptyConverter}}", error.get("IsVisible"))
        self.assertEqual("Fill", slot.get("HorizontalOptions", "Fill"))
        for child in (self.entry, line):
            self.assertEqual("0", child.get("Grid.Row", "0"))
            self.assertEqual("0", child.get("Grid.Column", "0"))
            self.assertEqual("1", child.get("Grid.RowSpan", "1"))
            self.assertEqual("1", child.get("Grid.ColumnSpan", "1"))
        self.assertGreaterEqual(int(line.get("ZIndex", "0")), int(self.entry.get("ZIndex", "0")))

    def test_line_fills_inner_width_without_intercepting_entry_taps(self):
        line = self.named("WeightUnderline")
        self.assertEqual("Fill", line.get("HorizontalOptions"))
        self.assertEqual("4,0", line.get("Margin"))
        self.assertEqual("True", line.get("InputTransparent"))
        self.assertNotIn("WidthRequest", line.attrib)
        self.assertNotIn("MaximumWidthRequest", line.attrib)
        self.assertEqual([], list(line))

    def test_line_source_is_focus_independent_and_reuses_both_theme_purples(self):
        line = self.named("WeightUnderline")
        theme = (ROOT / "Services/ThemeService.cs").read_text(encoding="utf-8-sig")
        for name, color in (("Dark", "#C084FC"), ("Light", "#7C3AED")):
            method = theme.split(f"private static void Apply{name}(", 1)[1].split("private static void", 1)[0]
            self.assertRegex(method, r'r\["AccentLight"\]\s*= Color.FromArgb\("' + color + r'"\)')
        self.assertEqual("{DynamicResource AccentLight}", line.get("Color"))
        node = line
        while node is not self.card:
            for key in ("IsVisible", "Opacity", "Style", "IsEnabled"):
                self.assertNotIn(key, node.attrib)
            self.assertFalse(any(child.tag.endswith((".Triggers", ".VisualStateGroups")) for child in node))
            self.assertNotIn("IsFocused", ET.tostring(node, encoding="unicode"))
            node = self.parents[node]
        code = (ROOT / "MainPage.xaml.cs").read_text(encoding="utf-8-sig")
        self.assertNotIn("WeightUnderline", code)  # No focus-driven color/visibility mutation.

    def test_only_maui_line_draws_and_input_keyboard_wiring_is_preserved(self):
        self.named("WeightUnderline")
        native = (ROOT / "Platforms/Android/WeightEntryHandler.cs").read_text(encoding="utf-8-sig")
        page = (ROOT / "MainPage.xaml.cs").read_text(encoding="utf-8-sig")
        self.assertIn("native.Background = null;", native)
        self.assertIn("WeightEntryHandler.RemoveUnderline(entry)", page)
        self.assertNotIn("BackgroundTintList", native + page)
        self.assertEqual("Numeric", self.entry.get("Keyboard"))
        self.assertEqual("Transparent", self.entry.get("BackgroundColor"))
        self.assertEqual("{Binding WeightText}", self.entry.get("Text"))
        self.assertEqual("OnWeightInputCompleted", self.entry.get("Completed"))
        self.assertEqual("OnWeightInputHandlerChanged", self.entry.get("HandlerChanged"))
        self.assertEqual("OnWeightInputHandlerChanging", self.entry.get("HandlerChanging"))
        self.assertEqual("True", self.page.get("HideSoftInputOnTapped"))


if __name__ == "__main__":
    unittest.main()
