# GitHub Actions: regression CI and manual Android builds

Application commands must choose an [explicit build profile](build-profiles.md).
Release alone does not select Production; CI and Dev APK remain explicit Dev/true.

## Automatic CI (`.github/workflows/build.yml`)

Pushes and pull requests targeting `main` keep the existing branch filters. The
standard public-repository `windows-latest` runner has a 45-minute job limit.
Permissions are explicitly `contents: read`; checkout does not persist Git
credentials. There is no production secret, deployment, APK upload or privileged
`pull_request_target` trigger in CI. Fork PRs deliberately execute the PR merge
snapshot's regression tests and application MSBuild code on an ephemeral runner,
without production credentials. Normal GitHub fork approval policy still applies.
Do not dispatch the manual workflows for an unreviewed fork as a privileged substitute.

`python tools/ci/run_regressions.py` runs all executable C# projects under `tests`,
including both `PredictTestAds` banner configurations and the AdViews
`--unload-repro` / `--detach-during-remove` lifecycle paths. Python discovery is
`test_*.py`, so CI contracts are not accidentally excluded by the old wiring-only
pattern. These console harnesses do not replace native device acceptance.

CI installs both .NET SDK **9.0.319** (supplies the console tests' .NET9
runtime) and **10.0.403** before running regressions. Console test TFMs remain
`net9.0`; SDK10 builds them without major runtime roll-forward. Root `global.json`
selects SDK10 exactly and pins **`sdk.workloadVersion=10.0.403`**. It is not a
top-level `workload` object. Shared setup retains supported Temurin **17**, installs
Android platform **36** and build-tools **36.0.0**, then runs
`dotnet workload install maui-android` from the trusted workflow checkout root.
Do **not** add `--version` to this command when global.json pins workloadVersion.
The setup logs SDK/runtime/workload inventories and fails on unexpected selected
SDK/workload versions. MAUI workload SDK **10.0.110** is distinct from explicitly
selected MAUI Controls NuGet **10.0.120**; hosted restore/build must confirm this
servicing combination. Existing plugin, logging, Sentry and other pins are retained.

The shared build verifies source project/global.json intent using trusted tooling,
logs evaluated Android/MAUI properties, then builds trimmed Release
`net10.0-android36.0` / `android-arm64` with `PredictBuildProfile=Dev`, `PredictTestAds=true` and
`AndroidKeyStore=false` (runner debug signing). XA0141 is an error. It requires a
signed APK and invokes the trusted `tools/ci/verify_android_apk.py`, not an app
checkout script. The checker enforces actual aapt targetSdk **36**, minSdk **26**,
ARM64-only ABI, all native ELF PT_LOAD 16 KB alignment/congruence, local-header
ZIP_STORED native data offsets, official `zipalign -c -P 16 -v 4`, and
`apksigner verify --verbose --print-certs`. Compressed libraries are explicitly
reported, not incorrectly failed on their ZIP data offset. Missing/malformed
libraries, tool failures and missing tools fail closed. JSON detail plus Markdown
summary are saved; the summary also goes to the Actions job summary on success.
See [Android API36 acceptance](android-api36.md) for commands and runtime limits.
The JDK major, SDK/workload set and SDK packages are explicit; runner images and
action major tags are not an immutable offline build image. Contract/fixture
tests do not imply a native build, signature or device acceptance success.

## Manual Dev APK (`.github/workflows/dev-apk.yml`)

Once this workflow exists on `main`, open **Actions → Dev APK → Run workflow**.
Normally select `main` as the **workflow ref**. `source_ref` is optional and defaults
to `main`; it selects an application branch, tag or full commit SHA in this
repository, not a script to execute. An invalid ref fails checkout.

The workflow checks out its own immutable `github.workflow_sha` into `workflow/`,
and resolves `source_ref` with `actions/checkout` into the separate `source/`
directory. Only `workflow/` supplies composite actions, APK verification and provenance tooling;
application `.github/` and `tools/` are not used as CI orchestration. Inputs are
passed as action inputs/environment data, never spliced into shell source. Building
an application still executes its MSBuild code; this is not a sandbox for hostile
source. Manual builds have no signing secrets or persisted checkout credentials.

Before building, `git -C source rev-parse HEAD` records the actual source commit.
The full validated SHA appears in the log, `source-manifest.json`, and artifact
name `predict-dev-<full-source-sha>`; no stale hard-coded source SHA is used.
The manifest also records the workflow SHA, SDK/workload/MAUI/TFM pins, explicit Dev/Release/ARM64/test-ad/trimmed intent
and runner-debug signing. It is a provenance/configuration record, not independent
proof of source behavior or a certificate attestation.

Workload installation runs with `workflow-directory: workflow`, **not** the empty
workspace root (whose highest preinstalled SDK could win). The app builds in
`source/`, whose project/global.json must match the trusted API36 recipe.
Successful manual builds upload only `*-Signed.apk`, the JSON manifest, and
`provenance/apk-verification/*.json` / `*.md` reports, with three-day retention
and missing-file errors. The artifact ZIP may contain a directory hierarchy;
look for `source-manifest.json` and the signed APK within it. No private key,
keystore, credentials, `bin/obj` tree or production AAB is uploaded.

### Phone updates require local re-signing

Each hosted runner can generate a different **debug** certificate. A runner-debug
signed APK is not signed by the existing local phone-test key and is not a
production release. Before an in-place phone update, compare the certificate
fingerprint printed by `apksigner` with the accepted local test APK. **Local
re-signing with the authorized existing development key remains necessary** to
preserve update compatibility. Verify the final APK signature again. Never upload
that private key, use the production upload key for test builds, or uninstall the
app merely to work around a certificate mismatch and lose user data.

## Release is deliberately disabled (`.github/workflows/release.yml`)

Tag pushes no longer trigger a release. The only trigger is manual dispatch, with
required `enable_release` acknowledgement and a full lowercase nonzero
`release_source_sha` that must exactly equal the selected workflow/source commit.
The preflight refuses missing/obvious placeholder signing secrets
(`KEYSTORE_BASE64`, `KEY_ALIAS`, `KEY_PASS`, `STORE_PASS`) without printing values.
Even valid inputs and present secrets **always fail**: production signing has not
been independently verified. `RELEASE_SIGNING_VERIFIED=false` documents the
closed gate; changing that value alone cannot enable publication.

This is preparation only: no source checkout, private-key decoding, signing,
production artifact upload, release creation or Play upload exists. No release
trial is part of CI unification, and secret correctness has not been inspected.
An `always()` step removes the reserved
`RUNNER_TEMP/predict-production-signing` directory if present; currently no key
is materialized there or anywhere else.

Before any separately approved production-signing implementation, verify key
ownership/public fingerprint and certificate expectations independently, explicitly
set **`AndroidKeyStore=true`** in the real publish command, keep key material only
in that runner-temp directory, and retain unconditional cleanup on every exit.
Do not mistake the Dev APK's explicitly false setting or this documentation for
an exercised production-signing path. Keeping release disabled is intentional,
not a claim that AAB signing has been tested.

## Verification boundaries and cost

The added Python tests are stdlib-only runner/provenance/contract regressions,
including malformed-SHA rejection and real subprocess fail-fast behavior. Text
contracts do not parse the complete GitHub Actions schema or execute Android
packaging. Hosted GitHub validation of the workflow and APK is still required.
The existing console test projects and application behavior sources are unchanged. Pages content
and configuration are untouched. This uses standard hosted runners only; public
repository execution is free, not a promise about private repositories, larger
runners or unrelated account billing settings.
