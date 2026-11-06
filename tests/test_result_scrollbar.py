"""Source-level result-page contract; not a native gesture/rendering test."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
MAUI = '{http://schemas.microsoft.com/dotnet/2021/maui}'
XAML = '{http://schemas.microsoft.com/winfx/2009/xaml}'


class ResultScrollbarTests(unittest.TestCase):
    def test_code5_identity_preserves_display_version_and_package(self):
        project = ET.parse(ROOT / 'Predict.csproj').getroot()
        self.assertEqual('5', project.findtext('.//ApplicationVersion'))
        self.assertEqual('1.0', project.findtext('.//ApplicationDisplayVersion'))
        self.assertEqual('hu.dorian.predict', project.findtext('.//ApplicationId'))

    def test_result_indicator_hidden_without_disabling_vertical_scrolling(self):
        page = ET.parse(ROOT / 'ResultPage.xaml').getroot()
        scrollers = list(page.iter(MAUI + 'ScrollView'))
        self.assertEqual(1, len(scrollers))
        scroll = scrollers[0]
        self.assertEqual('Never', scroll.get('VerticalScrollBarVisibility'))
        self.assertEqual('Vertical', scroll.get('Orientation', 'Vertical'))
        self.assertEqual('True', scroll.get('IsEnabled', 'True'))
        self.assertEqual('False', scroll.get('InputTransparent', 'False'))
        self.assertEqual('1', scroll.get('Grid.Row'))
        self.assertNotIn('HeightRequest', scroll.attrib)
        self.assertNotIn('MaximumHeightRequest', scroll.attrib)
        self.assertNotIn('Clip', scroll.attrib)
        self.assertEqual([MAUI + 'VerticalStackLayout'], [e.tag for e in scroll])
        names = {e.get(XAML + 'Name') for e in scroll.iter()}
        self.assertTrue({'BestCard', 'FormulasCard', 'PctCard'} <= names)
        self.assertTrue(any(e.get('Command') == '{Binding NewEstimationCommand}'
                            for e in scroll.iter(MAUI + 'Button')))


if __name__ == '__main__':
    unittest.main()
