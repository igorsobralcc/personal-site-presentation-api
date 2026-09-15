import importlib.util
import io
import json
import shutil
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


policy = load("policy", "validate-repository.py")
coverage = load("coverage_check", "check-feature-coverage.py")
artifacts = load("artifacts", "contract-artifacts.py")


class ValidatorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for folder in ["scripts", "specs", "docs", ".github", "src", "tests"]:
            shutil.copytree(ROOT / folder, self.root / folder,
                            ignore=shutil.ignore_patterns("bin", "obj", "TestResults", "__pycache__"))
        for name in ["README.md", "CONTRIBUTING.md"]:
            shutil.copyfile(ROOT / name, self.root / name)

    def test_repository_positive_and_deterministic(self):
        self.assertEqual([], policy.validate(self.root))
        self.assertEqual(policy.validate(self.root), policy.validate(self.root))

    def test_new_grouped_types_fail(self):
        path = self.root / "src/Bad.cs"
        path.write_text('namespace Example; public class Bad {} public record Other(string Name);', encoding='utf-8')
        self.assertTrue(any('Source organization violation' in x for x in policy.validate(self.root)))

    def test_unmapped_scenario_fails(self):
        path = self.root / "specs/mutation-validation/spec.md"
        path.write_text(path.read_text(encoding='utf-8') + '\n### PMV-999: Unmapped behavior\n', encoding='utf-8')
        self.assertTrue(any('Unmapped completion scenario: PMV-999' in x for x in policy.validate(self.root)))

    def test_implemented_spec_with_pending_evidence_fails(self):
        path = self.root / "specs/repository-policy/spec.md"
        path.write_text(path.read_text(encoding='utf-8').replace('- Status: Approved', '- Status: Implemented'), encoding='utf-8')
        self.assertTrue(any('Implemented specification has pending evidence' in x for x in policy.validate(self.root)))

    def test_unpinned_workflow_fails(self):
        path = self.root / ".github/workflows/ci.yml"
        path.write_text(path.read_text(encoding='utf-8').replace('actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd', 'actions/checkout@main'), encoding='utf-8')
        self.assertTrue(any('Unpinned action' in x for x in policy.validate(self.root)))

    def test_missing_link_fails(self):
        path = self.root / "README.md"
        path.write_text(path.read_text(encoding='utf-8') + '\n[Missing](missing-file.md)\n', encoding='utf-8')
        self.assertTrue(any('Broken documentation link' in x for x in policy.validate(self.root)))

    def test_coverage_positive_negative_and_missing_class(self):
        good = '<coverage><packages><package><classes><class name="Feature"><lines><line number="1" hits="1"/></lines></class></classes></package></packages></coverage>'
        self.assertEqual({'Feature': (1, 1)}, coverage.check(io.StringIO(good), ['Feature'], 90))
        with self.assertRaises(ValueError):
            coverage.check(io.StringIO(good.replace('hits="1"', 'hits="0"')), ['Feature'], 90)
        with self.assertRaises(ValueError):
            coverage.check(io.StringIO(good), ['Missing'], 90)
        async_lines = good.replace('</classes>', '<class name="Feature/&lt;Get&gt;d__0"><lines><line number="2" hits="0"/></lines></class></classes>')
        with self.assertRaises(ValueError):
            coverage.check(io.StringIO(async_lines), ['Feature'], 90)

    def test_generated_artifacts_are_deterministic_and_drift_changes_output(self):
        document = json.loads((ROOT / 'docs/openapi.json').read_text(encoding='utf-8'))
        self.assertEqual(artifacts.artifacts(document), artifacts.artifacts(document))
        before = artifacts.artifacts(document)
        document['paths']['/api/v1/presentation']['get']['operationId'] = 'changedOperation'
        self.assertNotEqual(before, artifacts.artifacts(document))

    def test_type_parser_ignores_nested_types_comments_and_strings(self):
        self.assertEqual(['One'], policy.source_types('public class One { string text = "class Fake {}"; class Nested {} } // class Comment {}'))
        self.assertEqual(['Changed'], policy.source_types('public delegate void Changed(string value);'))


if __name__ == "__main__":
    unittest.main()
