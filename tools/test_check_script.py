"""The check script (scripts/check.ps1) runs the gates this repository's CI runs, one command per tier.

PowerShell is not run here. What can drift is the list itself: a step that calls an npm script that was renamed, a path that moved, or a tier that quietly lost
a gate. So this test reads the script and holds each step to the repository: every `npm run` names a script in package.json, every path exists, each tier still
has the gates that define it, and everything CI runs is in the script too, so a green `check full` means a green CI.
"""

import json
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / 'scripts' / 'check.ps1'
CI = ROOT / '.github' / 'workflows' / 'ci.yml'
STEP = re.compile(r"Tier\s*=\s*'(?P<tier>\w+)';\s*Name\s*=\s*'(?P<name>[^']+)';\s*Dir\s*=\s*'(?P<dir>[^']+)';\s*Cmd\s*=\s*'(?P<cmd>[^']+)'")
REQUIRED = {
    'fast': [
        'format:check',
        'board.py check',
        'doc_checks/run.py',
        'unittest discover',
        'ruff check',
        'ruff format',
        'Conventions.Tests',
        'gitleaks',
    ],
    'full': ['-warnaserror', 'dotnet test Platform.sln'],
    'release': ['gitleaks git --redact', 'dependency_report.py'],
}
CI_GATES = [
    'npm run format:check',
    'python tools/doc_checks/run.py',
    'ruff check tools',
    'ruff format --check tools',
    'python -m unittest discover tools',
    'dotnet build Platform.sln',
    'dotnet test Platform.sln',
]


def steps():
    return [match.groupdict() for match in STEP.finditer(SCRIPT.read_text(encoding='utf-8'))]


class TheCheckScript(unittest.TestCase):
    def test_it_exists_and_takes_a_tier(self):
        self.assertTrue(SCRIPT.exists(), f'{SCRIPT} is missing: it is the one command per tier')
        text = SCRIPT.read_text(encoding='utf-8')
        for tier in ('fast', 'full', 'release'):
            self.assertIn(f"'{tier}'", text, f'the {tier} tier is a parameter value')

    def test_every_tier_has_its_defining_checks(self):
        found = steps()
        for tier, needles in REQUIRED.items():
            commands = ' | '.join(step['cmd'] for step in found if step['tier'] == tier)
            missing = [needle for needle in needles if needle not in commands]
            self.assertEqual(missing, [], f'the {tier} tier lost a defining check')

    def test_every_npm_script_it_runs_exists(self):
        known = json.loads((ROOT / 'package.json').read_text(encoding='utf-8'))['scripts']
        for step in steps():
            for script in re.findall(r'npm run ([\w:.-]+)', step['cmd']):
                self.assertIn(script, known, f'{step["name"]}: npm run {script} is not a script in package.json')

    def test_every_path_it_names_exists(self):
        for step in steps():
            self.assertTrue((ROOT / step['dir']).is_dir(), f'{step["name"]}: the folder {step["dir"]} does not exist')
            for path in re.findall(r'\b((?:tools|tests|src|scripts)/[\w./-]+|Platform\.sln)', step['cmd']):
                self.assertTrue((ROOT / path.rstrip('/')).exists(), f'{step["name"]}: {path} does not exist')

    def test_a_missing_tool_is_reported_not_silently_skipped(self):
        text = SCRIPT.read_text(encoding='utf-8')
        self.assertIn('SKIPPED', text, 'a check whose tool is not installed says so in the summary')
        self.assertIn('exit 1', text, 'a failed step makes the script fail')

    def test_everything_ci_runs_is_in_the_script_and_the_script_adds_nothing_unknown_to_ci(self):
        in_script = ' | '.join(step['cmd'] for step in steps())
        in_ci = [
            run.replace('python3 ', 'python ').replace('pipx run --spec ruff==0.14.0 ', '')
            for run in re.findall(r'- run: (.+)', CI.read_text(encoding='utf-8'))
        ]
        in_ci = [run for run in in_ci if run != 'npm ci']

        self.assertEqual(
            (
                [gate for gate in CI_GATES if gate not in in_script],
                [run for run in in_ci if not any(run.startswith(gate) for gate in CI_GATES)],
            ),
            ([], []),
            'CI runs a gate that `check full` does not (so a green script would not mean a green CI), or CI runs one this test does not know',
        )


if __name__ == '__main__':
    unittest.main()
