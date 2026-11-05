"""The checks run on the application they were lifted from, with that application's own configuration, and find nothing wrong.

Skipped where `mortgage-analytics` is not beside this repository (it is private, so CI cannot clone it); set MORTGAGE_ANALYTICS to its folder to run it elsewhere.
"""

import os
import pathlib
import unittest

from doc_checks import run

HERE = pathlib.Path(__file__).resolve().parent
CONFIG = HERE / 'examples' / 'mortgage-analytics.json'


def application():
    chosen = os.environ.get('MORTGAGE_ANALYTICS')
    return pathlib.Path(chosen) if chosen else HERE.parents[2] / 'mortgage-analytics'


@unittest.skipUnless(application().is_dir(), 'mortgage-analytics is not beside this repository')
class OnAnotherApplication(unittest.TestCase):
    def test_its_links_and_citations_hold(self):
        problems = run.refs.all_problems(application(), run.load_config(application(), CONFIG)['refs'])

        self.assertEqual(problems, [], 'the lifted check finds nothing in the repository it was lifted from')

    def test_its_map_is_current_and_every_folder_has_a_readme(self):
        config = run.load_config(application(), CONFIG)['map']

        problems = run.repo_map.map_problems(
            application(), config['named_folders'], [tuple(group) for group in config['groups']], config['command']
        )

        self.assertEqual(problems, [], 'MAP.md is what the folders say, built by the lifted code')


if __name__ == '__main__':
    unittest.main()
