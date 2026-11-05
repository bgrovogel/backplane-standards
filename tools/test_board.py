"""The work board (docs/work-board.md) is how a session, with or without the owner, knows what to do next.

It only works if it can be trusted, so board.py parses the table and these tests hold both its rules (what counts as the next task, what a
valid board is) and the real board to them. Each rule is shown on a small board that breaks it, because a check that never fails proves nothing.
"""

import pathlib
import unittest

import board

REAL_BOARD = pathlib.Path(__file__).resolve().parents[1] / 'docs' / 'work-board.md'

HEADER = '| ID | Task | Status | Who | Depends | Done when | Result |\n| -- | ---- | ------ | --- | ------- | --------- | ------ |\n'


def table(*rows):
    return HEADER + ''.join(f'| {row} |\n' for row in rows)


def tasks(*rows):
    return board.parse(table(*rows))


class Parsing(unittest.TestCase):
    def test_reads_every_row_with_its_columns(self):
        found = tasks('1 | First | done | auto | - | a test | abc123', '2 | Second | todo | ask | 1 | a doc | -')
        self.assertEqual([task.id for task in found], ['1', '2'])
        self.assertEqual(found[1].depends, ['1'])
        self.assertEqual(found[0].depends, [])
        self.assertEqual(found[1].who, 'ask')

    def test_ignores_prose_around_the_table(self):
        found = board.parse('# Board\n\nSome words.\n\n' + table('1 | First | todo | auto | - | x | -') + '\nMore words.\n')
        self.assertEqual(len(found), 1)

    def test_a_board_with_no_table_is_an_error(self):
        with self.assertRaises(ValueError):
            board.parse('# Nothing here\n')


class TheNextTask(unittest.TestCase):
    def test_is_the_first_todo_whose_dependencies_are_done(self):
        found = tasks('1 | A | done | auto | - | x | abc', '2 | B | todo | auto | 3 | x | -', '3 | C | todo | auto | 1 | x | -')
        self.assertEqual(board.next_task(found).id, '3')

    def test_skips_a_task_that_waits_on_one_not_done(self):
        found = tasks('1 | A | blocked | auto | - | x | waiting for the owner', '2 | B | todo | auto | 1 | x | -')
        self.assertIsNone(board.next_task(found))

    def test_unattended_takes_only_tasks_that_need_no_owner(self):
        found = tasks('1 | A | todo | ask | - | x | -', '2 | B | todo | auto | - | x | -')
        self.assertEqual(board.next_task(found).id, '1')
        self.assertEqual(board.next_task(found, unattended=True).id, '2')

    def test_a_task_already_being_worked_is_the_one_to_resume(self):
        found = tasks('1 | A | todo | auto | - | x | -', '2 | B | doing | auto | - | x | -')
        self.assertEqual(board.next_task(found).id, '2')

    def test_blocked_and_done_tasks_are_never_next(self):
        found = tasks('1 | A | blocked | auto | - | x | waiting for the owner', '2 | B | done | auto | - | x | abc')
        self.assertIsNone(board.next_task(found))


class AValidBoard(unittest.TestCase):
    def test_a_good_board_has_no_problems(self):
        self.assertEqual(board.problems(tasks('1 | A | done | auto | - | x | abc', '2 | B | todo | ask | 1 | y | -')), [])

    def test_ids_are_unique(self):
        self.assertTrue(board.problems(tasks('1 | A | todo | auto | - | x | -', '1 | B | todo | auto | - | x | -')))

    def test_status_and_who_come_from_the_lists(self):
        self.assertTrue(board.problems(tasks('1 | A | started | auto | - | x | -')))
        self.assertTrue(board.problems(tasks('1 | A | todo | maybe | - | x | -')))

    def test_a_dependency_must_exist(self):
        self.assertTrue(board.problems(tasks('1 | A | todo | auto | 9 | x | -')))

    def test_dependencies_may_not_loop(self):
        self.assertTrue(board.problems(tasks('1 | A | todo | auto | 2 | x | -', '2 | B | todo | auto | 1 | x | -')))

    def test_only_one_task_is_in_progress(self):
        self.assertTrue(board.problems(tasks('1 | A | doing | auto | - | x | -', '2 | B | doing | auto | - | x | -')))

    def test_done_names_its_commit_and_blocked_says_why(self):
        self.assertTrue(board.problems(tasks('1 | A | done | auto | - | x | -')))
        self.assertTrue(board.problems(tasks('1 | A | blocked | auto | - | x | -')))

    def test_a_done_task_cannot_depend_on_one_not_done(self):
        self.assertTrue(board.problems(tasks('1 | A | todo | auto | - | x | -', '2 | B | done | auto | 1 | x | abc')))

    def test_every_task_says_when_it_is_done(self):
        self.assertTrue(board.problems(tasks('1 | A | todo | auto | - | - | -')))


class TheRealBoard(unittest.TestCase):
    def test_is_valid(self):
        found = board.parse(REAL_BOARD.read_text(encoding='utf-8'))
        self.assertGreaterEqual(len(found), 10, 'the board must hold the work list')
        self.assertEqual(board.problems(found), [])


if __name__ == '__main__':
    unittest.main()
