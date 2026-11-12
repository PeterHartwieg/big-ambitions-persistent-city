from contextlib import nullcontext
import json
import os
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import run_flow

class SelfcheckEvidence(unittest.TestCase):
    def exercise(self,passed):
        with tempfile.TemporaryDirectory(prefix='e5b-selfcheck-offline-') as directory:
            root=Path(directory); evidence=root/'build/e5b/review-r3/rig_test/evidence'
            flow=root/'flow.json';flow.write_text('{"name":"offline only"}',encoding='utf-8')
            calls=[];seen={};checks={'actual_check':dict(passed=passed,detail='retained native-shaped diagnostic')}
            inventory=[dict(name='offline fixture only')]
            class Infra(RuntimeError):pass
            class FakeRig:
                def __init__(self):self.credential='offline-fixture-credential'
                def file_guard(self):return nullcontext()
            def selfcheck(**kwargs):
                calls.append((kwargs,os.environ.get('HARNESS_ALERTS')));return checks,inventory
            def session(document,path,rig_factory):
                rig=rig_factory()
                try:seen['result']=rig.selfcheck(notify='offline-notify-sentinel')
                except Infra as error:
                    seen['raised']=str(error)
                    seen['already_retained']=json.loads((evidence/'selfchecks/001.json').read_text())
                return dict(status='PASS' if 'result' in seen else 'INFRA',exit_code=0 if 'result' in seen else 3)
            modules={
                'rig_flows':SimpleNamespace(LocalRig=FakeRig,Infra=Infra,evidence_output=lambda p:p.resolve(),session=session,redact=lambda value:value),
                'rig_selfcheck':SimpleNamespace(run=selfcheck),
                'local_overlay':SimpleNamespace(safe_output=lambda p:None),
                'rig_modset':SimpleNamespace(plain=lambda p:None),
            }
            with patch.dict(sys.modules,modules),patch.dict(os.environ,{'HARNESS_ALERTS':'on'}),\
                 patch.object(sys,'argv',['run_flow.py',str(flow),'--evidence',str(evidence)]),\
                 patch.object(run_flow,'__file__',str(root/'tools/e5b-native-rig-proof/run_flow.py')):
                code=run_flow.main()
            stored=json.loads((evidence/'selfchecks/001.json').read_text())
            self.assertEqual(len(calls),1)
            self.assertEqual(calls[0],({'lock_token':'offline-fixture-credential','notify':'offline-notify-sentinel'},'off'))
            self.assertEqual(stored,dict(ok=passed,checks=checks,mod_inventory=inventory))
            return code,seen,stored
    def test_failure_is_retained_before_same_gate_raises(self):
        code,seen,stored=self.exercise(False)
        self.assertEqual(code,3);self.assertNotIn('result',seen)
        self.assertEqual(seen['already_retained'],stored)
        self.assertIn('Rig selfcheck failed',seen['raised'])
    def test_success_return_is_unchanged(self):
        code,seen,stored=self.exercise(True)
        self.assertEqual(code,0);self.assertNotIn('raised',seen);self.assertEqual(seen['result'],stored)

if __name__=='__main__':
    print('OFFLINE ONLY: fake native check modules and temporary files; no game/profile/lease/build operations.',flush=True)
    unittest.main()
