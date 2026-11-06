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
 def summary_grid(self):
  root=ET.parse(R/'ResultPage.xaml').getroot()
  weight=next(n for n in root.iter() if n.get('Text')=='{Binding WeightDisplay}')
  stack=next(n for n in root.iter() if weight in list(n))
  return next(n for n in root.iter() if stack in list(n))
 def test_summary_columns_have_equal_width(self):
  grid=self.summary_grid()
  self.assertEqual('*,*,*',grid.get('ColumnDefinitions'))
  self.assertEqual('Fill',list(grid)[0].get('HorizontalOptions'))
 def test_summary_headings_and_values_are_centered_in_their_columns(self):
  stacks=list(self.summary_grid())
  self.assertEqual(3,len(stacks))
  for column,heading in enumerate(('WEIGHT','REPS','RPE')):
   with self.subTest(heading=heading):
    stack=stacks[column]
    self.assertEqual(str(column),stack.get('Grid.Column'))
    self.assertIn(stack.get('HorizontalOptions'),('Fill','Center'))
    labels=list(stack)
    self.assertEqual(2,len(labels))
    self.assertEqual(heading,labels[0].get('Text'))
    for label in labels:
     self.assertEqual('Center',label.get('HorizontalTextAlignment'))
 def test_summary_preserves_compact_vertical_dimensions(self):
  grid=self.summary_grid()
  root=ET.parse(R/'ResultPage.xaml').getroot()
  border=next(n for n in root.iter() if grid.tag==n.tag and
              n.get('ColumnDefinitions')==grid.get('ColumnDefinitions')
              and any(c.get('Text')=='WEIGHT' for c in n.iter()))
  parent=next(n for n in root.iter() if border in list(n))
  self.assertEqual('18,14',parent.get('Padding'))
  self.assertEqual('8',grid.get('ColumnSpacing'))
  for node in [parent,*grid.iter()]:
   self.assertIsNone(node.get('HeightRequest'))
   self.assertIsNone(node.get('MinimumHeightRequest'))
  for stack in list(grid):
   self.assertIsNone(stack.get('Spacing'))
   labels=list(stack)
   self.assertEqual('11',labels[0].get('FontSize'))
   self.assertEqual('20',labels[1].get('FontSize'))
 def test_native_autosizing_keeps_full_text(self):
  source=(R/'Controls/SummaryValueLabel.cs').read_text()
  for token in ['SetMaxLines(1)','Ellipsize = null','SetHorizontallyScrolling(false)','SetAutoSizeTextTypeUniformWithConfiguration','ComplexUnitType.Sp']:
   self.assertIn(token,source)
  self.assertNotIn('Substring',source)
if __name__=='__main__': unittest.main()
