import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
class RepsWiringTests(unittest.TestCase):
 def test_bounds_have_disabled_visual_state(self):
  tree=ET.parse(ROOT/'MainPage.xaml'); ns={'m':'http://schemas.microsoft.com/dotnet/2021/maui'}
  for name in ['CanDecrementReps','CanIncrementReps']:
   matches=[e for e in tree.iter() if e.get('IsEnabled')=='{Binding '+name+'}']
   self.assertEqual(len(matches),1,name)
   triggers=matches[0].findall('.//m:Trigger',ns)
   self.assertTrue(any(t.get('Property')=='IsEnabled' and t.get('Value')=='False' and any(s.get('Property')=='Opacity' and float(s.get('Value'))<1 for s in t) for t in triggers),name)
if __name__=='__main__': unittest.main()
