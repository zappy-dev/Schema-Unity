#!/usr/bin/env python3
"""Tests for preflight.py. Usage: python3 test_preflight.py"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import preflight  # noqa: E402


def table(name, rows, col="Link"):
    return {"SchemeName": name,
            "Attributes": [{"AttributeName": "ID", "IsIdentifier": True}, {"AttributeName": col, "IsIdentifier": False}],
            "Entries": rows}


class Preflight(unittest.TestCase):
    def test_link_exact_ids_are_clean_and_blank_is_reported_apart(self):
        t = table("T", [{"ID": "a", "Link": "b"}, {"ID": "b", "Link": ""}])
        problems, blanks = preflight.check_link(t, "Link", t)
        self.assertEqual(problems, [])
        self.assertEqual(len(blanks), 1)
        self.assertEqual(preflight.report(problems, blanks), 2)

    def test_link_names_the_row_the_value_and_a_case_hint(self):
        t = table("T", [{"ID": "a", "Link": "B "}, {"ID": "b", "Link": "a"}])
        problems, _ = preflight.check_link(t, "Link", t)
        self.assertEqual(len(problems), 1)
        self.assertIn("T row 1 (a)", problems[0])
        self.assertIn("'B '", problems[0])
        self.assertIn("case or spaces", problems[0])

    def test_link_duplicate_target_ids_are_a_problem(self):
        t = table("T", [{"ID": "a", "Link": "a"}, {"ID": "a", "Link": "a"}])
        problems, _ = preflight.check_link(t, "Link", t)
        self.assertTrue(any("more than once" in p for p in problems))

    def test_color_accepts_rrggbb_only(self):
        t = table("T", [{"ID": "a", "Link": "#A1b2C3"}, {"ID": "b", "Link": "#fff"}, {"ID": "c", "Link": "red"},
                        {"ID": "d", "Link": "#11223344"}, {"ID": "e", "Link": ""}])
        problems, blanks = preflight.check_color(t, "Link")
        self.assertEqual(len(problems), 3)
        self.assertEqual(len(blanks), 1)
        self.assertEqual(preflight.report(problems, blanks), 1)

    def test_cells_reports_each_changed_cell_and_removed_rows(self):
        old = table("T", [{"ID": "a", "Link": "x"}, {"ID": "b", "Link": ""}])
        new = table("T", [{"ID": "a", "Link": "x"}, {"ID": "b", "Link": "a"}])
        self.assertEqual(len(preflight.changed_cells(old, old)), 0)
        diffs = preflight.changed_cells(old, new)
        self.assertEqual(len(diffs), 1)
        self.assertIn("'' -> 'a'", diffs[0])
        gone = table("T", [{"ID": "a", "Link": "x"}])
        self.assertTrue(any("removed" in d for d in preflight.changed_cells(old, gone)))


if __name__ == "__main__":
    unittest.main()
