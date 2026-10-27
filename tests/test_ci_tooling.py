"""CI tooling tests: stdlib only, no Android SDK or external services."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load_tool(name):
    path = ROOT / "tools" / "ci" / (name + ".py")
    if not path.is_file():
        raise AssertionError(f"Missing CI tool: {path}")
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RegressionPlanTests(unittest.TestCase):
    def test_real_process_failure_stops_before_later_command(self):
        import subprocess
        import sys
        tool = load_tool("run_regressions")
        self.assertTrue(callable(getattr(tool, "run_commands", None)), "Missing fail-fast executor")
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            commands = [[sys.executable, "-c", "raise SystemExit(7)"],
                        [sys.executable, "-c", "from pathlib import Path; Path('should-not-run').touch()"]]
            with self.assertRaises(subprocess.CalledProcessError) as failure:
                tool.run_commands(commands, Path(folder))
            self.assertEqual(7, failure.exception.returncode)
            self.assertFalse((Path(folder) / "should-not-run").exists())

    def test_discovers_every_executable_and_explicit_lifecycle_variants(self):
        tool = load_tool("run_regressions")
        commands = tool.commands(ROOT, dotnet="dotnet")
        projects = {str(p.relative_to(ROOT)).replace("\\", "/")
                    for p in (ROOT / "tests").rglob("*.csproj")}
        self.assertEqual(projects, {c[c.index("--project") + 1] for c in commands[:-1]})
        banner = [c for c in commands if "tests/Predict.BannerTests.csproj" in c]
        self.assertEqual(2, len(banner))
        self.assertEqual({"-p:PredictTestAds=true", "-p:PredictTestAds=false"},
                         {v for c in banner for v in c if v.startswith("-p:")})
        views = [c for c in commands if "tests/AdViews/Predict.AdViewsTests.csproj" in c]
        self.assertEqual(3, len(views))
        self.assertEqual({"--unload-repro", "--detach-during-remove"},
                         {v for c in views for v in c if v.startswith("--") and v not in ("--project", "--")})
        self.assertEqual(["-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py", "-v"], commands[-1][1:])
        self.assertTrue(all("Release" in c for c in commands[:-1]))


class ProvenanceTests(unittest.TestCase):
    def test_cli_writes_github_outputs_from_validated_sha(self):
        import json
        import os
        import subprocess
        import sys
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            output = Path(folder) / "github-output"
            env = dict(os.environ, SOURCE_SHA="12" * 20, WORKFLOW_SHA="34" * 20,
                       MANIFEST_DIR=folder, GITHUB_OUTPUT=str(output))
            result = subprocess.run([sys.executable, str(ROOT / "tools/ci/record_source.py")],
                                    env=env, capture_output=True, text=True, check=True)
            self.assertEqual("12" * 20, json.loads(result.stdout)["source_sha"])
            self.assertEqual("source_sha=" + "12" * 20 + "\nartifact_name=predict-dev-" + "12" * 20 + "\n",
                             output.read_text())

    def test_rejects_non_sha_and_placeholder_before_writing(self):
        tool = load_tool("record_source")
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            for invalid in ("main", "a" * 39, "A" * 40, "0" * 40,
                            "x" * 40, "a" * 40 + "\nartifact_name=evil", "$(touch hacked)"):
                with self.subTest(invalid=invalid):
                    with self.assertRaises(ValueError):
                        tool.record(invalid, "b2" * 20, Path(folder))
                    with self.assertRaises(ValueError):
                        tool.record("a1" * 20, invalid, Path(folder))
            self.assertEqual([], list(Path(folder).iterdir()))

    def test_manifest_and_artifact_name_use_actual_full_source_sha(self):
        tool = load_tool("record_source")
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            manifest = tool.record("a1" * 20, "b2" * 20, Path(folder))
            import json
            saved = json.loads((Path(folder) / "source-manifest.json").read_text())
            self.assertEqual(manifest, saved)
            self.assertEqual("a1" * 20, saved["source_sha"])
            self.assertEqual("b2" * 20, saved["workflow_sha"])
            self.assertEqual("predict-dev-" + "a1" * 20, saved["artifact_name"])
            self.assertTrue(saved["test_ads"])
            self.assertTrue(saved["trimmed"])
            self.assertEqual("runner-debug", saved["signing"])


if __name__ == "__main__":
    unittest.main()
