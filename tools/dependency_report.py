"""Reports the repository's dependencies: a known vulnerability fails (exit 1), a newer version is only listed.

    python tools/dependency_report.py

It asks the package managers themselves: `dotnet list package --vulnerable --include-transitive` and `--outdated` for the solution, and `npm audit` for
`package.json`. A vulnerability in a package that another package brings in counts, because it is in the build all the same. Needs the network (the advisories
are looked up at nuget.org and npm); a tool that is not installed, or a solution or manifest that is not there, is named and skipped. Part of `check release`.
"""

import json
import pathlib
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]


def _packages(listing):
    """Every package of every framework of every project in a `dotnet list package --format json` listing, top level and transitive."""
    for project in json.loads(listing).get('projects', []):
        for framework in project.get('frameworks', []):
            yield from framework.get('topLevelPackages', [])
            yield from framework.get('transitivePackages', [])


def vulnerable_nuget(listing):
    """One line for each advisory: the package, its version, the severity and where to read about it; the same advisory once however many projects use it."""
    found = [
        f'{package["id"]} {package.get("resolvedVersion", "?")}: {advisory.get("severity", "?")}, {advisory.get("advisoryurl", "?")}'
        for package in _packages(listing)
        for advisory in package.get('vulnerabilities', [])
    ]
    return list(dict.fromkeys(found))


def outdated_nuget(listing):
    """One line for each package that has a newer version than the one in use."""
    found = [
        f'{package["id"]} {package["resolvedVersion"]} -> {package["latestVersion"]}'
        for package in _packages(listing)
        if package.get('latestVersion') and package.get('resolvedVersion') != package['latestVersion']
    ]
    return list(dict.fromkeys(found))


def vulnerable_npm(audit):
    """One line for each package `npm audit --json` has an advisory for."""
    return [f'{name}: {entry.get("severity", "?")}' for name, entry in json.loads(audit).get('vulnerabilities', {}).items()]


def report(run):
    """The failures (vulnerabilities) and the notes (newer versions), from `run(name)`'s output for 'vulnerable', 'outdated' and 'audit' (None when skipped)."""
    texts = {name: run(name) for name in ('vulnerable', 'outdated', 'audit')}
    failures = vulnerable_nuget(texts['vulnerable']) if texts['vulnerable'] else []
    failures += vulnerable_npm(texts['audit']) if texts['audit'] else []
    notes = outdated_nuget(texts['outdated']) if texts['outdated'] else []
    return failures, notes


def _commands(root):
    solutions = sorted(root.glob('*.sln'))
    commands = {}
    if solutions:
        base = ['dotnet', 'list', solutions[0].name, 'package', '--format', 'json']
        commands['vulnerable'] = (['dotnet'], [*base, '--vulnerable', '--include-transitive'])
        commands['outdated'] = (['dotnet'], [*base, '--outdated'])
    if (root / 'package.json').exists():
        commands['audit'] = (['npm'], ['npm', 'audit', '--json'])
    return commands


def runner(root):
    commands = _commands(root)

    def run(name):
        if name not in commands:
            print(f'skipped {name}: nothing to look at in {root.name}')
            return None
        (tool,), command = commands[name]
        if shutil.which(tool) is None:
            print(f'skipped {name}: {tool} is not installed')
            return None
        # `npm audit` exits 1 when it finds something, so the exit code is not an error here: the output is the answer.
        done = subprocess.run(
            command, cwd=root, capture_output=True, text=True, encoding='utf-8', check=False, shell=sys.platform == 'win32'
        )
        return done.stdout

    return run


def main():
    failures, notes = report(runner(ROOT))
    if notes:
        print(f'{len(notes)} package(s) have a newer version (not a failure):')
        print('\n'.join(f'  {line}' for line in notes))
    if failures:
        print(f'{len(failures)} known vulnerabilit{"y" if len(failures) == 1 else "ies"}:')
        print('\n'.join(f'  {line}' for line in failures))
        return 1
    print('No known vulnerability in the dependencies.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
