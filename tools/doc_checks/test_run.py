"""The command line: which checks run, from which folder, with which configuration, and the exit code that makes it a gate."""

import contextlib
import io
import json
import pathlib
import re
import tempfile
import unittest

from doc_checks import run


def repository(files):
    root = pathlib.Path(tempfile.mkdtemp())
    for relative, text in files.items():
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')
    return root


def main(arguments):
    """The exit code and everything printed."""
    out = io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
        code = run.main(arguments)
    return code, out.getvalue()


class Configuration(unittest.TestCase):
    def test_a_repository_without_a_configuration_file_gets_the_defaults(self):
        self.assertEqual(run.load_config(repository({})), {}, 'every check has a default, so no file is needed to start')

    def test_the_file_in_the_root_is_read(self):
        root = repository({'doc-checks.json': json.dumps({'map': {'named_folders': ['ops']}})})

        self.assertEqual(run.load_config(root), {'map': {'named_folders': ['ops']}}, 'the repository says its own folders')

    def test_a_file_can_be_named_on_the_command_line(self):
        root = repository({})
        elsewhere = repository({'mine.json': '{"a": 1}'}) / 'mine.json'

        self.assertEqual(run.load_config(root, elsewhere), {'a': 1}, 'one configuration can serve a repository that has none of its own')


class Running(unittest.TestCase):
    def test_a_clean_repository_passes_every_fast_check(self):
        root = repository({'README.md': '# R\n\nA repo.\n'})
        run.repo_map.write_map(root, named_folders=[])

        code, printed = main(['--root', str(root), '--files', 'README.md,MAP.md'])

        self.assertEqual((code, 'ok' in printed.lower()), (0, True), 'nothing wrong, nothing printed but the good news')

    def test_a_broken_link_fails_the_run_and_names_it(self):
        root = repository({'README.md': '# R\n\n[gone](nowhere.md)\n'})
        run.repo_map.write_map(root, named_folders=[])

        code, printed = main(['--root', str(root), '--files', 'README.md,MAP.md', 'refs'])

        self.assertEqual((code, 'nowhere.md' in printed), (1, True), 'a gate exits 1 and says what is wrong')

    def test_only_the_named_checks_run(self):
        root = repository({'README.md': '# R\n\n[gone](nowhere.md)\n'})

        code, _ = main(['--root', str(root), '--files', 'README.md', 'primers'])

        self.assertEqual(code, 0, 'the link is not the primer check')

    def test_a_check_nobody_wrote_is_an_error(self):
        code, printed = main(['--root', str(repository({})), 'tea-leaves'])

        self.assertEqual((code, 'tea-leaves' in printed), (2, True), 'a typo must not pass quietly')

    def test_counts_need_their_configuration(self):
        code, printed = main(['--root', str(repository({})), 'counts'])

        self.assertEqual((code, 'counts' in printed), (2, True), 'asking for counts of a repository that says none is a mistake to report')


class ThisRepository(unittest.TestCase):
    def test_its_own_links_map_and_primers_hold(self):
        code, printed = main([])

        self.assertEqual(code, 0, 'this repository is held to the checks it ships:\n' + printed)

    def test_every_package_it_depends_on_has_a_primer_or_a_reason(self):
        config = run.load_config(run.HERE.parents[1])
        code, printed = main(['primers'])

        self.assertEqual(
            ('primers' in config, code), (True, 0), 'no tool without a primer, here too, and the check is switched on:\n' + printed
        )

    def test_the_map_names_the_folders_the_csharp_readme_rule_requires(self):
        source = (run.HERE.parents[1] / 'tests' / 'Platform.Conventions.Tests' / 'Readme.cs').read_text(encoding='utf-8')
        block = re.search(r'NamedFolders\s*=>\s*\[(.*?)\];', source, re.S).group(1)

        self.assertEqual(
            sorted(re.findall(r'"([^"]+)"', block)),
            sorted(run.load_config(run.HERE.parents[1])['map']['named_folders']),
            'the map and the C# README rule name the same folders',
        )


if __name__ == '__main__':
    unittest.main()
