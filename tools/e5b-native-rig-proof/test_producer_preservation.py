"""Offline producer preservation invariants; fake lease, temporary opaque files only."""
from contextlib import contextmanager
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import produce_fixture as producer

OUT=Path(__file__).resolve().parents[2]/'build/e5b/review-r3/rig_test'
class ProducerPreservation(unittest.TestCase):
    def setUp(self):
        OUT.mkdir(parents=True,exist_ok=True)
        self.temp=tempfile.TemporaryDirectory(prefix='mock-producer-',dir=OUT)
        self.root=Path(self.temp.name).resolve()
        self.assertTrue(self.root.is_relative_to(OUT.resolve()))
        self.profile=self.root/'profile'; self.old=self.profile/'SaveGames/Big Ambitions/old/FT opaque.hsg'
        self.old.parent.mkdir(parents=True); self.old.write_bytes(b'opaque offline original')
        self.patched=patch.object(producer,'PROFILE',self.profile);self.patched.start()
        self.saves=producer.ExistingSaves(self.root/'backup',self.guard)
    def tearDown(self):
        self.patched.stop()
        self.assertTrue(self.root.is_relative_to(OUT.resolve())) # Before owned recursive test cleanup.
        self.temp.cleanup()
    @contextmanager
    def guard(self): yield
    def report(self): return json.loads((self.root/'backup/verification.json').read_text())
    def test_only_new_character_files_are_retained_with_old_bytes_intact(self):
        self.saves.capture(); new=self.profile/'SaveGames/Big Ambitions/new/FT E5b mock.hsg'
        new.parent.mkdir();new.write_bytes(b'opaque new output');self.saves.owned_character=new.parent
        self.saves.verify()
        self.assertEqual(self.old.read_bytes(),b'opaque offline original');self.assertTrue(new.exists())
        self.assertTrue(self.report()['verified']);self.assertEqual(self.report()['unexpected_additions'],[])
    def test_unexpected_original_write_is_restored_but_never_passed(self):
        self.saves.capture();self.old.write_bytes(b'unexpected mutation')
        with self.assertRaisesRegex(RuntimeError,'Unexpected isolated-save changes'):self.saves.verify()
        self.assertEqual(self.old.read_bytes(),b'opaque offline original')
        self.assertEqual(len(self.report()['unexpected_original_changes']),1)
    def test_new_file_in_old_character_refuses_producer_acceptance(self):
        self.saves.capture();extra=self.old.with_name('unexpected-added.hsg');extra.write_bytes(b'new wrong character')
        self.saves.owned_character=self.profile/'SaveGames/Big Ambitions/new'
        with self.assertRaisesRegex(RuntimeError,'Unexpected isolated-save changes'):self.saves.verify()
        self.assertTrue(extra.exists());self.assertEqual(len(self.report()['unexpected_additions']),1)
    def test_corrupt_backup_cannot_overwrite_current_original(self):
        self.saves.capture();self.old.write_bytes(b'changed current bytes')
        backup=self.root/'backup/originals'/self.old.relative_to(self.saves.root);backup.write_bytes(b'corrupt backup')
        with self.assertRaisesRegex(RuntimeError,'Corrupt original-save backup'):self.saves.verify()
        self.assertEqual(self.old.read_bytes(),b'changed current bytes')

if __name__=='__main__':
    print('OFFLINE ONLY: temporary opaque test bytes and fake permit; no native city/profile/lease operations.',flush=True)
    unittest.main()
