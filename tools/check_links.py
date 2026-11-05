"""Checks every relative link and heading anchor in this repository's Markdown, and exits 1 naming each one that is broken.

Run it from anywhere: `python tools/check_links.py`. It reads headings the way GitHub makes anchors (lower case, punctuation dropped, spaces to hyphens),
skips fenced code and links to other sites, and needs nothing installed.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def slug(heading: str) -> str:
    """The anchor GitHub makes for a heading."""
    text = re.sub(r'[^\w\- ]', '', heading.replace('`', '').strip().lower())
    return text.replace(' ', '-')


def headings(path: Path) -> set[str]:
    return {slug(match) for match in re.findall(r'^#{1,6}\s+(.*)$', path.read_text(encoding='utf-8'), re.MULTILINE)}


def broken_links() -> tuple[int, list[str]]:
    """How many relative links there are, and which of them point nowhere."""
    files = [path for path in ROOT.rglob('*.md') if '.git' not in path.parts]
    anchors = {path: headings(path) for path in files}
    problems, total = [], 0
    for path in files:
        text = re.sub(r'```.*?```', '', path.read_text(encoding='utf-8'), flags=re.DOTALL)
        for target in re.findall(r'\]\(([^)\s]+)\)', text):
            if re.match(r'^(https?:|mailto:)', target):
                continue
            total += 1
            link, _, anchor = target.partition('#')
            destination = (path.parent / link).resolve() if link else path
            where = f'{path.relative_to(ROOT).as_posix()}: {target}'
            if not destination.exists():
                problems.append(f'{where} (no such file)')
            elif anchor and destination.suffix == '.md' and anchor not in anchors.get(destination, headings(destination)):
                problems.append(f'{where} (no such heading)')
    return total, problems


def main() -> None:
    total, problems = broken_links()
    if problems:
        sys.exit(f'{len(problems)} of {total} links are broken:\n' + '\n'.join(problems))
    print(f'All {total} relative links hold.')


if __name__ == '__main__':
    main()
