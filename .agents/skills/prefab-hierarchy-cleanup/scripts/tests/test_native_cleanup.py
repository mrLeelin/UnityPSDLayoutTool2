import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_native_cleanup import RunLog, check_envelope, run_process, claim_apply




def envelope(value='VERIFY_OK nodes=1'):
    return json.dumps({'success': True, 'data': {'success': True, 'result': {
        'success': True, 'result': value, 'diagnostics': []}}})


class NativeCleanupTests(unittest.TestCase):
    def test_nested_failure_not_masked_by_outer_success(self):
        raw = json.loads(envelope())
        raw['data']['result']['success'] = False
        with self.assertRaises(ValueError):
            check_envelope(json.dumps(raw), 'verify')

    def test_warn_missing_marker_and_bad_json_fail(self):
        for raw in [envelope('VERIFY_WARN issue=wrong member'), envelope('PREFLIGHT_OK'), '{}', 'bad']:
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                check_envelope(raw, 'verify')
        self.assertEqual(check_envelope(envelope(), 'verify'), 'VERIFY_OK nodes=1')

    def test_snapshot_requires_snapshot_marker(self):
        with self.assertRaises(ValueError):
            check_envelope(envelope('unrelated success'), 'snapshot')

    def test_heartbeat_and_timing_are_persisted(self):
        with tempfile.TemporaryDirectory() as directory:
            log = RunLog(Path(directory), 'target')
            run_process(log, 'wait_test', [sys.executable, '-c', 'import time; time.sleep(.12); print("done")'], Path(directory), timeout=2, heartbeat=.03)
            events = [json.loads(line) for line in log.path.read_text(encoding='utf-8').splitlines()]
            self.assertEqual(events[0]['event'], 'start')
            self.assertTrue(any(e['event'] == 'heartbeat' for e in events))
            self.assertEqual(events[-1]['event'], 'end')
            self.assertGreaterEqual(events[-1]['elapsed_ms'], 100)
            self.assertTrue(all('utc' in e and 'run_id' in e for e in events))

    def test_process_failure_and_timeout_are_not_success(self):
        for script, timeout in [('raise SystemExit(3)', 2), ('import time; time.sleep(5)', .05)]:
            with tempfile.TemporaryDirectory() as directory:
                log = RunLog(Path(directory), 'target')
                with self.assertRaises((RuntimeError, TimeoutError)):
                    run_process(log, 'failure', [sys.executable, '-c', script], Path(directory), timeout=timeout, heartbeat=.02)
                last = json.loads(log.path.read_text(encoding='utf-8').splitlines()[-1])
                self.assertIn(last['status'], ['failed', 'timeout'])

    def test_apply_claim_survives_new_run_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            claim_apply(project, 'Assets/UI.prefab', b'{"plan":1}')
            with self.assertRaises(FileExistsError):
                claim_apply(project, 'Assets/UI.prefab', b'{"plan":1}')


if __name__ == '__main__':
    unittest.main()
