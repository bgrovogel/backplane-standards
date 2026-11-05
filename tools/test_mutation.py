"""The mutation runner: how a project's Stryker configuration is made from the shared base, how a report becomes a score, and how the score is recorded."""

import json
import pathlib
import tempfile
import unittest

import mutation

CONFIG = {
    'base': {
        'target-framework': 'net10.0',
        'reporters': ['progress', 'json'],
        'thresholds': {'high': 95, 'low': 90, 'break': 90},
        'concurrency': 2,
    },
    'projects': {
        'Lib': {'tests': 'tests/Lib.Tests', 'project': 'Lib.csproj', 'thresholds': {'break': 93}},
        'Plain': {'tests': 'tests/Plain.Tests', 'project': 'Plain.csproj'},
    },
}


def report(*statuses):
    """What Stryker's JSON report holds for one file whose mutants ended with the given statuses."""
    return json.dumps(
        {
            'schemaVersion': '2',
            'files': {'src/A.cs': {'mutants': [{'id': str(index), 'status': status} for index, status in enumerate(statuses)]}},
        }
    )


class Configuration(unittest.TestCase):
    def test_a_project_gets_the_base_and_its_own_project_file(self):
        found = mutation.stryker_config(CONFIG, 'Plain')['stryker-config']

        self.assertEqual(
            (found['project'], found['target-framework'], found['thresholds']['break']),
            ('Plain.csproj', 'net10.0', 90),
            'the base applies unless the project says otherwise',
        )

    def test_a_project_can_raise_a_threshold_without_losing_the_others(self):
        found = mutation.stryker_config(CONFIG, 'Lib')['stryker-config']['thresholds']

        self.assertEqual(found, {'high': 95, 'low': 90, 'break': 93}, 'only the threshold it names changes')

    def test_the_base_itself_is_not_changed_by_asking(self):
        mutation.stryker_config(CONFIG, 'Lib')

        self.assertEqual(CONFIG['base']['thresholds']['break'], 90, 'the next project starts from the same base')

    def test_a_project_nobody_listed_is_an_error_that_names_the_choices(self):
        with self.assertRaises(KeyError, msg='a typo must not run nothing'):
            mutation.stryker_config(CONFIG, 'Lbi')


class Scores(unittest.TestCase):
    def test_caught_mutants_over_caught_and_missed(self):
        found = mutation.score_of(report('Killed', 'Killed', 'Timeout', 'Survived'))

        self.assertEqual((found.killed, found.survived, found.score), (3, 1, 75.0), 'a timeout is a catch; a survivor is a miss')

    def test_a_mutant_no_test_reaches_is_a_miss(self):
        self.assertEqual(mutation.score_of(report('Killed', 'NoCoverage')).score, 50.0, 'untested code is unprotected code')

    def test_mutants_that_do_not_compile_or_are_ignored_are_not_counted(self):
        found = mutation.score_of(report('Killed', 'CompileError', 'Ignored', 'RuntimeError'))

        self.assertEqual((found.total, found.score), (1, 100.0), 'only mutants the tests could have caught count')

    def test_a_report_with_no_countable_mutants_has_no_score(self):
        self.assertIsNone(mutation.score_of(report('CompileError')).score, 'nothing to measure is not 100 percent')


class Reports(unittest.TestCase):
    def test_the_newest_report_of_a_test_project_is_the_one_used(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            for stamp in ('2026-11-05.10-00-00', '2026-11-05.11-30-00', '2026-11-04.23-59-59'):
                path = root / 'StrykerOutput' / stamp / 'reports' / 'mutation-report.json'
                path.parent.mkdir(parents=True)
                path.write_text('{}', encoding='utf-8')

            self.assertIn(
                '2026-11-05.11-30-00',
                mutation.latest_report(root).as_posix(),
                'Stryker names a run by its time, so the last name is the last run',
            )

    def test_no_run_yet_is_an_error_that_says_so(self):
        with (
            tempfile.TemporaryDirectory() as folder,
            self.assertRaises(FileNotFoundError, msg='scoring a run that never happened must fail'),
        ):
            mutation.latest_report(pathlib.Path(folder))


class AfterARun(unittest.TestCase):
    def reports(self, root, *stamps):
        for stamp in stamps:
            path = root / 'StrykerOutput' / stamp / 'reports' / 'mutation-report.json'
            path.parent.mkdir(parents=True)
            path.write_text('{}', encoding='utf-8')

    def test_a_run_that_wrote_no_new_report_has_nothing_to_score(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            self.reports(root, '2026-11-05.10-00-00')
            before = mutation.reports_in(root)

            self.assertIsNone(mutation.new_report(root, before), 'a failed run must not be scored by the run before it')

    def test_a_run_that_wrote_a_report_is_scored_from_it(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            self.reports(root, '2026-11-05.10-00-00')
            before = mutation.reports_in(root)
            self.reports(root, '2026-11-05.11-00-00')

            self.assertIn('11-00-00', mutation.new_report(root, before).as_posix(), 'the report the run just wrote')


class Recording(unittest.TestCase):
    TABLE = (
        '# Mutation scores\n\nIntro.\n\n'
        '| Project | Score | Killed | Survived | Break | Measured | At |\n'
        '| ------- | ----- | ------ | -------- | ----- | -------- | -- |\n'
        '| Old | 90.0 | 9 | 1 | 85 | 2026-11-01 | abc1234 |\n'
    )

    def test_a_new_project_gets_a_row(self):
        text = mutation.record(self.TABLE, 'New', mutation.score_of(report('Killed', 'Survived')), 90, '2026-11-05', 'def5678')

        self.assertIn(
            '| New | 50.0 | 1 | 1 | 90 | 2026-11-05 | def5678 |', text, 'the row says what was measured, against which break, and when'
        )

    def test_a_measured_project_has_its_row_replaced_not_repeated(self):
        text = mutation.record(self.TABLE, 'Old', mutation.score_of(report('Killed')), 85, '2026-11-05', 'def5678')

        self.assertEqual(
            (text.count('| Old |'), '| Old | 100.0 | 1 | 0 | 85 | 2026-11-05 | def5678 |' in text),
            (1, True),
            'a project has one row, the latest',
        )


class Running(unittest.TestCase):
    def test_the_command_runs_stryker_from_the_test_project_with_the_made_config(self):
        command, folder = mutation.command_for(CONFIG, 'Lib', pathlib.Path('/repo'), pathlib.Path('/tmp/lib.json'))

        self.assertEqual(
            (command, folder),
            (['dotnet', 'stryker', '--config-file', str(pathlib.Path('/tmp/lib.json'))], pathlib.Path('/repo/tests/Lib.Tests')),
            'Stryker finds the tests and the project from where it is run',
        )


if __name__ == '__main__':
    unittest.main()
