"""Offline startup gate invariants; no native commands or lease/profile access."""
from copy import deepcopy
import unittest
from unittest.mock import Mock
from produce_fixture import StartupRefusal,wait_startup_ready

def ready(): return dict(scene='menu',agent_instance=dict(rig_input=dict(status='ready'),rig_ops=dict(status='ready',reason=None)),
    rig_mouse=dict(available=True,legacy_coverage=dict(pending=False,queue_dropped=0,scan_problems=[])))
class StartupGate(unittest.TestCase):
    def test_ready_immediate(self):
        fetch=Mock(return_value=ready());pause=Mock()
        self.assertEqual(wait_startup_ready(fetch,lambda:True,pause),ready())
        pause.assert_not_called()
    def test_exact_not_started_transitions_to_ready(self):
        starting=deepcopy(ready());starting['agent_instance']['rig_input']['status']='not started'
        starting['agent_instance']['rig_ops']=dict(status='disabled',reason='not started')
        fetch=Mock(side_effect=[starting,ready()]);pause=Mock()
        self.assertEqual(wait_startup_ready(fetch,lambda:True,pause),ready());pause.assert_called_once_with(.25)
    def test_hard_refusal_never_reaches_new_game_even_with_later_ready(self):
        bad=deepcopy(ready());bad['agent_instance']['rig_ops']=dict(status='disabled',reason='ops_hash_mismatch')
        fetch=Mock(side_effect=[bad,ready()]);pause=Mock();new_game=Mock()
        with self.assertRaises(StartupRefusal):
            wait_startup_ready(fetch,lambda:True,pause);new_game()
        self.assertEqual(fetch.call_count,1);pause.assert_not_called();new_game.assert_not_called()
    def test_process_exit_stops_before_health_or_new_game(self):
        fetch=Mock();new_game=Mock()
        with self.assertRaises(StartupRefusal):
            wait_startup_ready(fetch,lambda:False,Mock());new_game()
        fetch.assert_not_called();new_game.assert_not_called()
    def test_documented_pending_scan_waits_but_scan_errors_refuse(self):
        pending=deepcopy(ready());pending['rig_mouse']['legacy_coverage']['pending']=True
        fetch=Mock(side_effect=[pending,ready()]);pause=Mock()
        self.assertEqual(wait_startup_ready(fetch,lambda:True,pause),ready());pause.assert_called_once()
        pending['rig_mouse']['legacy_coverage']['scan_problems']=['actual scan failure']
        with self.assertRaises(StartupRefusal):wait_startup_ready(lambda:pending,lambda:True,Mock())
if __name__=='__main__':
    print('OFFLINE ONLY: mocked health/process state; no native operation.',flush=True)
    unittest.main()
