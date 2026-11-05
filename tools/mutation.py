"""Runs mutation testing (Stryker.NET) for chosen projects and records the score.

    python tools/mutation.py list                              the projects that can be mutated
    python tools/mutation.py run Backplane.Conventions          run Stryker on it, from its test project, and print the score
    python tools/mutation.py run Backplane.Conventions --record  ...and record the score in docs/mutation-scores.md (a clean working tree only)
    python tools/mutation.py score Backplane.Conventions        score the latest run without running again

Every project starts from the shared base in `tools/mutation.json` (reporters, target framework, thresholds, concurrency) and says only what is its own: the
project file, the tests that cover it, and a threshold it raises. The thresholds are the gate (G-22): Stryker exits non-zero below `break`. A run takes minutes,
so run one project at a time, in the foreground, with nothing else running: Stryker starts several test hosts at once.
"""

import argparse
import copy
import datetime
import json
import pathlib
import subprocess
import sys
import tempfile
from dataclasses import dataclass

ROOT = pathlib.Path(__file__).resolve().parents[1]
CONFIG = pathlib.Path(__file__).resolve().parent / 'mutation.json'
SCORES = ROOT / 'docs' / 'mutation-scores.md'
CAUGHT = {'Killed', 'Timeout'}
MISSED = {'Survived', 'NoCoverage'}


@dataclass(frozen=True)
class Score:
    killed: int
    survived: int

    @property
    def total(self):
        return self.killed + self.survived

    @property
    def score(self):
        """Percent of the countable mutants the tests caught; None when there were none to count."""
        return round(self.killed / self.total * 100, 1) if self.total else None


def stryker_config(config, name):
    """The Stryker configuration of one project: the shared base, then the project's own file and thresholds."""
    if name not in config['projects']:
        raise KeyError(f'no project called {name}; the projects are {", ".join(config["projects"])}')
    own = config['projects'][name]
    merged = copy.deepcopy(config['base'])
    merged['project'] = own['project']
    merged['thresholds'] = {**merged.get('thresholds', {}), **own.get('thresholds', {})}
    return {'stryker-config': merged}


def score_of(report_text):
    """The score in a Stryker JSON report. Mutants that did not compile, were ignored or crashed could not have been caught, so they are not counted."""
    statuses = [mutant['status'] for file in json.loads(report_text)['files'].values() for mutant in file['mutants']]
    return Score(sum(status in CAUGHT for status in statuses), sum(status in MISSED for status in statuses))


def reports_in(tests_folder):
    """The JSON reports of the runs in a test project, oldest first (Stryker names a run folder by its time)."""
    return sorted(pathlib.Path(tests_folder).glob('StrykerOutput/*/reports/mutation-report.json'), key=lambda path: path.parts[-3])


def latest_report(tests_folder):
    """The report of the newest run in a test project."""
    reports = reports_in(tests_folder)
    if not reports:
        raise FileNotFoundError(
            f'no mutation report under {tests_folder}/StrykerOutput: run `python tools/mutation.py run <project>` first'
        )
    return reports[-1]


def new_report(tests_folder, before):
    """The report a run just wrote, or None if it wrote none (a failed run must not be scored by the run before it)."""
    return next((report for report in reversed(reports_in(tests_folder)) if report not in before), None)


def record(text, name, score, break_at, measured, commit):
    """The scores document with this project's row added, or replaced if it has one (a project has one row: the latest)."""
    row = f'| {name} | {score.score} | {score.killed} | {score.survived} | {break_at} | {measured} | {commit} |'
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if line.startswith(f'| {name} |'):
            lines[index] = row
            break
    else:
        lines.append(row)
    return '\n'.join(lines) + '\n'


def command_for(config, name, root, config_file):
    """The command that runs Stryker for a project, and the folder to run it in: the project's test project, where Stryker finds both the tests and the code."""
    return ['dotnet', 'stryker', '--config-file', str(config_file)], pathlib.Path(root) / config['projects'][name]['tests']


def _git(*arguments):
    return subprocess.run(['git', *arguments], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()


def _scored(config, name, report, record_it):
    found = score_of(report.read_text(encoding='utf-8'))
    break_at = stryker_config(config, name)['stryker-config']['thresholds'].get('break', 0)
    print(f'{name}: score {found.score} ({found.killed} caught, {found.survived} missed; break at {break_at})')
    if record_it:
        if _git('status', '--porcelain'):
            sys.exit(
                'Not recording: the working tree has uncommitted changes, and a score of code nobody committed names no commit. Commit, then record.'
            )
        today = datetime.date.today().isoformat()
        SCORES.write_text(
            record(SCORES.read_text(encoding='utf-8'), name, found, break_at, today, _git('rev-parse', '--short=7', 'HEAD')),
            encoding='utf-8',
        )
        print(f'Recorded in {SCORES.relative_to(ROOT).as_posix()}')


def main(arguments):
    parser = argparse.ArgumentParser(description='Runs mutation testing for chosen projects.')
    parser.add_argument('action', choices=['list', 'run', 'score'])
    parser.add_argument('project', nargs='?')
    parser.add_argument('--record', action='store_true', help='write the score into docs/mutation-scores.md')
    options = parser.parse_args(arguments)
    config = json.loads(CONFIG.read_text(encoding='utf-8'))
    if options.action == 'list':
        print('\n'.join(config['projects']))
        return 0
    if options.project is None:
        print('Name a project; `list` shows them.', file=sys.stderr)
        return 2
    stryker_config(config, options.project)  # a project nobody listed fails here, naming the choices
    tests = ROOT / config['projects'][options.project]['tests']
    report, exit_code = None, 0
    if options.action == 'run':
        before = reports_in(tests)
        with tempfile.TemporaryDirectory() as folder:
            config_file = pathlib.Path(folder) / f'{options.project}.json'
            config_file.write_text(json.dumps(stryker_config(config, options.project), indent=2), encoding='utf-8')
            command, where = command_for(config, options.project, ROOT, config_file)
            exit_code = subprocess.run(command, cwd=where, check=False).returncode
        report = new_report(tests, before)
        if report is None:
            print(f'Stryker wrote no report (exit code {exit_code}): there is no score. Read its output above.', file=sys.stderr)
            return exit_code or 1
    else:
        report = latest_report(tests)
    _scored(config, options.project, report, options.record)
    return exit_code


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
