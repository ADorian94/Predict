"""Static wiring guards only: these are NOT Android runtime/device evidence."""
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parent.parent


def source(relative):
    path = ROOT / relative
    return path.read_text(encoding="utf-8-sig") if path.exists() else ""


class WeightFocusWiringTests(unittest.TestCase):
    def test_xaml_connects_weight_entry_callbacks_and_background_dismissal(self):
        import xml.etree.ElementTree as ET
        page = ET.fromstring(source("MainPage.xaml"))
        self.assertEqual("True", page.attrib.get("HideSoftInputOnTapped"))
        ns = "{http://schemas.microsoft.com/winfx/2009/xaml}"
        entries = [node for node in page.iter() if node.tag.endswith("}Entry") and node.attrib.get(ns + "Name") == "WeightInput"]
        self.assertEqual(1, len(entries))
        entry = entries[0]
        for event, callback in (("HandlerChanged", "OnWeightInputHandlerChanged"),
                                ("HandlerChanging", "OnWeightInputHandlerChanging"),
                                ("Completed", "OnWeightInputCompleted")):
            self.assertEqual(callback, entry.attrib.get(event))
        self.assertEqual("{Binding WeightText}", entry.attrib.get("Text"))
        self.assertEqual("{DynamicResource TextPrimary}", entry.attrib.get("TextColor"))

    def test_appearance_is_scoped_to_weight_handler_not_global_entry_mapping(self):
        page = source("MainPage.xaml.cs")
        native = source("Platforms/Android/WeightEntryHandler.cs")
        self.assertIn("OnWeightInputHandlerChanged", page)
        self.assertIn("WeightEntryHandler.RemoveUnderline(entry)", page)
        self.assertIn("native.Background = null;", native)
        self.assertNotIn("EntryHandler.Mapper", page + native)
        for forbidden in ("CursorVisible = false", "Focusable = false", "FocusableInTouchMode = false"):
            self.assertNotIn(forbidden, page + native)

    def test_keyboard_observer_ignores_initial_hidden_state_and_detaches(self):
        native = source("Platforms/Android/WeightEntryHandler.cs")
        for required in (
            "ViewCompat.GetRootWindowInsets(_native)",
            "insets.IsVisible(WindowInsetsCompat.Type.Ime())",
            "GetWindowVisibleDisplayFrame",  # API26/null-insets fallback
            "if (!_native.HasFocus)",
            "var wasVisible = _keyboardWasVisible;",
            "_keyboardWasVisible = visible;",
            "if (wasVisible && !visible)",
            "_entry.Unfocus();",
            "_native.ClearFocus();",
            "GlobalLayout += OnGlobalLayout;",
            "GlobalLayout -= OnGlobalLayout;",
            "_observer.IsAlive",
        ):
            self.assertIn(required, native)
        for forbidden in ("SetOnApplyWindowInsetsListener", "SetOnTouchListener", "RequestFocus("):
            self.assertNotIn(forbidden, native)

    def test_focus_cycles_reset_keyboard_observation(self):
        native = source("Platforms/Android/WeightEntryHandler.cs")
        self.assertIn("_native.FocusChange += OnFocusChange;", native)
        self.assertIn("_native.FocusChange -= OnFocusChange;", native)
        method = native.split("private void OnFocusChange", 1)
        self.assertEqual(len(method), 2)
        self.assertIn("_keyboardWasVisible = false;", method[1].split("private void", 1)[0])

    def test_page_owns_observer_lifetime_without_changing_consent_or_gestures(self):
        page = source("MainPage.xaml.cs")
        for required in (
            "Loaded += OnWeightInputLoaded;", "Unloaded += OnWeightInputUnloaded;",
            "OnWeightInputHandlerChanging", "OnWeightInputCompleted",
            "if (!_isAppearing || !IsLoaded", "_weightInputObserver?.Dispose();",
            "Loaded += (_, _) => QueueAnalyticsConsent();",
            "Services.AdsConsentPresentation.RunAsync", "_entranceCompleted = true;",
        ):
            self.assertIn(required, page)
        disappearing = page.split("protected override void OnDisappearing()", 1)[1].split("private void", 1)[0]
        self.assertIn("DetachWeightInputObserver();", disappearing)
        for forbidden in ("TapGestureRecognizer", "InputTransparent =", "SetOnTouchListener"):
            self.assertNotIn(forbidden, page)


if __name__ == "__main__":
    unittest.main()
