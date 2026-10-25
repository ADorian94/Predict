"""UI wiring complements production-linked, network-isolated C# consent tests."""
import pathlib
import re
import unittest
import xml.etree.ElementTree as ET
ROOT = pathlib.Path(__file__).resolve().parent.parent

def source(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")

class AnalyticsConsentWiringTests(unittest.TestCase):
    def test_main_page_prompts_even_on_return_after_prompt_failure(self):
        text = source("MainPage.xaml.cs")
        self.assertIn("await Services.MixpanelService.EnsureAnalyticsConsentAsync", text)
        self.assertLess(text.index("EnsureAnalyticsConsentAsync"), text.index("if (_hasAppeared) return"))
        self.assertIn('DisplayAlert("Optional usage analytics",', text)
        self.assertIn('Services.MixpanelService.AnalyticsDisclosure, "Allow", "No thanks")', text)

    def test_settings_cards_match_existing_design_and_do_not_write_on_initialization(self):
        text = source("ViewModels/SettingsViewModel.cs")
        self.assertIn("_isAnalyticsEnabled = MixpanelService.IsAnalyticsEnabled;", text)
        root = ET.fromstring(source("SettingsPage.xaml"))
        self.assertFalse(any(e.tag.endswith("}Switch") for e in root.iter()))
        for state, command in (("Enabled", "EnableAnalyticsCommand"), ("Disabled", "DisableAnalyticsCommand")):
            cards = [e for e in root.iter() if e.tag.endswith("}Border")
                     and e.get("Background") == "{Binding Analytics" + state + "Background}"]
            self.assertEqual(len(cards), 1)
            card = cards[0]
            self.assertEqual(card.get("Padding"), "0,16")
            self.assertEqual(card.get("StrokeThickness"), "1.5")
            self.assertTrue(any(e.get("Command") == "{Binding " + command + "}" for e in card.iter()))
            self.assertTrue(any(e.get("CornerRadius") == "16" for e in card.iter()))
        self.assertIn("ShowAnalyticsDetailsCommand", source("SettingsPage.xaml"))
        self.assertNotIn('Text="{Binding AnalyticsDisclosure}"', source("SettingsPage.xaml"))

    def test_privacy_page_discloses_optional_pseudonymous_analytics(self):
        text = source("docs/index.html")
        self.assertIn("Mixpanel EU", text)
        self.assertIn("pseudonymous", text)
        self.assertIn("No thanks", text)
        self.assertIn("analytics are off by default", text)
        self.assertNotIn("Predict does not collect, store, or transmit personal data to external servers", text)

    def test_disclosure_describes_scope_without_claiming_anonymity(self):
        text = source("Services/MixpanelService.cs")
        self.assertIn("public const string AnalyticsDisclosure", text)
        for word in ("Mixpanel EU", "random installation ID", "pseudonymous", "app version",
                     "weight", "reps", "RPE", "estimates", "Settings", "advertising consent"):
            self.assertIn(word, text)
        self.assertNotIn("anonymous", text.lower())

if __name__ == "__main__":
    unittest.main()
