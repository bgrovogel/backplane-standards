"""The repository map: what it says about a folder, and that a committed MAP.md is the current one."""

import pathlib
import tempfile
import unittest

from doc_checks import repo_map


def make_tree(files):
    """A temporary repository with the given {relative path: text} files; returns its root."""
    root = pathlib.Path(tempfile.mkdtemp())
    for relative, text in files.items():
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')
    return root


class WhatAFolderIs(unittest.TestCase):
    def test_it_is_the_first_sentence_under_the_heading(self):
        text = '# Domain\n\nThe business rules. Plain C# with no packages.\n\n- **Lives here:** the rules.\n'

        self.assertEqual(repo_map.first_sentence(text), 'The business rules.', 'one sentence is enough for a map')

    def test_a_sentence_can_run_over_several_lines(self):
        text = '# Tools\n\nChecks that keep the app\nand its documentation honest. Each is independent.\n'

        self.assertEqual(
            repo_map.first_sentence(text), 'Checks that keep the app and its documentation honest.', 'a wrapped line is one sentence'
        )

    def test_a_quote_marker_bold_and_links_are_dropped(self):
        text = '# Guide\n\n> **How the code is built.** The [main README](../README.md) is the front page.\n'

        self.assertEqual(repo_map.first_sentence(text), 'How the code is built.', 'the marks are formatting, not words')

    def test_a_link_inside_the_sentence_keeps_its_words(self):
        text = '# Guide\n\nRead the [main README](../README.md) first. Then the rest.\n'

        self.assertEqual(repo_map.first_sentence(text), 'Read the main README first.', 'the link text stays, the target goes')

    def test_a_readme_with_only_a_heading_says_nothing(self):
        self.assertEqual(repo_map.first_sentence('# Empty\n'), '', 'no text, no sentence')


class TheMap(unittest.TestCase):
    def test_a_project_folder_with_a_readme_is_listed_with_its_sentence(self):
        root = make_tree({'src/App.Domain/App.Domain.csproj': '<Project />', 'src/App.Domain/README.md': '# Domain\n\nThe rules. More.\n'})

        text = repo_map.build_map(root, named_folders=[])

        self.assertIn('| `src/App.Domain` | The rules. |', text, 'the folder and its first sentence appear in a table row')

    def test_a_folder_without_a_readme_is_flagged_rather_than_left_out(self):
        root = make_tree({'tests/App.Tests/App.Tests.csproj': '<Project />'})

        text = repo_map.build_map(root, named_folders=[])

        self.assertIn('| `tests/App.Tests` | (no README) |', text, 'a missing README shows in the map')

    def test_build_and_package_folders_are_ignored(self):
        root = make_tree(
            {
                'src/App/bin/Other.csproj': '<Project />',
                'src/App/obj/Other.csproj': '<Project />',
                'src/App/node_modules/x/Y.csproj': '<Project />',
            }
        )

        text = repo_map.build_map(root, named_folders=[])

        self.assertEqual(
            (' bin' in text, 'node_modules' in text), (False, False), 'a build output folder and a package folder are not projects'
        )

    def test_named_folders_are_listed_even_without_a_project_file(self):
        root = make_tree({'deploy/README.md': '# deploy\n\nRunning the app on a server. More.\n'})

        text = repo_map.build_map(root, named_folders=['deploy'])

        self.assertIn('| `deploy` | Running the app on a server. |', text, 'a named folder is in the map')

    def test_groups_come_from_the_configuration_and_the_rest_go_last(self):
        root = make_tree({'src/A/A.csproj': '<Project />', 'src/A/README.md': '# A\n\nA lib.\n', 'ops/README.md': '# ops\n\nRuns it.\n'})

        text = repo_map.build_map(root, named_folders=['ops'], groups=[('src', 'The libraries')])

        self.assertEqual(
            [line for line in text.splitlines() if line.startswith('## ')],
            ['## The libraries', '## Operations and documentation'],
            'a folder in no group is listed under the last heading',
        )

    def test_the_same_tree_gives_the_same_map(self):
        root = make_tree({'src/App/App.csproj': '<Project />', 'src/App/README.md': '# App\n\nThe app. Yes.\n'})

        self.assertEqual(repo_map.build_map(root, named_folders=[]), repo_map.build_map(root, named_folders=[]), 'the map is deterministic')

    def test_the_map_names_the_command_that_rebuilds_it(self):
        text = repo_map.build_map(make_tree({}), named_folders=[], command='python tools/doc_checks/run.py map')

        self.assertIn('`python tools/doc_checks/run.py map`', text, 'a reader of the generated file is told how to change it')


class Checking(unittest.TestCase):
    def test_a_current_map_passes_and_a_stale_one_is_reported_with_the_command(self):
        root = make_tree({'src/App/App.csproj': '<Project />', 'src/App/README.md': '# App\n\nThe app. Yes.\n'})
        repo_map.write_map(root, named_folders=[])
        current = repo_map.map_problems(root, named_folders=[])
        (root / 'src/App/README.md').write_text('# App\n\nNow it does something else.\n', encoding='utf-8')
        stale = repo_map.map_problems(root, named_folders=[])

        self.assertEqual(
            (current, len(stale), 'run.py map' in stale[0]), ([], 1, True), 'drift is found and the message says how to fix it'
        )

    def test_a_missing_map_is_stale_too(self):
        root = make_tree({'README.md': '# App\n'})

        self.assertEqual(len(repo_map.map_problems(root, named_folders=[])), 1, 'a repository must have its map')

    def test_a_folder_with_no_readme_is_a_problem(self):
        root = make_tree({'src/App/App.csproj': '<Project />'})
        repo_map.write_map(root, named_folders=[])

        problems = repo_map.map_problems(root, named_folders=[])

        self.assertEqual(problems, ['src/App has no README.md: write its five lines'], 'the map shows the gap and the check fails it')


if __name__ == '__main__':
    unittest.main()
