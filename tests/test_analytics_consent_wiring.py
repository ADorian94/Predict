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

    def test_settings_switch_is_two_way_and_initialization_does_not_write_consent(self):
        text = source("ViewModels/SettingsViewModel.cs")
        self.assertIn("private bool _isAnalyticsEnabled;", text)
        self.assertIn("_isAnalyticsEnabled = MixpanelService.IsAnalyticsEnabled;", text)
        self.assertRegex(text, r"OnIsAnalyticsEnabledChanged\(bool value\)\s*=>\s*MixpanelService.SetAnalyticsEnabled\(value\)")
        root = ET.fromstring(source("SettingsPage.xaml"))
        switches = [e for e in root.iter() if e.tag.endswith("}Switch")]
        self.assertEqual(len(switches), 1)
        self.assertEqual(switches[0].get("IsToggled"), "{Binding IsAnalyticsEnabled, Mode=TwoWay}")
        self.assertIn('Text="{Binding AnalyticsDisclosure}"', source("SettingsPage.xaml"))

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
