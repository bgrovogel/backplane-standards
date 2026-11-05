"""The documentation's links and its file:line citations are held to the repository, each rule shown on a small example that breaks it."""

import pathlib
import tempfile
import unittest

from doc_checks import refs


def write(root, name, text):
    path = root / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')
    return path


class Anchors(unittest.TestCase):
    def test_follow_github_slugs_and_number_a_repeated_heading(self):
        found = refs.anchors_of('# Title\n## The `Big` idea?\n## Again\n## Again\n```\n# not a heading\n```\n')

        self.assertEqual(
            found, {'title', 'the-big-idea', 'again', 'again-1'}, 'GitHub drops punctuation, joins words with hyphens and numbers a repeat'
        )


class Links(unittest.TestCase):
    def problems(self, files, stay_inside=False):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder) / 'repo'
            paths = [write(root, name, text) for name, text in files.items()]
            write(pathlib.Path(folder), 'outside.md', '# Outside\n')
            return refs.link_problems([path for path in paths if path.suffix == '.md'], root, stay_inside)

    def test_a_good_link_and_a_good_anchor_pass(self):
        self.assertEqual(
            self.problems({'a.md': '[b](b.md) [h](b.md#heading) [self](#top)\n# Top\n', 'b.md': '# Heading\n'}),
            [],
            'a file, a heading and a heading of the same page all exist',
        )

    def test_a_missing_file_is_found(self):
        self.assertEqual(len(self.problems({'a.md': '[gone](nowhere.md)\n'})), 1, 'a link to nothing is broken')

    def test_a_missing_anchor_is_found(self):
        self.assertEqual(
            len(self.problems({'a.md': '[h](b.md#nope)\n', 'b.md': '# Heading\n'})), 1, 'a link to a heading that is not there is broken'
        )

    def test_a_link_inside_a_code_block_or_to_the_web_is_not_checked(self):
        self.assertEqual(
            self.problems({'a.md': '```\n[x](nowhere.md)\n```\n[web](https://example.com/x)\n'}),
            [],
            'sample code and other sites are not this check',
        )

    def test_the_line_of_the_problem_is_named(self):
        self.assertIn('a.md:3:', self.problems({'a.md': 'one\ntwo\n[gone](nowhere.md)\n'})[0], 'the report says where to look')

    def test_a_link_out_of_the_repository_is_found_when_asked_for(self):
        files = {'a.md': '[out](../outside.md)\n'}

        self.assertEqual(
            (self.problems(files), len(self.problems(files, stay_inside=True))),
            ([], 1),
            'a clone on another machine does not have the neighbour folder',
        )


class Citations(unittest.TestCase):
    def test_a_file_and_line_in_backticks_is_a_citation(self):
        self.assertEqual(
            refs.citations('See `src/A.cs:46` here.\n'), [('src/A.cs:46', 'src/A.cs', 46, 1)], 'a path, a colon and a line in backticks'
        )

    def test_a_line_word_belongs_to_the_last_file_named_on_the_same_line_whatever_its_case(self):
        found = refs.citations('`A.cs:1`, then `B.cs`, line 7. Line 9 too.\nLine 3 with no file.\n')

        self.assertEqual(
            [(written, number) for written, _, _, number in found],
            [('A.cs:1', 1), ('B.cs:7', 1), ('B.cs:9', 1)],
            'a bare line number cites the file before it',
        )

    def test_prose_that_is_not_a_citation_is_ignored(self):
        self.assertEqual(refs.citations('At 6:30 it ran `npm test` on line 4 of nothing.\n'), [], 'a time and a command are not citations')


