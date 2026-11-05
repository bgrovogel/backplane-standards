"""The reusable CI workflow (.github/workflows/dotnet-angular-ci.yml) is held to the rules it exists to spread.

A workflow cannot be run from here, so what is checked is what can silently drift: every action pinned to a commit, the permissions read-only, a
caller's input never pasted into a shell, every gate still present, and every input described. The file is read as text (no YAML library is needed
for these rules, and CI's Python has none to rely on).
"""

import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / '.github' / 'workflows' / 'dotnet-angular-ci.yml'
DELIVERY_DOC = ROOT / 'docs' / '09-repositories-and-delivery.md'

# Each gate the workflow runs, as a fragment of the command that runs it, and the job it belongs in.
GATES = {
    'backend': [
        'dotnet restore',
        '--locked-mode',
        'dotnet build',
        '-warnaserror',
        '--vulnerable --include-transitive',
        'dotnet test',
        'XPlat Code Coverage',
        'reportgenerator',
        'linecoverage',
    ],
    'frontend': [
        'npm ci',
        'npm audit --omit=dev',
        'npm run lint',
        'npm run format:check',
        'npm run lint:styles',
        'npm run typecheck',
        'npm test -- --ci',
    ],
    'e2e': ['npm run "$E2E_INSTALL_SCRIPT"', 'npm run "$E2E_SCRIPT"'],
}


def lines():
    return WORKFLOW.read_text(encoding='utf-8').splitlines()


def indentation(line):
    return len(line) - len(line.lstrip(' '))


def shell_lines():
    """The lines of every `run:` step, which are the ones a shell reads."""
    found = []
    all_lines = lines()
    for number, line in enumerate(all_lines):
        match = re.match(r'(\s*)(?:- )?run:\s*(.*)$', line)
        if not match:
            continue
        if match.group(2) in ('|', '>', '|-', '>-'):
            base = indentation(line)
            for later in all_lines[number + 1 :]:
                if later.strip() and indentation(later) <= base:
                    break
                found.append(later)
        else:
            found.append(match.group(2))
    return found


def jobs():
    """Each job's name and its lines, by the two-space-indented keys under `jobs:`."""
    text = WORKFLOW.read_text(encoding='utf-8')
    body = text.split('\njobs:\n', 1)[1]
    parts = re.split(r'\n(?=  [a-z][\w-]*:\n)', '\n' + body)
    return {part.strip().split(':', 1)[0]: part for part in parts if part.strip()}


def input_blocks():
    """Each workflow_call input's name and its lines: the keys at six spaces under `inputs:`."""
    found = {}
    name = None
    inside = False
    for line in lines():
        if line == '    inputs:':
            inside = True
        elif inside and line.strip() and indentation(line) < 6:
            break
        elif inside and indentation(line) == 6 and line.strip().endswith(':'):
            name = line.strip()[:-1]
            found[name] = ''
        elif inside and name:
            found[name] += line + '\n'
    return found


