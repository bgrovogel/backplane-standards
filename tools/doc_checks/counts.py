"""Checks that every test count in the README and docs/ matches the real number of tests.

The counts come from the test tools themselves, without running the slow suites: `dotnet test --list-tests`, `playwright test --list`, and a Jest run (Jest has
no list mode). What to run, and where a count appears in the documents, is the repository's `doc-checks.json` ("counts"). An exact count must match; "about N" must
be the total to the nearest ten, and "over N" the total rounded down to the hundred, so rounded wording stays true as the suites grow.

Two safety nets: a rule whose wording is no longer found fails (a rewrite cannot quietly switch its check off), and an "N ... tests" phrase that no rule checks
fails (a new count cannot go unchecked).
"""

import json
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import NamedTuple


class Rule(NamedTuple):
    """One place a count appears: the file, a pattern whose first group is the number, and which count it states."""

    file: str
    pattern: str
    count: str
    kind: str = 'exact'  # or "about" (nearest ten) or "over" (rounded down to the hundred)


# A number followed, within three words, by "test" or "tests": the shape of a test count anywhere in prose.
TEST_COUNT_PHRASE = re.compile(r'(?<![\w,.])(\d[\d,]*) (?:[A-Za-z.+/-]+ ){0,3}tests?\b')


def with_sums(counts: dict[str, int], sums: dict[str, list[str]]) -> dict[str, int]:
    """The counts with each named sum added, in order, so a sum can add an earlier one."""
    found = dict(counts)
    for name, parts in sums.items():
        found[name] = sum(found[part] for part in parts)
    return found


def rules_of(config: dict) -> list[Rule]:
    """The rules a repository's configuration lists."""
    return [Rule(rule['file'], rule['pattern'], rule['count'], rule.get('kind', 'exact')) for rule in config.get('rules', [])]


def _expected(rule: Rule, actual: int) -> int:
    if rule.kind == 'about':
        return round(actual, -1)
    if rule.kind == 'over':
        return actual // 100 * 100
    return actual


def _line_of(text: str, index: int) -> int:
    return text.count('\n', 0, index) + 1


def check(docs: dict[str, str], rules: list[Rule], counts: dict[str, int]) -> list[str]:
    """Every problem with the docs' counts, as 'file:line: what is wrong'; empty when they are all right."""
    problems = []
    checked = {file: set() for file in docs}
    for rule in rules:
        text = docs[rule.file]
        matches = list(re.finditer(rule.pattern, text))
        if not matches:
            problems.append(f"{rule.file}: no longer says '{rule.pattern}' ({rule.count}); update the rules to the new wording")
        for match in matches:
            checked[rule.file].add(match.start(1))
            stated, expected = int(match.group(1).replace(',', '')), _expected(rule, counts[rule.count])
            if stated != expected:
                problems.append(
                    f'{rule.file}:{_line_of(text, match.start(1))}: says {match.group(1)}, but there are {expected:,} ({rule.count})'
                )
    for file, text in docs.items():
        for phrase in TEST_COUNT_PHRASE.finditer(text):
            if phrase.start(1) not in checked[file]:
                problems.append(
                    f"{file}:{_line_of(text, phrase.start())}: '{phrase.group(0)}' is a test count no rule checks; add one to the rules"
                )
    return problems


def parse_dotnet_listing(text: str) -> int:
    """The number of tests `dotnet test --list-tests` printed: one indented line each, after its heading."""
    listed = text.split('The following Tests are available:', 1)[1]
    return sum(1 for line in listed.splitlines() if line.startswith('    ') and line.strip())


def parse_playwright_listing(text: str) -> int:
    """The total from `playwright test --list`'s summary line ('Total: 84 tests in 10 files')."""
    return int(re.search(r'^Total: (\d+) tests? in', text, re.MULTILINE).group(1))


def parse_jest_json(text: str) -> int:
    """The total from `jest --json`."""
    return json.loads(text)['numTotalTests']


PARSERS = {'dotnet-list': parse_dotnet_listing, 'playwright-list': parse_playwright_listing, 'jest-json': parse_jest_json}


def gather(config: dict, run) -> dict[str, int]:
    """The real counts: each source's command is run (by `run(command, cwd)`, from the folder it names) and read by its parser, then the sums are added."""
    found = {}
    for name, source in config['sources'].items():
        if source['parser'] not in PARSERS:
            raise ValueError(f"{name}: no parser called '{source['parser']}'; the parsers are {', '.join(PARSERS)}")
        found[name] = PARSERS[source['parser']](run(source['command'], source.get('cwd', '.')))
    return with_sums(found, config.get('sums', {}))


def command_runner(root: Path):
    """Runs a command in a folder of the repository and returns what it printed; stops the whole check when it fails."""

    def run(command: list[str], cwd: str) -> str:
        # npm's commands are .cmd scripts on Windows, which need the shell to start.
        result = subprocess.run(
            command, cwd=root / cwd, capture_output=True, text=True, encoding='utf-8', shell=os.name == 'nt', check=False
        )
        if result.returncode != 0:
            sys.exit(f'{" ".join(command)} failed:\n{result.stdout}\n{result.stderr}')
        return result.stdout

    return run


def count_problems(root: Path, config: dict, run=None) -> list[str]:
    """Every count in the documents that is not the real number."""
    rules = rules_of(config)
    actual = gather(config, run or command_runner(root))
    print('Counts: ' + ', '.join(f'{key} {value:,}' for key, value in actual.items()))
    docs = {file: (root / file).read_text(encoding='utf-8') for file in sorted({rule.file for rule in rules})}
    return check(docs, rules, actual)
