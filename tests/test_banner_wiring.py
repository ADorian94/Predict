"""Static XAML contract checks; these do not prove Android ad delivery."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
AD_NS = '{clr-namespace:Plugin.MauiMtAdmob.Controls;assembly=Plugin.MauiMtAdmob}'

class BannerWiringTests(unittest.TestCase):
    def banners(self):
        for filename in ('HistoryPage.xaml', 'ResultPage.xaml'):
            for element in ET.parse(ROOT / filename).iter():
                if element.tag.startswith(AD_NS) and 'AdsId' in element.attrib:
                    yield filename, element

    def test_admob_uses_admob_control(self):
        banners = list(self.banners())
        self.assertEqual(len(banners), 3)
        for filename, element in banners:
            with self.subTest(page=filename):
                self.assertEqual(element.tag, AD_NS + 'MTAdView')

    def test_all_slots_have_lifecycle_events_and_fixed_banner_size(self):
        for filename, element in self.banners():
            with self.subTest(page=filename):
                self.assertEqual(element.get('{clr-namespace:Predict.Services}BannerAdLifecycle.Observe'), 'True')
                self.assertEqual(element.get('AdSize'), 'Banner')

if __name__ == '__main__':
    unittest.main(verbosity=2)
