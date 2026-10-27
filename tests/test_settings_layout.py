"""MAUI XML layout contracts; native renderer/device acceptance is separate."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
NS = {'m': 'http://schemas.microsoft.com/dotnet/2021/maui'}


class SettingsLayoutTests(unittest.TestCase):
    def setUp(self):
        self.root = ET.parse(ROOT / 'SettingsPage.xaml').getroot()
        self.parents = {child: parent for parent in self.root.iter() for child in parent}

    def one(self, nodes):
        nodes = list(nodes)
        self.assertEqual(len(nodes), 1)
        return nodes[0]

    def command(self, name):
        return self.one(e for e in self.root.iter() if e.get('Command') == '{Binding ' + name + '}')

    def test_analytics_explanation_precedes_equal_choices_and_details_is_last(self):
        enabled = self.command('EnableAnalyticsCommand')
        grid = self.parents[self.parents[self.parents[enabled]]]
        self.assertEqual(grid.tag, '{' + NS['m'] + '}Grid')
        stack = self.parents[grid]
        children = list(stack)
        explanation = self.one(e for e in stack.findall('m:Label', NS)
                               if 'Mixpanel' in e.get('Text', ''))
        details = self.parents[self.parents[self.command('ShowAnalyticsDetailsCommand')]]
        self.assertEqual(children, [explanation, grid, details],
                         'Analytics must read explanation -> choices -> privacy link')
        self.assertEqual(grid.get('ColumnDefinitions'), '*,*')
        self.assertEqual([e.get('Grid.Column') for e in grid], ['0', '1'])
        self.assertIn('No workout values', explanation.get('Text'))

    def advertising_group(self):
        heading = self.one(e for e in self.root.findall('.//m:Label', NS)
                           if e.get('Text') == 'ADVERTISING')
        return self.parents[heading], heading

    def test_advertising_heading_and_card_share_complete_ump_visibility_gate(self):
        group, heading = self.advertising_group()
        self.assertEqual(group.get('IsVisible'), '{Binding AdvertisingPrivacyRequired}')
        card = self.one(group.findall('m:Border', NS))
        self.assertEqual(list(group), [heading, card])
        reference = self.one(e for e in self.root.findall('.//m:Label', NS)
                             if e.get('Text') == 'OPTIONAL ANALYTICS')
        for key in ('FontSize', 'FontAttributes', 'TextColor', 'CharacterSpacing', 'Margin'):
            self.assertEqual(heading.get(key), reference.get(key))
        self.assertTrue(any(e.tag.endswith('}ScrollView') and group in list(e.iter())
                            for e in self.root.iter()))

    def test_advertising_review_is_inner_accessible_full_width_button_after_copy(self):
        action = self.command('ShowAdvertisingPrivacyCommand')
        self.assertEqual(action.tag, '{' + NS['m'] + '}Button',
                         'Only an inner native Button should open advertising privacy')
        self.assertEqual(action.get('Text'), 'REVIEW')
        self.assertTrue(action.get('SemanticProperties.Description'))
        self.assertEqual(action.get('HorizontalOptions'), 'Fill')
        self.assertIn(action.get('Background'), ('Transparent', '{x:Null}'))
        self.assertEqual(action.get('Shadow'), '{x:Null}')
        self.assertEqual(action.get('CornerRadius'), '16')
        group, _ = self.advertising_group()
        card = self.one(group.findall('m:Border', NS))
        self.assertIsNone(card.find('m:Border.GestureRecognizers', NS))
        stack = self.one(card.findall('m:VerticalStackLayout', NS))
        self.assertEqual(list(stack)[0].tag, '{' + NS['m'] + '}Label')
        self.assertIn('Separate from usage analytics', list(stack)[0].get('Text'))
        self.assertIn(action, list(list(stack)[-1].iter()))

    def test_consent_cards_use_live_theme_resources_and_flexible_dimensions(self):
        group, _ = self.advertising_group()
        analytics_grid = self.parents[self.parents[self.parents[self.command('EnableAnalyticsCommand')]]]
        analytics_card = self.parents[self.parents[analytics_grid]]
        colors = ET.parse(ROOT / 'Resources/Styles/Colors.xaml').getroot()
        keys = {e.get('{http://schemas.microsoft.com/winfx/2009/xaml}Key') for e in colors}
        theme = (ROOT / 'Services/ThemeService.cs').read_text(encoding='utf-8-sig')
        dark, light = theme.split('private static void ApplyDark', 1)[1].split('private static void ApplyLight', 1)
        for outer in (analytics_card, self.one(group.findall('m:Border', NS))):
            self.assertEqual(outer.get('Background'), '{DynamicResource CardBackgroundBrush}')
            self.assertEqual(outer.get('Stroke'), '{DynamicResource CardBorderBrush}')
            self.assertTrue(any(e.get('CornerRadius') == '20' for e in outer.iter()))
        for container in (analytics_card, group):
            for element in container.iter():
                self.assertIsNone(element.get('WidthRequest'))
                self.assertIsNone(element.get('HeightRequest'))
                if element.tag.endswith('}Label'):
                    self.assertNotIn(element.get('LineBreakMode'), ('NoWrap', 'HeadTruncation', 'TailTruncation'))
                    self.assertIsNone(element.get('MaxLines'))
                for value in element.attrib.values():
                    if value.startswith('{DynamicResource '):
                        key = value[len('{DynamicResource '):-1]
                        self.assertIn(key, keys)
                        for palette in (dark, light):
                            self.assertIn('r["' + key + '"]', palette)
        inner = self.parents[self.command('ShowAdvertisingPrivacyCommand')]
        self.assertEqual(inner.get('Stroke'), '{DynamicResource CardBorderBrush}')
        self.assertTrue(any(e.get('CornerRadius') == '16' for e in inner.iter()))


if __name__ == '__main__':
    unittest.main()
