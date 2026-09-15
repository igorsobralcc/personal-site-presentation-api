"""Exercise the exact deployment shell with a deterministic disposable Docker fixture."""
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASH = 'C:/Program Files/Git/bin/bash.exe' if os.name == 'nt' else shutil.which('bash')

DOCKER = r'''#!/usr/bin/env bash
set -eu
echo "$*" >> "$MOCK_STATE/commands"
case "$1" in
  pull) exit 0 ;;
  rm)
    case "${@: -1}" in
      *-previous) rm -f "$MOCK_STATE/previous" ;;
      *) rm -f "$MOCK_STATE/new" ;;
    esac ;;
  container)
    case "${@: -1}" in
      *-previous) test -f "$MOCK_STATE/previous" ;;
      *) test -f "$MOCK_STATE/old" || test -f "$MOCK_STATE/new" ;;
    esac ;;
  stop)
    if [[ "$MOCK_SCENARIO" == 'interrupted' ]]; then kill -INT "$PPID"; fi
    exit 0 ;;
  rename)
    if [[ "$2" == *-previous ]]; then mv "$MOCK_STATE/previous" "$MOCK_STATE/old";
    else mv "$MOCK_STATE/old" "$MOCK_STATE/previous"; fi ;;
  start) test -f "$MOCK_STATE/old" ;;
  run)
    touch "$MOCK_STATE/new"
    test "$MOCK_SCENARIO" != 'startup-failure' ;;
  inspect)
    if [[ "$MOCK_SCENARIO" == 'unhealthy' ]]; then echo unhealthy; else echo healthy; fi ;;
  exec) test "$MOCK_SCENARIO" != 'readiness-failure' ;;
  *) echo "Unexpected Docker fixture call" >&2; exit 9 ;;
esac
'''


class DeploymentTests(unittest.TestCase):
    def run_deployment(self, scenario, tag=None):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / 'old').touch()
            docker = root / 'docker'
            docker.write_text(DOCKER, newline='\n')
            docker.chmod(0o755)
            sleeper = root / 'sleep'
            sleeper.write_text('#!/usr/bin/env bash\nexit 0\n', newline='\n')
            sleeper.chmod(0o755)
            sequencer = root / 'seq'
            sequencer.write_text('#!/usr/bin/env bash\nprintf "1\\n2\\n"\n', newline='\n')
            sequencer.chmod(0o755)
            source = (ROOT / '.github/workflows/deploy.yml').read_text(encoding='utf-8')
            command = source.split('      - name: Deploy verified image', 1)[1].split('        run: |\n', 1)[1]
            command = '\n'.join(line[10:] if line.startswith('          ') else line for line in command.splitlines())
            command = ('export PATH="$(cygpath -u "$MOCK_BIN"):$PATH"\n' if os.name == 'nt'
                       else 'export PATH="$MOCK_BIN:$PATH"\n') + command
            environment = dict(os.environ, MOCK_STATE=root.as_posix(), MOCK_BIN=root.as_posix(), MOCK_SCENARIO=scenario,
                               GITHUB_REPOSITORY='owner/presentation', GITHUB_SHA='a' * 40,
                               REQUESTED_TAG=tag or '', PATH=root.as_posix() + os.pathsep + os.environ['PATH'])
            result = subprocess.run([BASH, '-c', command], env=environment, capture_output=True, text=True, timeout=15)
            return result, (root / 'old').exists(), (root / 'previous').exists(), (root / 'new').exists(), (root / 'commands').read_text(encoding='utf-8') if (root / 'commands').exists() else ''

    def test_success_requires_readiness_and_removes_old_only_afterward(self):
        result, old, previous, new, commands = self.run_deployment('success')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertFalse(old)
        self.assertFalse(previous)
        self.assertTrue(new)
        self.assertIn('/health/ready', commands)
        self.assertLess(commands.index('exec '), commands.rindex('rm --force personal-site-presentation-api-previous'))

    def test_startup_health_readiness_and_interruption_failures_restore_previous(self):
        for scenario in ['startup-failure', 'unhealthy', 'readiness-failure', 'interrupted']:
            with self.subTest(scenario=scenario):
                result, old, previous, new, commands = self.run_deployment(scenario)
                self.assertNotEqual(0, result.returncode, result.stdout)
                self.assertTrue(old, commands + result.stderr)
                self.assertFalse(previous)
                self.assertFalse(new)
                self.assertIn('start personal-site-presentation-api', commands)

    def test_mutable_tag_is_rejected_before_touching_previous(self):
        result, old, previous, new, commands = self.run_deployment('success', 'latest')
        self.assertNotEqual(0, result.returncode)
        self.assertTrue(old)
        self.assertFalse(previous)
        self.assertFalse(new)
        self.assertEqual('', commands)


if __name__ == '__main__':
    unittest.main()
