"""Source wiring constraints; native layout/device acceptance remains separate."""
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[1]
class SummaryWeightFit(unittest.TestCase):
 def test_summary_single_line_adaptive_not_truncated(self):
  root=ET.parse(R/'ResultPage.xaml').getroot()
  labels=[n for n in root.iter() if n.get('Text')=='{Binding WeightDisplay}']
  self.assertEqual(1,len(labels))
  label=labels[0]
  self.assertTrue(label.tag.endswith('SummaryValueLabel'))
  self.assertEqual('NoWrap',label.get('LineBreakMode'))
  self.assertEqual('1',label.get('MaxLines'))
 def test_weight_gets_more_horizontal_room(self):
  root=ET.parse(R/'ResultPage.xaml').getroot()
  grid=next(n for n in root.iter() if n.get('ColumnDefinitions')=='2*,*,*')
  self.assertEqual('Fill',list(grid)[0].get('HorizontalOptions'))
 def test_native_autosizing_keeps_full_text(self):
  source=(R/'Controls/SummaryValueLabel.cs').read_text()
  for token in ['SetMaxLines(1)','Ellipsize = null','SetHorizontallyScrolling(false)','SetAutoSizeTextTypeUniformWithConfiguration','ComplexUnitType.Sp']:
   self.assertIn(token,source)
  self.assertNotIn('Substring',source)
if __name__=='__main__': unittest.main()