class Resolving(unittest.TestCase):
    FILES = ['src/Proj.Api/Filters/V.cs', 'src/Proj.Api/Endpoints/E.cs', 'client/a/index.ts', 'client/b/index.ts', 'x/NotApi/Filters/V.cs']

    def test_a_path_matches_the_end_of_a_file_path(self):
        self.assertEqual(refs.resolve(self.FILES, 'Endpoints/E.cs'), ['src/Proj.Api/Endpoints/E.cs'], 'the end of a path is enough')

    def test_a_leading_elision_and_a_shortened_first_folder_match(self):
        self.assertEqual(refs.resolve(self.FILES, '.../Api/Filters/V.cs'), ['src/Proj.Api/Filters/V.cs'], 'Api is the end of Proj.Api')

    def test_a_name_that_is_in_two_places_is_ambiguous(self):
        self.assertEqual(len(refs.resolve(self.FILES, 'index.ts')), 2, 'a citation must name its file far enough to be one')

    def test_an_unknown_path_matches_nothing(self):
        self.assertEqual(refs.resolve(self.FILES, 'Missing.cs'), [], 'no such file')


class StaleCitations(unittest.TestCase):
    FILES = ['src/A.cs', 'x/index.ts', 'y/index.ts']
    LINES = {'src/A.cs': ['', 'var total = Sum(items);', 'return total;']}

    def problems(self, text, anchors):
        def read_line(name, number):
            lines = self.LINES[name]
            return lines[number - 1] if 0 < number <= len(lines) else None

        return refs.citation_problems(text, 'guide.md', self.FILES, read_line, anchors)

    def test_a_citation_that_still_shows_its_token_passes(self):
        self.assertEqual(self.problems('`A.cs:2`\n', {'A.cs:2': 'Sum('}), [], 'line 2 still shows Sum(')

    def test_a_line_that_moved_is_found(self):
        self.assertEqual(len(self.problems('`A.cs:3`\n', {'A.cs:3': 'Sum('})), 1, 'line 3 no longer shows what the guide cites it for')

    def test_a_blank_line_is_found_because_its_token_is_missing(self):
        self.assertEqual(len(self.problems('`A.cs:1`\n', {'A.cs:1': 'Sum('})), 1, 'a blank line shows nothing')

    def test_a_line_past_the_end_is_found(self):
        self.assertIn('no line 9', self.problems('`A.cs:9`\n', {'A.cs:9': 'x'})[0], 'the file is shorter than the citation')

    def test_a_citation_with_no_pinned_token_is_found(self):
        self.assertIn('not pinned', self.problems('`A.cs:2`\n', {})[0], 'every citation is pinned to what its line shows')

    def test_a_missing_or_ambiguous_file_is_found(self):
        self.assertEqual(len(self.problems('`Gone.cs:1` `index.ts:1`\n', {})), 2, 'one file is missing and one is in two places')

    def test_a_pin_no_guide_cites_any_more_is_found(self):
        self.assertEqual(
            len(refs.unused_anchors(['`A.cs:2`\n'], {'A.cs:2': 'Sum(', 'A.cs:5': 'old'})), 1, 'a pin nobody cites is dead weight'
        )


class ReadingALine(unittest.TestCase):
    def test_gives_the_line_or_nothing_past_either_end(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            write(root, 'f.txt', 'one\ntwo\n')
            read = refs.line_reader(root)

            self.assertEqual(
                [read('f.txt', 0), read('f.txt', 1), read('f.txt', 2), read('f.txt', 3)], [None, 'one', 'two', None], 'lines count from one'
            )


class WholeRepository(unittest.TestCase):
    def test_links_and_citations_of_a_small_repository_are_all_held(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            write(root, 'docs/guide.md', 'See `src/A.cs:2` and [the code](../src/A.cs).\n')
            write(root, 'src/A.cs', 'x\nvar total = Sum(items);\n')
            write(root, 'docs/citations.json', '{"src/A.cs:2": "Sum("}')

            problems = refs.all_problems(
                root,
                {'cited_guides': ['docs/guide.md'], 'citations': 'docs/citations.json'},
                files=['docs/guide.md', 'src/A.cs', 'docs/citations.json'],
            )

            self.assertEqual(problems, [], 'a repository whose guide cites a line that still shows its token has nothing to report')

    def test_a_repository_with_no_cited_guides_only_has_its_links_checked(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            write(root, 'a.md', '[gone](nowhere.md)\n')

            problems = refs.all_problems(root, {}, files=['a.md'])

            self.assertEqual(len(problems), 1, 'no citations file is needed when no guide is cited')


if __name__ == '__main__':
    unittest.main()
