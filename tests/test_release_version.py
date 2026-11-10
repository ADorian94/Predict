"""Release version remains an integer at or above the share-fix baseline."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
MINIMUM_VERSION_CODE = 9


class ReleaseVersionTests(unittest.TestCase):
    def test_release_version_code_supports_current_and_future_releases(self):
        version = ET.parse(ROOT / 'Predict.csproj').findtext('.//ApplicationVersion')
        self.assertIsNotNone(version)
        self.assertRegex(version, r'^[1-9][0-9]*$')
        self.assertGreaterEqual(int(version), MINIMUM_VERSION_CODE)


if __name__ == '__main__':
    unittest.main()
