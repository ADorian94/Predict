import pathlib
import unittest
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parent.parent

class RpeWiringTests(unittest.TestCase):
    def test_unavailable_rpe_is_explained_in_result_and_share(self):
        root = ET.parse(ROOT / 'ResultPage.xaml').getroot()
        self.assertTrue(any(n.attrib.get('Text') == '{Binding Result.RpeAvailabilityNote}'
                            and n.attrib.get('IsVisible') == '{Binding Result.IsRpeUnavailable}'
                            for n in root.iter()), 'result page must show the unsupported-range notice')
        source = (ROOT / 'ViewModels/ResultViewModel.cs').read_text(encoding='utf-8-sig')
        self.assertIn('Result?.RoundingNote', source, 'caption must not hardcode eight formulas')
        self.assertIn('Result?.Formulas', source, 'screen must use the supported-only formula list')
        self.assertIn('Formulas.Select', source, 'share must use the same supported-only list')
        self.assertIn('{Result.RpeAvailabilityNote}', source, 'shared result must explain why RPE is absent')

if __name__ == '__main__':
    unittest.main()
