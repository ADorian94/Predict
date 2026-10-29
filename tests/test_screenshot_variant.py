"""Temporary screenshot identity and startup boundary; not a production consent fix."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[1]
class ScreenshotVariant(unittest.TestCase):
    def test_separate_install_identity(self):
        p = ET.parse(ROOT / 'Predict.csproj')
        self.assertEqual('hu.dorian.predict.screenshots', p.findtext('.//ApplicationId'))
        self.assertEqual('Predict Screenshots', p.findtext('.//ApplicationTitle'))
        self.assertEqual('2', p.findtext('.//ApplicationVersion'))
        self.assertEqual('1.0', p.findtext('.//ApplicationDisplayVersion'))
    def test_only_default_coordinator_is_registered(self):
        s = (ROOT / 'MauiProgram.cs').read_text(encoding='utf-8-sig')
        self.assertIn('AddSingleton<AdsConsentCoordinator>()', s)
        self.assertNotIn('AndroidAdsConsentBackend', s)
        self.assertNotIn('GetRequiredService<', s)
        self.assertNotIn('.Init(', s)
    def test_dev_profile_accepts_separate_screenshot_package(self):
        from test_build_profiles import BuildProfiles
        BuildProfiles.setUpClass()
        BuildProfiles().run_case({'PredictBuildProfile':'Dev', 'PredictTestAds':'true',
            'ApplicationId':'hu.dorian.predict.screenshots',
            'AndroidPackageFormats':'apk', 'AndroidPackageFormat':'apk'},
            symbols='PREDICT_TEST_ADS')
    def test_production_guard_rejects_screenshot_package(self):
        from test_build_profiles import BuildProfiles
        BuildProfiles.setUpClass()
        for target in ('CoreCompile', '_BuildApkEmbed', '_CopyPackage'):
            with self.subTest(target=target):
                BuildProfiles().run_case({'ApplicationId':'hu.dorian.predict.screenshots'},
                    code='PREDICT006', target=target)
    def test_missing_android_backend_is_optional(self):
        s = (ROOT / 'Platforms/Android/MainActivity.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('GetRequiredService', s)
        self.assertIn('GetService<AndroidAdsConsentBackend>()', s)
