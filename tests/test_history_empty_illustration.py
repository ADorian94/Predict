"""Source contracts, not native layout/device acceptance. No external fixtures."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
NS = {'m': 'http://schemas.microsoft.com/dotnet/2021/maui'}
ASSET = ROOT / 'Resources/Images/history_empty.svg'
# Canonical original: ordered XML tree, sorted non-paint attributes and text.
# Excludes fill/stroke only; includes all geometry, transforms, and metadata.
ORIGINAL_GEOMETRY = '4e5f05f3b7844e0743df9f72f1952d3b01cb614559e4d94ea0990cbb546a9f79'
ORIGINAL_HISTORY_OUTSIDE_EMPTY = '582bada25ddbe80df432ca94a932e306c54c51520daec67cf9e92ee1a1e967e6'


def canonical_geometry(element):
    return [element.tag,
            sorted((k, v) for k, v in element.attrib.items() if k not in ('fill', 'stroke')),
            (element.text or '').strip(), [canonical_geometry(c) for c in element]]


class HistoryEmptyIllustrationTests(unittest.TestCase):
    def setUp(self):
        self.page = ET.parse(ROOT / 'HistoryPage.xaml').getroot()
        self.empty = self.page.find('.//m:CollectionView.EmptyView', NS)

    def asset(self):
        self.assertTrue(ASSET.is_file(), 'The recolored empty-history SVG is missing')
        return ET.parse(ASSET).getroot()

    def image(self):
        images = self.empty.findall('.//m:Image', NS)
        self.assertEqual(len(images), 1, 'EmptyView must contain exactly one illustration')
        return images[0]

    def test_image_exists_only_inside_collection_empty_view(self):
        image = self.image()
        self.assertEqual(self.page.findall('.//m:Image', NS), [image])
        self.assertEqual(image.get('Source'), 'history_empty.png')
        self.assertEqual(image.get('InputTransparent'), 'True')
        self.assertEqual(image.get('AutomationProperties.IsInAccessibleTree'), 'False')
        self.assertFalse(any(e.get('IsVisible') or e.get('BindingContext') for e in self.empty.iter()))

    def test_finite_responsive_layout_uses_contentview_grid_and_aspect_fit(self):
        self.assertEqual(len(self.empty), 1)
        root = self.empty[0]
        self.assertEqual(root.tag, '{' + NS['m'] + '}ContentView')
        grid = root.find('m:Grid', NS)
        self.assertIsNotNone(grid)
        self.assertEqual(grid.get('RowDefinitions'), '*,Auto')
        self.assertEqual(grid.get('VerticalOptions'), 'Center')
        self.assertEqual(grid.get('HorizontalOptions'), 'Fill')
        self.assertEqual(grid.get('MaximumHeightRequest'), '280')
        self.assertEqual(grid.get('MaximumWidthRequest'), '320')
        image = self.image()
        self.assertIn(image, list(grid))
        self.assertEqual(image.get('Grid.Row'), '0')
        self.assertEqual(image.get('Aspect'), 'AspectFit')
        self.assertEqual(image.get('MaximumHeightRequest'), '239')
        self.assertEqual(image.get('HorizontalOptions'), 'Fill')
        self.assertEqual(image.get('VerticalOptions'), 'Fill')
        for node in root.iter():
            for fixed in ('HeightRequest', 'WidthRequest', 'MinimumHeightRequest', 'MinimumWidthRequest'):
                self.assertIsNone(node.get(fixed), 'Upper bounds, not fixed/minimum dimensions, allow shrinkage')
            self.assertFalse(node.tag.endswith(('StackLayout', 'ScrollView')))

    def test_existing_empty_copy_remains_live_themed_and_wrappable(self):
        label = self.empty.find('.//m:Label', NS)
        self.assertIsNotNone(label)
        self.assertEqual(label.get('Text'), 'No history yet.')
        self.assertEqual(label.get('FontSize'), '14')
        self.assertEqual(label.get('TextColor'), '{DynamicResource LabelMuted}')
        self.assertEqual(label.get('HorizontalTextAlignment'), 'Center')
        self.assertEqual(label.get('Grid.Row'), '1')
        self.assertIsNone(label.get('MaxLines'))
        self.assertNotIn(label.get('LineBreakMode'), ('NoWrap', 'TailTruncation'))

    def test_collection_entries_templates_commands_and_other_markup_unchanged(self):
        collection = self.page.find('.//m:CollectionView', NS)
        self.assertEqual(collection.get('ItemsSource'), '{Binding Entries}')
        self.assertEqual(collection.get('ItemTemplate'), '{StaticResource ItemSelector}')
        self.empty.clear()
        self.assertEqual(hashlib.sha256(ET.tostring(self.page)).hexdigest(), ORIGINAL_HISTORY_OUTSIDE_EMPTY)

    def test_svg_is_packaged_by_maui_and_referenced_as_generated_png(self):
        self.asset()
        project = ET.parse(ROOT / 'Predict.csproj').getroot()
        images = project.findall('.//MauiImage')
        self.assertTrue(any(e.get('Include', '').replace('\\', '/') == 'Resources/Images/*' for e in images))
        update = [e for e in images if e.get('Update', '').replace('\\', '/') == 'Resources/Images/history_empty.svg']
        self.assertEqual(len(update), 1)
        self.assertEqual(update[0].get('BaseSize'), '320,239')
        self.assertEqual(self.image().get('Source'), ASSET.with_suffix('.png').name)

    def test_geometry_and_source_metadata_match_original_canonical_digest(self):
        svg = self.asset()
        encoded = json.dumps(canonical_geometry(svg), ensure_ascii=True, separators=(',', ':')).encode()
        self.assertEqual(hashlib.sha256(encoded).hexdigest(), ORIGINAL_GEOMETRY)
        self.assertEqual(svg.get('artist'), 'Katerina Limpitsouni')
        self.assertEqual(svg.get('source'), 'https://undraw.co/')

    def test_predict_palette_skin_and_white_are_preserved(self):
        fills = Counter(e.get('fill') for e in self.asset().iter())
        self.assertEqual(fills, Counter({None: 1, '#fff': 18, '#9e616a': 5, '#ffb8b8': 3,
                                        '#ffb6b6': 2, '#7C3AED': 2, '#8B7CB3': 11,
                                        '#A496C4': 8, '#DDD6F0': 7, '#EDE9F5': 3,
                                        '#C4B5DD': 1}))
        colors = (ROOT / 'Resources/Styles/Colors.xaml').read_text(encoding='utf-8-sig')
        self.assertIn('>#7C3AED</Color>', colors)
        self.assertFalse(any(e.get('href') or e.get('{http://www.w3.org/1999/xlink}href')
                             for e in self.asset().iter()), 'Self-contained SVG only')

    def test_provenance_ships_with_source_outside_image_glob(self):
        path = ROOT / 'Resources/history_empty.provenance.txt'
        self.assertTrue(path.is_file(), 'Document the artwork source and complete recolor mapping')
        text = path.read_text(encoding='utf-8')
        for token in ('unDraw', 'Katerina Limpitsouni', 'https://undraw.co/', '#6c63ff -> #7C3AED',
                      '#2f2e41 -> #8B7CB3', '#3f3d56 -> #A496C4', '#e6e6e6 -> #DDD6F0',
                      '#f2f2f2 -> #EDE9F5', '#ccc -> #C4B5DD',
                      '98bd4df3b046b2775253023c940f2a63b76c19213f476ab3bd3b91ed759bfe97'):
            self.assertIn(token, text)


if __name__ == '__main__':
    unittest.main()
