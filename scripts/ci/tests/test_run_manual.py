"""Exercise the native launcher without starting external services."""
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parents[3]
FAKE_TOOL = r'''
import json, os, pathlib, sys, time
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
if name == 'dotnet' and args[:2] == ['user-secrets', 'list']:
    if 'BranchBooking.Api' in args[-1]:
        print('ConnectionStrings:BranchBooking = local-test-only')
    sys.exit(0)
if name == 'npm' and args == ['ci']:
    sys.exit(0)
if name == 'dotnet' and args[0] == 'build':
    sys.exit(0)
entry = {'tool': name, 'args': args, 'pid': os.getpid(),
         'channel': os.getenv('Notifications__Channel', 'Simulated'),
         'host': os.getenv('Notifications__Smtp__Host'),
         'security': os.getenv('Notifications__Smtp__Security')}
fd = os.open(os.environ['LAUNCHER_TEST_LOG'], os.O_WRONLY | os.O_CREAT | os.O_APPEND, 0o600)
os.write(fd, (json.dumps(entry) + '\n').encode())
os.close(fd)
if name == 'mailpit' and os.getenv('LAUNCHER_TEST_MAILPIT_FAIL'):
    sys.exit(3)
while True:
    time.sleep(1)
'''


class ManualLauncherTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'scripts').mkdir()
        (self.root / 'frontend/branchbooking-web').mkdir(parents=True)
        shutil.copy2(ROOT / 'scripts/run-manual.sh', self.root / 'scripts/run-manual.sh')
        tools = self.root / 'bin'
        tools.mkdir()
        for name in ('dotnet', 'node', 'npm', 'mailpit'):
            path = tools / name
            path.write_text(f'#!{sys.executable}\n' + FAKE_TOOL)
            path.chmod(0o755)
        self.log = self.root / 'services.jsonl'
        self.env = {k: v for k, v in os.environ.items()
                    if not k.startswith('Notifications__') and k != 'ConnectionStrings__BranchBooking'}
        self.env.update(PATH=str(tools) + os.pathsep + os.defpath,
                        LAUNCHER_TEST_LOG=str(self.log), ConnectionStrings__BranchBooking='local-test-only')

    def entries(self):
        return [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []

    def start(self, *args):
        process = subprocess.Popen(['bash', str(self.root / 'scripts/run-manual.sh'), *args],
                                   env=self.env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.addCleanup(self.stop, process)
        return process

    def stop(self, process):
        if process.poll() is None:
            process.send_signal(signal.SIGTERM)
            process.wait(timeout=5)

    def wait_for_services(self, process, count):
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            if len(self.entries()) >= count:
                return self.entries()
            self.assertIsNone(process.poll(), 'Launcher exited before services started')
            time.sleep(.05)
        self.fail('Services did not start')

    def assert_stopped(self):
        for entry in self.entries():
            with self.assertRaises(ProcessLookupError):
                os.kill(entry['pid'], 0)

    def test_mailpit_starts_worker_with_local_smtp_and_cleans_up(self):
        process = self.start('--mailpit')
        entries = self.wait_for_services(process, 4)
        mailpit = next(e for e in entries if e['tool'] == 'mailpit')
        self.assertEqual(mailpit['args'], ['--listen', '127.0.0.1:8025', '--smtp', '127.0.0.1:1025'])
        worker = next(e for e in entries if 'src/BranchBooking.Worker' in e['args'])
        self.assertEqual((worker['channel'], worker['host'], worker['security']), ('Smtp', '127.0.0.1', 'None'))
        self.stop(process)
        self.assert_stopped()

    def test_default_starts_worker_without_mailpit_and_preserves_settings(self):
        self.env['Notifications__Channel'] = 'Simulated'
        process = self.start()
        entries = self.wait_for_services(process, 3)
        self.assertFalse(any(e['tool'] == 'mailpit' for e in entries))
        self.assertTrue(any('src/BranchBooking.Worker' in e['args'] for e in entries))
        self.assertTrue(all(e['channel'] == 'Simulated' for e in entries))
        self.stop(process)
        self.assert_stopped()

    def test_mailpit_failure_stops_other_services(self):
        self.env['LAUNCHER_TEST_MAILPIT_FAIL'] = '1'
        process = self.start('--mailpit')
        self.assertEqual(process.wait(timeout=8), 3)
        self.assert_stopped()

    def test_existing_smtp_configuration_is_preserved_without_mailpit_option(self):
        self.env.update(Notifications__Channel='Smtp', Notifications__Smtp__Host='existing-inbox')
        process = self.start()
        entries = self.wait_for_services(process, 3)
        self.assertTrue(all(e['channel'] == 'Smtp' and e['host'] == 'existing-inbox' for e in entries))
        self.stop(process)
        self.assert_stopped()

    def test_missing_mailpit_fails_before_starting_services(self):
        (self.root / 'bin/mailpit').unlink()
        self.assertEqual(self.start('--mailpit').wait(timeout=5), 1)
        self.assertEqual(self.entries(), [])

    def test_worker_needs_its_own_connection_when_only_api_has_user_secrets(self):
        self.env.pop('ConnectionStrings__BranchBooking')
        self.assertEqual(self.start().wait(timeout=5), 1)
        self.assertEqual(self.entries(), [])

    def test_unknown_option_fails_before_starting_services(self):
        self.assertEqual(self.start('--invalid').wait(timeout=5), 2)
        self.assertEqual(self.entries(), [])


if __name__ == '__main__':
    unittest.main()
