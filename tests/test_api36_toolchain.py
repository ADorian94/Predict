"""Supported Android target and reproducible SDK selection."""
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[1]
class ToolchainTests(unittest.TestCase):
    def test_supported_target_and_exact_sdk_workload(self):
        project = ET.parse(ROOT / "Predict.csproj")
        self.assertEqual("net10.0-android36.0", project.findtext(".//TargetFrameworks"))
        self.assertEqual("10.0.120", project.findtext(".//MauiVersion"))
        self.assertIsNone(project.find(".//AndroidSdkPlatform"))
        self.assertEqual("26.0", project.findtext(".//SupportedOSPlatformVersion"))
        self.assertEqual("android-arm64", project.findtext(".//RuntimeIdentifier"))
        pin = json.loads((ROOT / "global.json").read_text())
        self.assertEqual({"version":"10.0.403", "rollForward":"disable", "allowPrerelease":False, "workloadVersion":"10.0.403"}, pin["sdk"])
        self.assertNotIn("workload", pin)
