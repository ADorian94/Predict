"""Boundary wiring only; coordinator/VM behavior is production-linked in C# tests."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parent.parent
class AdsWiring(unittest.TestCase):
 def test_activity_does_not_initialize_sdk_before_ump(self):
  text=(ROOT/'Platforms/Android/MainActivity.cs').read_text(encoding='utf-8-sig')
  self.assertNotIn('CrossMauiMTAdmob.Current.Init(',text)
  for s in ('OnResume','OnPause','OnDestroy','AdsConsent'):
   self.assertIn(s,text)
 def test_no_ungated_banner_is_constructed_from_xaml(self):
  hosts=[]
  for name in ('MainPage.xaml','HistoryPage.xaml','ResultPage.xaml'):
   for e in ET.parse(ROOT/name).iter():
    self.assertNotIn('MTAdView',e.tag)
    if e.tag.endswith('}ConsentedBanner'): hosts.append(e)
  self.assertEqual(len(hosts),3)
  self.assertEqual([e.get('HeightRequest') for e in hosts],['60']*3)
 def test_result_has_no_direct_plugin_access(self):
  text=(ROOT/'ViewModels/ResultViewModel.cs').read_text(encoding='utf-8-sig')
  self.assertNotIn('CrossMauiMTAdmob',text)
  self.assertIn('PreloadInterstitial()',text)
  self.assertIn('TryShowInterstitialAsync()',text)
 def test_android_uses_free_binding_not_paid_wrapper(self):
  path=ROOT/'Platforms/Android/AndroidAdsConsentBackend.cs'
  self.assertTrue(path.exists(),'free UMP platform boundary missing')
  text=path.read_text()
  for s in ('Xamarin.Google.UserMesssagingPlatform','CanRequestAds()', 'UserMessagingPlatform.ShowPrivacyOptionsForm(', 'OnMobileAdsInitialized +=', 'initialiseConsentAtStartup: false'):
   self.assertIn(s,text)
  self.assertNotIn('.Reset()',text)
 def test_privacy_entry_separate_from_analytics(self):
  text=(ROOT/'SettingsPage.xaml').read_text(encoding='utf-8-sig')
  for s in ('ShowAdvertisingPrivacyCommand','Advertising privacy choices','{Binding AdvertisingPrivacyRequired}'):
   self.assertIn(s,text)
if __name__=='__main__': unittest.main()
