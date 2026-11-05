"""No tool without a primer: every dependency is covered by a primer or exempt for a stated reason, and nothing in the configuration is stale."""

import json
import unittest

from doc_checks import primers

PROPS = """<Project><ItemGroup>
  <PackageVersion Include="NUnit" Version="5.0.0" />
  <PackageVersion Include="NUnit.Analyzers" Version="4.15.0" />
  <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />
</ItemGroup></Project>"""

PACKAGE_JSON = json.dumps({'dependencies': {'rxjs': '7.8.0'}, 'devDependencies': {'prettier': '3.6.2'}})

INDEX = '| [NUnit](nunit.md) | NUnit |\n'


class Dependencies(unittest.TestCase):
    def test_nuget_packages_are_read_from_the_central_list(self):
        self.assertEqual(
            primers.nuget_names(PROPS), ['NUnit', 'NUnit.Analyzers', 'Microsoft.NET.Test.Sdk'], 'one name for each PackageVersion'
        )

    def test_npm_packages_are_read_from_both_sections(self):
        self.assertEqual(
            sorted(primers.npm_names(PACKAGE_JSON)), ['prettier', 'rxjs'], 'a tool that only the build uses still needs its primer'
        )

    def test_a_project_with_no_central_list_has_no_packages(self):
        self.assertEqual(primers.nuget_names('<Project />'), [], 'nothing adopted, nothing to explain')


class Coverage(unittest.TestCase):
    CONFIG = {
        'index': 'docs/primers/README.md',
        'covers': {'docs/primers/nunit.md': ['NUnit', 'NUnit.*']},
        'exempt': {'Microsoft.NET.Test.Sdk': 'the host that runs the tests; NUnit is the tool'},
    }
    NAMES = ['NUnit', 'NUnit.Analyzers', 'Microsoft.NET.Test.Sdk']

    def problems(self, names=None, config=None, exists=lambda path: True, index=INDEX):
        return primers.problems(names or self.NAMES, config or self.CONFIG, exists, index)

    def test_covered_and_exempt_dependencies_pass(self):
        self.assertEqual(self.problems(), [], 'every package has a primer or a reason')

    def test_a_dependency_with_no_primer_is_found_and_the_message_says_what_to_do(self):
        found = self.problems(names=[*self.NAMES, 'Serilog'])

        self.assertEqual(
            found,
            ['Serilog has no primer: write one, list it in "covers" and in the primer index, or exempt it with a reason'],
            'a new package cannot arrive without its primer',
        )

    def test_an_exemption_needs_a_reason(self):
        config = {**self.CONFIG, 'exempt': {'Microsoft.NET.Test.Sdk': ''}}

        self.assertEqual(len(self.problems(config=config)), 1, 'an exemption with no reason is a hole')

    def test_a_primer_that_is_not_there_is_found(self):
        found = self.problems(exists=lambda path: False)

        self.assertEqual(
            found,
            ['docs/primers/nunit.md is named as the primer for NUnit, NUnit.* but is not there'],
            'the configuration cannot point at nothing',
        )

    def test_a_primer_missing_from_the_index_is_found(self):
        found = self.problems(index='| [Other](other.md) | Other |\n')

        self.assertEqual(found, ['docs/primers/nunit.md is not listed in docs/primers/README.md'], 'a primer nobody can find is not read')

    def test_a_pattern_that_no_dependency_matches_is_stale(self):
        found = self.problems(names=['NUnit', 'Microsoft.NET.Test.Sdk'])

        self.assertEqual(
            found, ['"NUnit.*" in docs/primers/nunit.md covers no dependency; delete it'], 'a removed package takes its entry with it'
        )

    def test_an_exemption_for_a_package_that_is_gone_is_stale(self):
        found = self.problems(names=['NUnit', 'NUnit.Analyzers'])

        self.assertEqual(
            found,
            ['The exemption for Microsoft.NET.Test.Sdk is for a package that is not a dependency; delete it'],
            'exemptions do not outlive the packages',
        )


class FromAFolder(unittest.TestCase):
    def test_dependency_files_that_exist_are_read_and_the_rest_are_skipped(self):
        files = {'Directory.Packages.props': PROPS, 'package.json': PACKAGE_JSON}

        names = primers.dependencies(['Directory.Packages.props', 'package.json', 'other/package.json'], files.get)

        self.assertEqual(
            sorted(names),
            ['Microsoft.NET.Test.Sdk', 'NUnit', 'NUnit.Analyzers', 'prettier', 'rxjs'],
            'both kinds are read; a file that is not there is not an error',
        )


SOURCE = '[the docs](https://example.com/docs)'
GOOD = '# Tool\n\n> One sentence.\n\n**Read time:** 5 min · **Last verified:** 2026-11-05\n\n' + ''.join(
    f'## {heading}\n\n{SOURCE if heading == "Further reading" else "text"}\n\n' for heading in primers.SECTIONS
)


class Template(unittest.TestCase):
    def test_a_primer_with_every_section_and_its_header_passes(self):
        self.assertEqual(primers.template_problems('x.md', GOOD), [], 'the template is followed')

    def test_a_missing_section_is_named(self):
        text = GOOD.replace('## What goes wrong\n', '## Troubles\n')

        self.assertEqual(
            primers.template_problems('x.md', text),
            ["x.md: no section 'What goes wrong'"],
            'the reader knows where to look because every primer has the same sections',
        )

    def test_sections_out_of_order_are_found(self):
        text = GOOD.replace('## Terms', '## Early').replace('## Why we use it', '## Terms').replace('## Early', '## Why we use it')

        self.assertEqual(
            len(primers.template_problems('x.md', text)), 2, 'a section in the wrong place is as hard to find as a missing one'
        )

    def test_the_read_time_and_the_verified_date_are_required(self):
        text = GOOD.replace('**Read time:** 5 min · **Last verified:** 2026-11-05', '')

        self.assertEqual(
            len(primers.template_problems('x.md', text)), 2, 'a primer says how long it takes and when it was last checked against the tool'
        )

    def test_a_primer_names_its_sources_under_further_reading(self):
        text = GOOD.replace(SOURCE, 'text')

        self.assertEqual(
            primers.template_problems('x.md', text),
            ["x.md: 'Further reading' names no source"],
            'a primer with no sources cannot be checked',
        )

    def test_the_skipped_files_are_not_held_to_it(self):
        found = primers.folder_problems({'a.md': GOOD, 'README.md': '# Index\n', 'b.md': '# Bad\n'}, skip=['README.md'])

        self.assertEqual(sorted({line.split(':')[0] for line in found}), ['b.md'], 'the index and the map are not primers')


if __name__ == '__main__':
    unittest.main()
