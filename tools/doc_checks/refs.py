"""Checks the references in the documentation: relative Markdown links and #anchors, and the file:line citations of the code guides.

A link is broken when its file is missing or its #anchor names no heading (and, where the repository asks for it, when it leaves the repository, because a clone
on another machine does not have the neighbour folder). A citation (`path/File.cs:46`, or "line 79" after one) is stale when the file is missing or ambiguous,
the line is past the end, or the line no longer shows the token that the citations file says it is cited for: the file would still be there after a refactor,
so the line has to be pinned to what it shows. Every citation in the cited guides needs a token.

Which guides are cited, and where the pins are, is the repository's `doc-checks.json` ("refs": "cited_guides" and "citations").
"""

import json
import pathlib
import re
import subprocess

LINK = re.compile(r'(?<!!)\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)')
HEADING = re.compile(r'^(#{1,6})\s+(.*?)\s*#*\s*$')
CITATION = re.compile(r'`(?P<path>[\w./\\-]+\.(?:ts|cs|py|html|scss|json|yml|csproj|md)):(?P<line>\d+)`')
LINE_WORD = re.compile(r'\bline (?P<line>\d+)\b', re.IGNORECASE)
BARE_FILE = re.compile(r'`(?P<path>[\w./-]+\.(?:ts|cs|py|html|scss|json|yml|csproj|md))`')
FENCE = ('```', '~~~')


def slug(text):
    """GitHub's anchor for a heading."""
    text = re.sub(r'\[([^\]]*)\]\([^)]*\)', r'\1', text.replace('`', ''))
    return re.sub(r'[^\w\- ]', '', text.strip().lower()).replace(' ', '-')


def anchors_of(text):
    found, seen, in_code = set(), {}, False
    for line in text.splitlines():
        if line.startswith(FENCE):
            in_code = not in_code
        elif not in_code and (match := HEADING.match(line)):
            base = slug(match.group(2))
            found.add(base if base not in seen else f'{base}-{seen[base]}')
            seen[base] = seen.get(base, 0) + 1
    return found


def tracked_files(root):
    """Every file git tracks in the repository, as paths relative to it."""
    names = subprocess.run(['git', 'ls-files'], cwd=root, capture_output=True, text=True, check=True).stdout.split('\n')
    return [name for name in names if name]


def link_problems(files, root, stay_inside=False):
    """One line for each relative link whose file is missing or whose #anchor names no heading."""
    found, cache, inside = [], {}, pathlib.Path(root).resolve()
    for path in files:
        in_code = False
        for number, line in enumerate(path.read_text(encoding='utf-8').splitlines(), 1):
            if line.startswith(FENCE):
                in_code = not in_code
            if in_code:
                continue
            for target in LINK.findall(line):
                if re.match(r'^[a-z]+:', target):
                    continue
                file_part, _, anchor = target.partition('#')
                destination = path if file_part == '' else (path.parent / file_part).resolve()
                where = f'{path.resolve().relative_to(inside).as_posix()}:{number}'
                if stay_inside and destination != inside and inside not in destination.parents:
                    found.append(
                        f'{where}: the link {target} leaves the repository, and a clone on another machine does not have what it points at'
                    )
                elif not destination.exists():
                    found.append(f'{where}: the link {target} points at a file that is not there')
                elif anchor and destination.suffix == '.md':
                    if destination not in cache:
                        cache[destination] = anchors_of(destination.read_text(encoding='utf-8'))
                    if anchor not in cache[destination]:
                        found.append(f'{where}: the link {target} names a heading that is not in {destination.name}')
    return found


def citations(text):
    """Each (written form, path, line, document line) cited in the text; a bare "line N" counts for the last file named on the same line,
    with or without a line of its own (`SharedRules.cs`, line 13)."""
    found = []
    for number, line in enumerate(text.splitlines(), 1):
        spots = [(match.start(), 'file', match) for match in CITATION.finditer(line)]
        spots += [(match.start(), 'line', match) for match in LINE_WORD.finditer(line)]
        spots += [(match.start(), 'bare', match) for match in BARE_FILE.finditer(line)]
        last = None
        for _, kind, match in sorted(spots, key=lambda spot: spot[0]):
            if kind == 'bare':
                last = match.group('path')
            elif kind == 'file':
                last = match.group('path')
                found.append((f'{last}:{match.group("line")}', last, int(match.group('line')), number))
            elif last is not None:
                found.append((f'{last}:{match.group("line")}', last, int(match.group('line')), number))
    return found


def resolve(files, written):
    """The repository files a cited path can mean. A leading ".../" drops the start of the path, and the first folder named may be the
    end of a longer one (".../Api/Filters/X.cs" is "src/Project.Api/Filters/X.cs")."""
    wanted = written.replace(chr(92), '/').removeprefix('.../').split('/')
    matches = []
    for name in files:
        parts = name.split('/')
        if len(parts) < len(wanted) or parts[len(parts) - len(wanted) + 1 :] != wanted[1:]:
            continue
        first = parts[len(parts) - len(wanted)]
        if first == wanted[0] or first.endswith('.' + wanted[0]):
            matches.append(name)
    return matches


def citation_problems(text, doc, files, read_line, anchors):
    """One line for each citation that is ambiguous, out of range, unpinned, or no longer shows its pinned token."""
    found = []
    for written, path, line, number in citations(text):
        where = f'{doc}:{number}: {written}'
        matches = resolve(files, path)
        if len(matches) != 1:
            found.append(
                f'{where}: names {"no file" if not matches else "several files (" + ", ".join(matches) + ")"}; cite the path far enough to be one'
            )
            continue
        shown = read_line(matches[0], line)
        if shown is None:
            found.append(f'{where}: {matches[0]} has no line {line}')
        elif written not in anchors:
            found.append(
                f'{where}: not pinned; add "{written}" to the citations file with a token that line shows ({shown.strip()[:60]!r})'
            )
        elif anchors[written] not in shown:
            found.append(
                f'{where}: no longer shows "{anchors[written]}" (it shows {shown.strip()[:60]!r}); find the new line and update the guide and the citations file'
            )
    return found


def unused_anchors(texts, anchors):
    """Pins in the citations file that no guide cites any more."""
    cited = {written for text in texts for written, *_ in citations(text)}
    return [f'the citations file: "{written}" is cited by no guide; delete it' for written in anchors if written not in cited]


def line_reader(root):
    """A function giving line N (counting from one) of a file of the repository, or None past either end."""

    def read(name, number):
        lines = (pathlib.Path(root) / name).read_text(encoding='utf-8').splitlines()
        return lines[number - 1] if 0 < number <= len(lines) else None

    return read


def all_problems(root, config, files=None, stay_inside=False):
    """Every broken link, and every stale or unpinned citation, in the repository.

    `files` is the repository's file list (git's, unless given); `config` is the "refs" section of its configuration."""
    root = pathlib.Path(root)
    files = tracked_files(root) if files is None else files
    markdown = [root / name for name in files if name.endswith('.md') and (root / name).exists()]
    found = link_problems(markdown, root, stay_inside)
    guides = config.get('cited_guides', [])
    if guides:
        anchors = json.loads((root / config['citations']).read_text(encoding='utf-8'))
        texts = {doc: (root / doc).read_text(encoding='utf-8') for doc in guides}
        for doc, text in texts.items():
            found += citation_problems(text, doc, files, line_reader(root), anchors)
        found += unused_anchors(texts.values(), anchors)
    return found
