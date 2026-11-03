"""Offline lifecycle invariants. Fake registry/lease; temporary workspace files only."""
from contextlib import contextmanager
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('e5b_run_flow',Path(__file__).with_name('run_flow.py'))
runner=importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)
OUT=Path(__file__).resolve().parents[2]/'build/e5b/review-r3/rig_test'

class LifecycleInvariants(unittest.TestCase):
    def setUp(self):
        OUT.mkdir(parents=True,exist_ok=True)
        self.temp=tempfile.TemporaryDirectory(prefix='mock-lifecycle-',dir=OUT)
        self.root=Path(self.temp.name).resolve()
        self.assertTrue(self.root.is_relative_to(OUT.resolve())) # Before owned recursive temp cleanup.
        self.paths=(self.root/'run.json',self.root/'settings.json')
        self.paths[0].write_bytes(b'original run metadata')
        self.original=('Windows Registry Editor Version 5.00\r\n\r\n['+runner.REG_EXPORT+']\r\n"pref"=dword:00000001\r\n').encode('utf-16')
        self.state=self.original
        self.calls=[]
        self.guards=0
        self.patches=[patch.object(runner,'TARGETS',self.paths),patch.object(runner,'registry_exists',lambda:self.state is not None),patch.object(runner,'reg',self.reg)]
        for mocked in self.patches: mocked.start()
        self.lifecycle=runner.Lifecycle(self.root/'backup',self.guard)
    def tearDown(self):
        for mocked in reversed(self.patches): mocked.stop()
        self.assertTrue(self.root.is_relative_to(OUT.resolve()))
        self.temp.cleanup()
    @contextmanager
    def guard(self):
        self.guards+=1
        yield
    def reg(self,operation,*arguments):
        self.calls.append(operation)
        if operation=='export': Path(arguments[1]).write_bytes(self.state)
        elif operation=='delete': self.state=None
        elif operation=='import': self.state=Path(arguments[0]).read_bytes()
        else: raise AssertionError('Unexpected simulated registry command')
    def test_exact_restore_and_removal_of_originally_absent_file(self):
        self.lifecycle.capture()
        self.paths[0].write_bytes(b'new player metadata')
        self.paths[1].write_bytes(b'created settings')
        self.state=self.original+'\r\n"added_during_game"="changed"\r\n'.encode('utf-16-le')
        self.assertNotEqual(self.state,self.original)
        self.lifecycle.restore()
        self.assertEqual(self.paths[0].read_bytes(),b'original run metadata')
        self.assertFalse(self.paths[1].exists())
        self.assertEqual(self.state,self.original)
        self.assertEqual(self.guards,2)
    def test_corrupt_backup_refuses_before_overwriting_active_files(self):
        self.lifecycle.capture()
        self.paths[0].write_bytes(b'active native output')
        (self.root/'backup/file-0.bin').write_bytes(b'corrupted')
        with self.assertRaisesRegex(RuntimeError,'Corrupt lifecycle backup'):
            self.lifecycle.restore()
        self.assertEqual(self.paths[0].read_bytes(),b'active native output')
        self.assertNotIn('delete',self.calls)
    def test_original_registry_absence_is_restored(self):
        self.state=None
        self.lifecycle.capture()
        self.state=self.original
        self.lifecycle.restore()
        self.assertIsNone(self.state)
        self.assertIn('delete',self.calls)
    def test_foreign_registry_key_is_refused_during_capture(self):
        self.state=('Windows Registry Editor Version 5.00\r\n[HKEY_CURRENT_USER\\Software\\Hovgaard Games\\Big Ambitions]\r\n').encode('utf-16')
        with self.assertRaisesRegex(RuntimeError,'unexpected key'):
            self.lifecycle.capture()
        self.assertIsNone(self.lifecycle.manifest)
        self.assertNotIn('delete',self.calls)
        self.assertEqual(self.paths[0].read_bytes(),b'original run metadata')
    def test_modified_restoration_target_is_refused(self):
        self.lifecycle.capture()
        self.lifecycle.manifest['files'][0]['path']=str(self.root.parent/'foreign.json')
        with self.assertRaisesRegex(RuntimeError,'Unexpected lifecycle restoration targets'):
            self.lifecycle.restore()
        self.assertNotIn('delete',self.calls)

if __name__=='__main__':
    print('OFFLINE ONLY: fake registry/lease, temporary owned files; no native/profile/lease commands.',flush=True)
    unittest.main()
