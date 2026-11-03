from pathlib import Path
import ast,json,sys,unittest
ROOT=Path(__file__).resolve().parents[1]
class HelperSdkPinTests(unittest.TestCase):
 def test_helper_sdk_matches_exact_gate_requirement_without_rollforward(self):
  tree=ast.parse((ROOT/'tools/ci/ads_attestation.py').read_text(encoding='utf-8-sig'))
  pins=[]
  for node in ast.walk(tree):
   if isinstance(node,ast.Constant) and isinstance(node.value,str):
    try: obj=json.loads(node.value)
    except (ValueError,TypeError): continue
    if isinstance(obj,dict) and 'sdk' in obj:pins.append(obj['sdk'])
  self.assertEqual(pins,[{'version':'9.0.319','rollForward':'disable'}])
if __name__=='__main__':unittest.main()
