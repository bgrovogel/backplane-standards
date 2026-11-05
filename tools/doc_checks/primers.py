"""No tool without a primer: every package the repository depends on has a primer, or is exempt for a stated reason.

The dependencies are the NuGet packages named in `Directory.Packages.props` and the npm packages in `package.json` (both lists are in the repository's
`doc-checks.json`, "primers": "dependency_files"). "covers" says which primer explains which packages (a name, or a pattern with `*`); "exempt" names
the packages that need none, each with its reason (a pure helper that another tool's primer already explains). The check also fails on an entry that no longer
matches a dependency, so the configuration cannot outlive the packages, and on a primer that is missing or not in the primer index.
"""

import fnmatch
import json
import pathlib
import re


def nuget_names(text):
    """The packages of a central package list (`Directory.Packages.props`), in the order written."""
    return re.findall(r'<PackageVersion\s+Include="([^"]+)"', text)


def npm_names(text):
    """The packages `package.json` depends on, for running and for building."""
    manifest = json.loads(text)
    return [*manifest.get('dependencies', {}), *manifest.get('devDependencies', {})]


def dependencies(files, read):
    """Every dependency named in the given files; `read(name)` gives a file's text, or None if it is not there."""
    names = []
    for name in files:
        text = read(name)
        if text is None:
            continue
        names += npm_names(text) if name.endswith('.json') else nuget_names(text)
    return names


def problems(names, config, exists, index_text):
    """Every dependency without a primer or a reason, and every entry of the configuration that is stale."""
    covers, exempt, index = config.get('covers', {}), config.get('exempt', {}), config['index']
    found = []
    for name in names:
        covered = any(fnmatch.fnmatchcase(name, pattern) for patterns in covers.values() for pattern in patterns)
        if not covered and name not in exempt:
            found.append(f'{name} has no primer: write one, list it in "covers" and in the primer index, or exempt it with a reason')
    found += [
        f'The exemption for {name} has no reason; say why it needs no primer' for name, reason in exempt.items() if not reason.strip()
    ]
    found += [f'The exemption for {name} is for a package that is not a dependency; delete it' for name in exempt if name not in names]
    for primer, patterns in covers.items():
        if not exists(primer):
            found.append(f'{primer} is named as the primer for {", ".join(patterns)} but is not there')
        elif pathlib.PurePosixPath(primer).name not in index_text:
            found.append(f'{primer} is not listed in {index}')
        found += [f'"{pattern}" in {primer} covers no dependency; delete it' for pattern in patterns if not fnmatch.filter(names, pattern)]
    return found


SECTIONS = [
    'What it is',
    'Why we use it',
    'The big idea',
    'How it works inside',
    'How it fits with everything else',
    'An example from our code',
    'How to use it',
    'The rules we follow',
    'What goes wrong',
    'Terms',
    'Further reading',
]


def template_problems(name, text):
    """What a primer lacks of the template: the read time and verified date in its header, every section in order, and a source under Further reading."""
    found = [
        f'{name}: no {label} in its header'
        for label, mark in (('read time', '**Read time:**'), ('verified date', '**Last verified:**'))
        if mark not in text
    ]
    headings = re.findall(r'^## (.+?)\s*$', text, re.MULTILINE)
    missing = [section for section in SECTIONS if section not in headings]
    found += [f"{name}: no section '{section}'" for section in missing]
    if not missing:
        present = [heading for heading in headings if heading in SECTIONS]
        found += [f"{name}: '{actual}' is where '{expected}' belongs" for actual, expected in zip(present, SECTIONS) if actual != expected]
        sources = text.split('## Further reading', 1)[1]
        if not re.search(r'https?://|\]\(', sources):
            found.append(f"{name}: 'Further reading' names no source")
    return found


def folder_problems(files, skip=()):
    """The template problems of every primer in {name: text}, except the files in `skip` (the index, the map)."""
    return [line for name, text in sorted(files.items()) if name not in skip for line in template_problems(name, text)]


def primer_problems(root, config):
    """The same, for the repository at `root`."""
    root = pathlib.Path(root)

    def read(name):
        path = root / name
        return path.read_text(encoding='utf-8') if path.exists() else None

    names = dependencies(config.get('dependency_files', ['Directory.Packages.props', 'package.json']), read)
    found = problems(names, config, lambda path: (root / path).exists(), read(config['index']) or '')
    folder = root / config.get('folder', 'docs/primers')
    if 'template' in config and folder.is_dir():
        files = {path.name: path.read_text(encoding='utf-8') for path in folder.glob('*.md')}
        found += folder_problems(files, config['template'].get('skip', []))
    return found