class TheReusableWorkflow(unittest.TestCase):
    def test_it_exists(self):
        self.assertTrue(WORKFLOW.exists(), f'{WORKFLOW} is missing: it is the one pipeline every application calls')

    def test_it_is_only_called_never_triggered_by_itself(self):
        triggers = re.search(r'^on:\n((?:  .*\n)+)', WORKFLOW.read_text(encoding='utf-8'), re.MULTILINE)
        self.assertIsNotNone(triggers, 'the workflow names its trigger')
        self.assertEqual(
            re.findall(r'^  (\w+):', triggers.group(1), re.MULTILINE),
            ['workflow_call'],
            'the caller decides when it runs (and its path filters)',
        )

    def test_every_action_is_pinned_to_a_commit(self):
        uses = re.findall(r'uses:\s*(\S+)', WORKFLOW.read_text(encoding='utf-8'))
        self.assertGreater(len(uses), 3, 'the workflow uses actions')
        unpinned = [action for action in uses if not re.fullmatch(r'[\w./-]+@[0-9a-f]{40}', action)]
        self.assertEqual(unpinned, [], 'a tag can be moved by whoever owns the action; a commit cannot')

    def test_every_pinned_action_says_which_version_it_is(self):
        for line in lines():
            if re.search(r'uses:\s*\S+@[0-9a-f]{40}', line):
                self.assertRegex(line, r'# v\d+', f'a bare hash cannot be reviewed or updated: {line.strip()}')

    def test_the_permissions_are_read_only_once_and_never_widened(self):
        text = WORKFLOW.read_text(encoding='utf-8')
        self.assertEqual(re.findall(r'^\s*permissions:.*$', text, re.MULTILINE), ['permissions:'], 'one permissions block, at the top')
        self.assertRegex(text, r'(?m)^permissions:\n  contents: read\n(?!  )', 'it reads the repository and nothing else')

    def test_a_callers_input_is_never_pasted_into_a_shell(self):
        pasted = [line.strip() for line in shell_lines() if re.search(r'\$\{\{\s*(inputs|github\.(event|head_ref))', line)]
        self.assertEqual(pasted, [], 'pass it through env: and read "$NAME", so a value cannot become a command')

    def test_the_workflow_uses_no_secret(self):
        self.assertNotIn(
            'secrets.', WORKFLOW.read_text(encoding='utf-8'), 'a workflow that can read secrets can leak them; pass nothing in'
        )

    def test_every_gate_is_present_in_its_job(self):
        found = jobs()
        for job, gates in GATES.items():
            self.assertIn(job, found, f'the {job} job is missing')
            missing = [gate for gate in gates if gate not in found[job]]
            self.assertEqual(missing, [], f'the {job} job lost a gate')

    def test_the_optional_jobs_are_off_until_asked_for(self):
        found = jobs()
        for job in ('e2e', 'mutation', 'docker', 'repository'):
            self.assertIn(job, found, f'the {job} job is missing')
            self.assertRegex(found[job], r'\n    if: inputs\.[\w-]+ != \'\'', f'{job} runs only when its input is given')

    def test_every_input_is_described_and_has_a_default_unless_required(self):
        blocks = input_blocks()
        self.assertGreater(len(blocks), 5, 'the workflow takes inputs')
        for name, block in blocks.items():
            self.assertIn('description:', block, f'input {name} says what it is for')
            self.assertTrue('required: true' in block or 'default:' in block, f'input {name} has a default or is required')

    def test_the_solution_is_the_one_required_input(self):
        required = [name for name, block in input_blocks().items() if 'required: true' in block]
        self.assertEqual(required, ['dotnet-solution'], 'everything else has a default; only the solution cannot be guessed')


class TheCallerShownInTheDocumentation(unittest.TestCase):
    """The few lines an application writes are in docs/09; they are held to the workflow, so the example cannot call an input that is gone."""

    @staticmethod
    def example():
        text = DELIVERY_DOC.read_text(encoding='utf-8')
        block = text.split('# .github/workflows/ci.yml in an application', 1)[1].split('```', 1)[0]
        given = re.search(r'    with:\n((?:      .*\n)+)', block).group(1)
        return block, re.findall(r'^      ([\w-]+):', given, re.MULTILINE)

    def test_it_calls_this_workflow(self):
        block, _ = self.example()
        self.assertRegex(
            block,
            r'uses: [\w./-]+/backplane-standards/\.github/workflows/dotnet-angular-ci\.yml@\S+',
            'it names the file in .github/workflows',
        )

    def test_it_gives_the_required_input_and_only_inputs_that_exist(self):
        _, given = self.example()
        known = input_blocks()
        self.assertEqual([name for name in given if name not in known], [], 'an input the workflow does not take is an error, or ignored')
        self.assertIn('dotnet-solution', given, 'the one required input')

    def test_it_holds_the_minutes_policy_in_the_caller(self):
        block, _ = self.example()
        self.assertIn('cancel-in-progress: true', block, 'a newer push cancels the older run')
        self.assertIn('paths-ignore', block, 'a documentation-only change runs nothing')
        self.assertIn('permissions:\n  contents: read', block, 'the caller is read-only too')


if __name__ == '__main__':
    unittest.main()
