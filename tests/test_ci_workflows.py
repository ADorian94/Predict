"""Dependency-free guardrails; hosted execution remains required for YAML/Android validation."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class WorkflowContracts(unittest.TestCase):
    def read(self, relative):
        path = ROOT / relative
        self.assertTrue(path.is_file(), f"Missing {relative}")
        return path.read_text(encoding="utf-8")

    def test_ci_runs_regressions_and_shared_testads_build_safely(self):
        text = self.read(".github/workflows/build.yml")
        for required in ("push:", "pull_request:", "branches: [main]", "contents: read",
                         "persist-credentials: false", "timeout-minutes:", "windows-latest",
                         "python tools/ci/run_regressions.py", "uses: ./.github/actions/setup-android",
                         "uses: ./.github/actions/build-test-apk"):
            self.assertIn(required, text)
        for forbidden in ("pull_request_target", "secrets.", "contents: write", "9.0.x"):
            self.assertNotIn(forbidden, text)

    def test_shared_setup_and_build_are_pinned_to_proven_recipe(self):
        setup = self.read(".github/actions/setup-android/action.yml")
        for required in ("9.0.319", "java-version: '17'", "distribution: temurin",
                         "platforms;android-36", "build-tools;36.0.0", "dotnet workload install maui-android", "10.0.403",
                         "working-directory: ${{ inputs.workflow-directory }}"):
            self.assertIn(required, setup)
        self.assertNotIn("maui-android --version", setup)
        self.assertIn("$sdkVersion -cne '10.0.403'", setup)
        self.assertIn("$workloadVersion -cne '10.0.403'", setup)
        ci = self.read(".github/workflows/build.yml")
        self.assertIn("9.0.319", ci)
        self.assertIn("10.0.403", ci)
        build = self.read(".github/actions/build-test-apk/action.yml")
        for required in ("dotnet build Predict.csproj", "-c Release -f net10.0-android36.0",
                         "-p:PredictTestAds=true", "-p:AndroidPackageFormats=apk",
                         "-p:RuntimeIdentifier=android-arm64", "-p:PublishTrimmed=true",
                         "-p:AndroidKeyStore=false", "AndroidSdkDirectory=$env:ANDROID_HOME",
                         "JavaSdkDirectory=$env:JAVA_HOME", "$LASTEXITCODE", "No signed APK produced",
                         "verify_android_apk.py", "TOOLING_DIRECTORY", "record_source.py", "--verify-source",
                         "-getProperty:TargetFramework,MauiVersion,MauiWorkloadVersion", "--build-tools", "--report", "--summary"):
            self.assertIn(required, build)

    def test_manual_dev_separates_workflow_tools_from_selected_application(self):
        text = self.read(".github/workflows/dev-apk.yml")
        for required in ("workflow_dispatch:", "source_ref:", "default: main", "contents: read",
                         "ref: ${{ github.workflow_sha }}", "path: workflow", "path: source",
                         "ref: ${{ inputs.source_ref || 'main' }}", "git -C source rev-parse HEAD",
                         "python workflow/tools/ci/record_source.py", "workflow_sha",
                         "uses: ./workflow/.github/actions/setup-android",
                         "uses: ./workflow/.github/actions/build-test-apk", "source-directory: source",
                         "name: ${{ steps.source.outputs.artifact_name }}",
                         "source/bin/Release/net10.0-android36.0/android-arm64/*-Signed.apk",
                         "provenance/source-manifest.json", "provenance/apk-verification", "workflow-directory: workflow", "retention-days: 3", "if-no-files-found: error"):
            self.assertIn(required, text)
        self.assertEqual(2, text.count("persist-credentials: false"))
        self.assertNotIn("${{ inputs.source_ref }}", text)  # Never embedded into shell source.
        self.assertNotIn("secrets.", text)

    def test_release_is_manual_preparation_only_and_cannot_publish(self):
        text = self.read(".github/workflows/release.yml")
        for required in ("workflow_dispatch:", "enable_release:", "release_source_sha:",
                         "contents: read", "timeout-minutes:", "RELEASE_SIGNING_VERIFIED: 'false'",
                         "KEYSTORE_BASE64", "KEY_ALIAS", "KEY_PASS", "STORE_PASS",
                         "[0-9a-f]{40}", "$env:RELEASE_SOURCE_SHA -cne $env:WORKFLOW_SHA",
                         "Production signing is not independently verified; release remains disabled",
                         "if: ${{ always() }}", "predict-production-signing", "AndroidKeyStore=true"):
            self.assertIn(required, text)
        for forbidden in ("push:", "tags:", "dotnet publish", "actions/upload-artifact", "certutil", "FromBase64String"):
            self.assertNotIn(forbidden, text)


if __name__ == "__main__":
    unittest.main()
