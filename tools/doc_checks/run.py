"""Runs the documentation checks on a repository and exits 1 when any fails, so it can be a gate.

    python tools/doc_checks/run.py                 # refs, map and primers, for the repository this file is in
    python tools/doc_checks/run.py refs primers    # only these
    python tools/doc_checks/run.py map             # rewrite MAP.md (the other checks only look)
    python tools/doc_checks/run.py counts          # compare the documents' test counts with the real ones (slow: it lists every suite)
    python tools/doc_checks/run.py --root ../app --config doc-checks.json

  refs     relative links and #anchors, and file:line citations pinned to what the line shows
  map      MAP.md is what the folders' READMEs say, and every project folder has a README
  primers  every package the repository depends on has a primer, or an exemption with a reason
  counts   every test count in the documents is the real number

What each check looks at is the repository's `doc-checks.json` (see the README of this folder); with none, the defaults apply and a check with nothing to look at passes.
Exit code: 0 all held, 1 a check failed, 2 the command was wrong.
"""

import argparse
import json
import pathlib
import sys

if __package__ in (None, ''):
    sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from doc_checks import counts, primers, refs, repo_map  # noqa: E402  (after the path is fixed so the file also runs as a script)

HERE = pathlib.Path(__file__).resolve().parent
FAST = ['refs', 'map', 'primers']
CHECKS = [*FAST, 'counts']


def load_config(root, config_file=None):
    """The repository's configuration: the named file, else `doc-checks.json` in the root, else nothing (every check has defaults)."""
    path = pathlib.Path(config_file) if config_file else pathlib.Path(root) / 'doc-checks.json'
    return json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}


def _map_arguments(config):
    section = config.get('map', {})
    groups = [tuple(group) for group in section['groups']] if 'groups' in section else repo_map.DEFAULT_GROUPS
    return section.get('named_folders', []), groups, section.get('command', repo_map.COMMAND)


def _run_check(name, root, config, files):
    if name == 'refs':
        return refs.all_problems(root, config.get('refs', {}), files, config.get('refs', {}).get('stay_inside', False))
    if name == 'map':
        return repo_map.map_problems(root, *_map_arguments(config))
    if name == 'primers':
        return primers.primer_problems(root, config['primers']) if 'primers' in config else []
    return counts.count_problems(root, config['counts'])


def main(arguments):
    parser = argparse.ArgumentParser(description='Runs the documentation checks.')
    parser.add_argument('checks', nargs='*', help=f'any of {", ".join(CHECKS)}; the fast ones when none are named')
    parser.add_argument('--root', default=str(HERE.parents[1]), help='the repository to check (this one by default)')
    parser.add_argument('--config', help='the configuration file (doc-checks.json in the root by default)')
    parser.add_argument('--files', help='the repository files as a comma-separated list (git ls-files by default; for tests)')
    options = parser.parse_args(arguments)

    unknown = [name for name in options.checks if name not in CHECKS]
    if unknown:
        print(f'No check called {", ".join(unknown)}; the checks are {", ".join(CHECKS)}.', file=sys.stderr)
        return 2
    root = pathlib.Path(options.root)
    config = load_config(root, options.config)
    names = options.checks or FAST
    if 'counts' in names and 'counts' not in config:
        print(
            'The counts check needs a "counts" section in doc-checks.json: what to run, and where the documents state a count.',
            file=sys.stderr,
        )
        return 2
    if names == ['map']:
        print(f'Wrote {repo_map.write_map(root, *_map_arguments(config)).relative_to(root)}')
        return 0

    files = options.files.split(',') if options.files else None
    failed = False
    for name in names:
        found = _run_check(name, root, config, files)
        print(f'{name}: ' + ('ok' if not found else f'{len(found)} problem(s)'))
        print('\n'.join(f'  {line}' for line in found))
        failed = failed or bool(found)
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
