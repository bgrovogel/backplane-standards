"""Reads the work board (docs/work-board.md) and says what to do next.

    python tools/board.py next               the task to work on now (the one in progress, else the first ready one)
    python tools/board.py next --unattended  the same, but only tasks marked auto (no owner needed)
    python tools/board.py check              validate the board; exit 1 and list the problems if it is not valid
    python tools/board.py list               every task with its status

The board is a Markdown table so it reads in VS Code and in a pull request; this file is the only code that understands it.
"""

import argparse
import pathlib
import sys
from dataclasses import dataclass

BOARD = pathlib.Path(__file__).resolve().parents[1] / 'docs' / 'work-board.md'
COLUMNS = ['ID', 'Task', 'Status', 'Who', 'Depends', 'Done when', 'Result']
STATUSES = ['todo', 'doing', 'blocked', 'done']
WHO = ['auto', 'ask', 'owner']
NOTHING = {'', '-'}


@dataclass(frozen=True)
class Task:
    id: str
    title: str
    status: str
    who: str
    depends: list
    done_when: str
    result: str


def parse(text):
    """The tasks in the first table whose header is the board's columns."""
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if _cells(line) == COLUMNS:
            rows = []
            for row in lines[index + 2 :]:
                if not row.startswith('|'):
                    break
                rows.append(_task(_cells(row)))
            return rows
    raise ValueError('no table with the columns ' + ' | '.join(COLUMNS))


def _cells(line):
    return [cell.strip() for cell in line.strip().strip('|').split('|')] if line.startswith('|') else []


def _task(cells):
    if len(cells) != len(COLUMNS):
        raise ValueError(f'a board row needs {len(COLUMNS)} columns, found {len(cells)}: {cells}')
    ident, title, status, who, depends, done_when, result = cells
    return Task(ident, title, status, who, [] if depends in NOTHING else [part.strip() for part in depends.split(',')], done_when, result)


def next_task(tasks, unattended=False):
    """The task in progress if there is one, else the first todo whose dependencies are done (only `auto` ones when unattended)."""
    done = {task.id for task in tasks if task.status == 'done'}
    allowed = [task for task in tasks if task.who == 'auto' or not unattended]
    for task in allowed:
        if task.status == 'doing':
            return task
    for task in allowed:
        if task.status == 'todo' and all(dependency in done for dependency in task.depends):
            return task
    return None


def problems(tasks):
    """Everything wrong with the board, one line each; empty when it can be trusted."""
    found = []
    ids = [task.id for task in tasks]
    known = set(ids)
    by_id = {task.id: task for task in tasks}
    for ident in sorted({ident for ident in ids if ids.count(ident) > 1}):
        found.append(f'{ident}: the id is used more than once')
    for task in tasks:
        if task.status not in STATUSES:
            found.append(f'{task.id}: status "{task.status}" is not one of {STATUSES}')
        if task.who not in WHO:
            found.append(f'{task.id}: who "{task.who}" is not one of {WHO}')
        if task.done_when in NOTHING:
            found.append(f'{task.id}: says nothing about when it is done')
        if task.status in ('done', 'blocked') and task.result in NOTHING:
            found.append(f'{task.id}: {task.status} needs the commit (done) or the reason (blocked) in Result')
        for dependency in task.depends:
            if dependency not in known:
                found.append(f'{task.id}: depends on {dependency}, which is not on the board')
            elif task.status == 'done' and by_id[dependency].status != 'done':
                found.append(f'{task.id}: is done but depends on {dependency}, which is not')
    if sum(task.status == 'doing' for task in tasks) > 1:
        found.append('more than one task is doing; finish or block one first')
    found.extend(_loops(tasks, known))
    return found


def _loops(tasks, known):
    graph = {task.id: [dependency for dependency in task.depends if dependency in known] for task in tasks}
    state = {}

    def visit(ident, path):
        state[ident] = 'open'
        for dependency in graph[ident]:
            if state.get(dependency) == 'open':
                return [' -> '.join(path + [ident, dependency])]
            if dependency not in state:
                loop = visit(dependency, path + [ident])
                if loop:
                    return loop
        state[ident] = 'closed'
        return []

    for ident in graph:
        if ident not in state:
            loop = visit(ident, [])
            if loop:
                return [f'dependencies loop: {loop[0]}']
    return []


def _describe(task):
    return f'{task.id}  [{task.status}, {task.who}]  {task.title}\n    done when: {task.done_when}'


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('command', choices=['next', 'check', 'list'])
    parser.add_argument('--unattended', action='store_true', help='for next: only tasks that need no owner')
    options = parser.parse_args(argv)
    tasks = parse(BOARD.read_text(encoding='utf-8'))
    if options.command == 'check':
        found = problems(tasks)
        print('\n'.join(found) if found else f'The board is valid: {len(tasks)} tasks.')
        return 1 if found else 0
    if options.command == 'list':
        print('\n'.join(_describe(task) for task in tasks))
        return 0
    chosen = next_task(tasks, options.unattended)
    print(_describe(chosen) if chosen else 'Nothing is ready' + (' that needs no owner.' if options.unattended else '.'))
    return 0 if chosen else 2


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
