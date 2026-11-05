"""The dependency report: which packages have a known vulnerability (that fails the check) and which have a newer version (that is only reported)."""

import json
import unittest

import dependency_report


def nuget_listing(**packages):
    """What `dotnet list package --format json` prints for one project with the given {id: package fields}."""
    return json.dumps(
        {
            'version': 1,
            'projects': [
                {
                    'path': 'C:/x/App.csproj',
                    'frameworks': [
                        {
                            'framework': 'net10.0',
                            'topLevelPackages': [{'id': name, **fields} for name, fields in packages.items()],
                        }
                    ],
                }
            ],
        }
    )


class Vulnerabilities(unittest.TestCase):
    def test_a_clean_listing_has_none(self):
        clean = json.dumps({'version': 1, 'projects': [{'path': 'C:/x/App.csproj'}]})

        self.assertEqual(dependency_report.vulnerable_nuget(clean), [], 'a project with no frameworks listed has no vulnerable package')

    def test_a_vulnerable_package_is_named_with_its_version_and_severity(self):
        listing = nuget_listing(
            **{
                'Bad.Package': {
                    'resolvedVersion': '1.2.0',
                    'vulnerabilities': [{'severity': 'High', 'advisoryurl': 'https://github.com/advisories/x'}],
                }
            }
        )

        self.assertEqual(
            dependency_report.vulnerable_nuget(listing),
            ['Bad.Package 1.2.0: High, https://github.com/advisories/x'],
            'the report names the package and where to read about it',
        )

    def test_a_transitive_package_counts_too(self):
        listing = json.dumps(
            {
                'projects': [
                    {
                        'path': 'p',
                        'frameworks': [
                            {
                                'framework': 'net10.0',
                                'transitivePackages': [
                                    {
                                        'id': 'Deep',
                                        'resolvedVersion': '0.1',
                                        'vulnerabilities': [{'severity': 'Moderate', 'advisoryurl': 'u'}],
                                    }
                                ],
                            }
                        ],
                    }
                ]
            }
        )

        self.assertEqual(
            dependency_report.vulnerable_nuget(listing),
            ['Deep 0.1: Moderate, u'],
            'a vulnerable package that something else brings in is still ours',
        )

    def test_the_same_package_in_two_projects_is_named_once(self):
        project = {
            'path': 'p',
            'frameworks': [
                {
                    'framework': 'f',
                    'topLevelPackages': [
                        {'id': 'Bad', 'resolvedVersion': '1', 'vulnerabilities': [{'severity': 'High', 'advisoryurl': 'u'}]}
                    ],
                }
            ],
        }
        listing = json.dumps({'projects': [project, project]})

        self.assertEqual(
            len(dependency_report.vulnerable_nuget(listing)), 1, 'one advisory is one line, however many projects use the package'
        )

    def test_npm_audit_names_each_vulnerable_package_and_its_severity(self):
        audit = json.dumps(
            {'vulnerabilities': {'left-pad': {'name': 'left-pad', 'severity': 'high'}, 'ok': {'name': 'ok', 'severity': 'low'}}}
        )

        self.assertEqual(
            dependency_report.vulnerable_npm(audit), ['left-pad: high', 'ok: low'], 'npm lists every package it has an advisory for'
        )

    def test_npm_audit_with_nothing_found_has_none(self):
        self.assertEqual(dependency_report.vulnerable_npm(json.dumps({'vulnerabilities': {}})), [], 'an empty list is a clean audit')


class Outdated(unittest.TestCase):
    def test_a_package_behind_its_latest_version_is_reported_and_one_at_it_is_not(self):
        listing = nuget_listing(
            Behind={'resolvedVersion': '1.0.0', 'latestVersion': '2.0.0'}, Current={'resolvedVersion': '3.0.0', 'latestVersion': '3.0.0'}
        )

        self.assertEqual(dependency_report.outdated_nuget(listing), ['Behind 1.0.0 -> 2.0.0'], 'only a newer version is news')


class Reporting(unittest.TestCase):
    def test_vulnerabilities_fail_the_report_and_newer_versions_do_not(self):
        outputs = {
            'vulnerable': nuget_listing(Bad={'resolvedVersion': '1', 'vulnerabilities': [{'severity': 'High', 'advisoryurl': 'u'}]}),
            'outdated': nuget_listing(Behind={'resolvedVersion': '1', 'latestVersion': '2'}),
            'audit': json.dumps({'vulnerabilities': {}}),
        }

        failures, notes = dependency_report.report(lambda name: outputs[name])

        self.assertEqual(
            (failures, notes), (['Bad 1: High, u'], ['Behind 1 -> 2']), 'a known vulnerability is a failure; an upgrade is a note'
        )

    def test_a_clean_repository_has_neither(self):
        clean = json.dumps({'projects': []})

        failures, notes = dependency_report.report(lambda name: json.dumps({'vulnerabilities': {}}) if name == 'audit' else clean)

        self.assertEqual((failures, notes), ([], []), 'nothing vulnerable and nothing behind')


if __name__ == '__main__':
    unittest.main()
