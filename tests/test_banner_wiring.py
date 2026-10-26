"""XAML/host contract checks complement linked runtime tests, not Android delivery."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parent.parent
NS='{clr-namespace:Predict.Services}'
class BannerWiringTests(unittest.TestCase):
 def banners(self):
  for name in ('HistoryPage.xaml','ResultPage.xaml'):
   for e in ET.parse(ROOT/name).iter():
    if e.tag==NS+'ConsentedBanner': yield name,e
 def test_admob_uses_consent_host_in_all_original_slots(self):
  banners=list(self.banners()); self.assertEqual(len(banners),3)
  self.assertEqual([e.get('HeightRequest') for _,e in banners],['60']*3)
  self.assertEqual([e.get('Margin') for _,e in banners],['0,0,0,12',None,None])
  host=(ROOT/'Services/ConsentedBanner.cs').read_text()
  for text in ('new MTAdView','AdsId = AdUnits.Banner','AdSize = BannerSize.Banner','consent?.IsReady != true'):
   self.assertIn(text,host)
 def test_all_slots_retain_loaded_failed_lifecycle_and_native_teardown(self):
  host=(ROOT/'Services/ConsentedBanner.cs').read_text()
  self.assertIn('BannerAdLifecycle.SetObserve(banner, true)',host)
  self.assertIn('BannerAdLifecycle.SetObserve(old, false)',host)
  self.assertIn('native.Pause()',host); self.assertIn('native.Destroy()',host)
  self.assertIn('GetMethod("Destroy", Type.EmptyTypes)',host); self.assertIn('DisconnectHandler()',host)
  lifecycle=(ROOT/'Services/BannerAdLifecycle.cs').read_text()
  for text in ('banner.AdsLoaded += loaded','banner.AdsFailedToLoad += failed','snapshot.State.OnLoaded()','snapshot.State.OnFailed()'):
   self.assertIn(text,lifecycle)
if __name__=='__main__':unittest.main(verbosity=2)
