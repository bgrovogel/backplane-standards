"""The test-count check, each rule shown on a small document that breaks it."""

import json
import unittest

from doc_checks import counts

COUNTS = counts.with_sums(
    {'domain': 353, 'application': 206, 'jest': 474, 'e2e': 84},
    {'dotnet': ['domain', 'application'], 'total': ['dotnet', 'jest', 'e2e']},
)


class Sums(unittest.TestCase):
    def test_a_sum_can_add_an_earlier_sum(self):
        self.assertEqual(
            (COUNTS['dotnet'], COUNTS['total']),
            (559, 1117),
            'two .NET projects make the .NET total; .NET, Jest and Playwright make the total',
        )


class Checking(unittest.TestCase):
    def test_docs_that_match_the_counts_pass(self):
        docs = {'README.md': 'Tests: 559 backend tests and 474 Jest tests.'}
        rules = [
            counts.Rule('README.md', r'([\d,]+) backend tests', 'dotnet'),
            counts.Rule('README.md', r'([\d,]+) Jest tests', 'jest'),
        ]

        self.assertEqual(counts.check(docs, rules, COUNTS), [], 'every count is right, so there is nothing to report')

    def test_a_wrong_count_is_reported_with_its_file_line_and_the_real_number(self):
        docs = {'README.md': 'Intro.\nTests: 558 backend tests.'}

        problems = counts.check(docs, [counts.Rule('README.md', r'([\d,]+) backend tests', 'dotnet')], COUNTS)

        self.assertEqual(
            problems,
            ['README.md:2: says 558, but there are 559 (dotnet)'],
            'the problem names where the stale number is and what it should be',
        )

    def test_about_means_the_total_to_the_nearest_ten(self):
        rule = [counts.Rule('a.md', r'About ([\d,]+) tests', 'total', 'about')]

        self.assertEqual(
            (counts.check({'a.md': 'About 1,120 tests'}, rule, COUNTS), len(counts.check({'a.md': 'About 1,170 tests'}, rule, COUNTS))),
            ([], 1),
            "1,117 tests is 'about 1,120'; 'about 1,170' has drifted",
        )

    def test_over_means_the_total_rounded_down_to_the_hundred(self):
        rule = [counts.Rule('a.md', r'over ([\d,]+) tests', 'total', 'over')]

        self.assertEqual(
            (counts.check({'a.md': 'over 1,100 tests'}, rule, COUNTS), len(counts.check({'a.md': 'over 1,000 tests'}, rule, COUNTS))),
            ([], 1),
            "1,117 tests is 'over 1,100'; 'over 1,000' is true but stale",
        )

    def test_a_rule_that_matches_nothing_is_reported(self):
        rule = counts.Rule('README.md', r'([\d,]+) backend tests', 'dotnet')

        problems = counts.check({'README.md': 'Reworded: lots of backend tests.'}, [rule], COUNTS)

        self.assertEqual(
            problems,
            ["README.md: no longer says '([\\d,]+) backend tests' (dotnet); update the rules to the new wording"],
            'a rewording must not quietly switch its check off',
        )

    def test_a_count_of_tests_that_no_rule_checks_is_reported(self):
        problems = counts.check({'README.md': 'Line one.\nThe 41 smoke tests run first.'}, [], COUNTS)

        self.assertEqual(
            problems,
            ["README.md:2: '41 smoke tests' is a test count no rule checks; add one to the rules"],
            'a new count in the docs cannot go unchecked',
        )


class Parsing(unittest.TestCase):
    def test_counts_the_tests_dotnet_lists(self):
        listing = (
            'Test run for C:\\x\\Domain.Tests.dll (.NETCoreApp,Version=v10.0)\n'
            'The following Tests are available:\n'
            '    FirstTest\n'
            '    SecondTest(1,2)\n'
            '    SecondTest(3,4)\n'
        )

        self.assertEqual(counts.parse_dotnet_listing(listing), 3, 'each listed test case counts, parameterised ones included')

    def test_reads_the_total_playwright_lists(self):
        self.assertEqual(
            counts.parse_playwright_listing('  [chromium] › a.spec.ts:1:1 › x\nTotal: 84 tests in 10 files\n'),
            84,
            "the total comes from Playwright's own summary line",
        )

    def test_reads_a_single_playwright_test(self):
        self.assertEqual(counts.parse_playwright_listing('Total: 1 test in 1 file\n'), 1, 'the singular form counts too')

    def test_reads_the_total_jest_reports(self):
        self.assertEqual(counts.parse_jest_json(json.dumps({'numTotalTests': 474})), 474, "the total comes from Jest's own report")


class Gathering(unittest.TestCase):
    CONFIG = {
        'sources': {
            'domain': {'parser': 'dotnet-list', 'command': ['dotnet', 'test', 'D', '--list-tests']},
            'jest': {'parser': 'jest-json', 'command': ['npx', 'jest'], 'cwd': 'client'},
        },
        'sums': {'total': ['domain', 'jest']},
    }

    def test_each_source_is_run_where_it_says_and_read_by_its_parser(self):
        ran = []

        def run(command, cwd):
            ran.append((command[0], cwd))
            return 'The following Tests are available:\n    A\n    B\n' if command[0] == 'dotnet' else json.dumps({'numTotalTests': 5})

        found = counts.gather(self.CONFIG, run)

        self.assertEqual(
            (found, ran),
            ({'domain': 2, 'jest': 5, 'total': 7}, [('dotnet', '.'), ('npx', 'client')]),
            'each tool is asked from its own folder and the sum is added',
        )

    def test_a_parser_nobody_wrote_is_an_error(self):
        config = {'sources': {'x': {'parser': 'tea-leaves', 'command': ['x']}}}

        with self.assertRaises(ValueError, msg='a typo in the configuration must not count zero tests'):
            counts.gather(config, lambda command, cwd: '')


class Rules(unittest.TestCase):
    def test_rules_come_from_the_configuration(self):
        found = counts.rules_of(
            {
                'rules': [
                    {'file': 'README.md', 'pattern': r'(\d+) tests', 'count': 'total'},
                    {'file': 'a.md', 'pattern': r'(\d+)', 'count': 'x', 'kind': 'over'},
                ]
            }
        )

        self.assertEqual(
            found,
            [counts.Rule('README.md', r'(\d+) tests', 'total', 'exact'), counts.Rule('a.md', r'(\d+)', 'x', 'over')],
            'a rule is exact unless its configuration says otherwise',
        )


if __name__ == '__main__':
    unittest.main()
