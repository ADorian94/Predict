# Android 16 / API36 and 16 KB acceptance

## Minimal supported toolchain

- `global.json`: SDK **10.0.403**, `rollForward: disable`, `allowPrerelease: false`,
  **`sdk.workloadVersion: 10.0.403`**. The workload pin is inside `sdk`.
- Workload set10.0.403 resolves Android **36.1.118** and MAUI SDK **10.0.110**.
- Application: **net10.0-android36.0**, explicit **MauiVersion10.0.120** (Controls
  servicing packages). This differs deliberately from the workload's MAUI SDK;
  only actual hosted restore/build validates the combination and dependency graph.
- Android platform **36**, build-tools **36.0.0**, Temurin **17**. JDK17 is within
  Android36.1.118's supported [17,22) range; retaining it minimizes unrelated change.
- Both SDKs **9.0.319** and **10.0.403** are installed in CI. Existing `net9.0`
  executable regressions retain their .NET9 runtime, supplied by SDK9 installation.
- Minimum Android **26**, ARM64-only runtime, application ID/version, calculator,
  Settings copy, consent/ad lifecycle and all existing dependency pins are retained.
  In particular Plugin.MauiMTAdmob2.4.0, Logging.Debug9.0.8 and Sentry.Maui6.6.0
  are not upgraded speculatively.

Official references: [workload sets](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-sets),
[global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json),
[Android 16 KB page sizes](https://developer.android.com/guide/practices/page-sizes),
[MAUI support](https://dotnet.microsoft.com/en-us/platform/support/policy/maui).

## Trusted orchestration and commands

Manual Dev APK uses two checkouts: `workflow/` at `github.workflow_sha` supplies
orchestration and tools; `source/` at resolved `source_ref` supplies app code.
Run workload acquisition from **workflow/** so its global.json controls selection:

```powershell
Set-Location workflow
dotnet --version
dotnet workload install maui-android
dotnet workload --version
dotnet workload list
dotnet --list-runtimes
```

Expected selected SDK/workload set:10.0.403. Do not combine pinned
`sdk.workloadVersion` with `workload install --version`: the CLI disallows that.
The workspace root has no trusted global.json in the two-checkout job; do not
install workloads there and inadvertently select a higher preinstalled SDK.

From source/, the composite first invokes workflow/tools/ci/record_source.py
`--verify-source .`, then logs evaluated TFM/MAUI/platform properties and runs:

```powershell
dotnet build Predict.csproj -c Release -f net10.0-android36.0 `
  -p:PredictTestAds=true -p:AndroidPackageFormats=apk `
  -p:RuntimeIdentifier=android-arm64 -p:PublishTrimmed=true `
  -p:AndroidKeyStore=false -p:WarningsAsErrors=XA0141 `
  "-p:AndroidSdkDirectory=$env:ANDROID_HOME" "-p:JavaSdkDirectory=$env:JAVA_HOME"
```

Regression entry point (repository root with both SDKs installed):
`python tools/ci/run_regressions.py`. Python-only fixtures/contracts:
`python -m unittest discover -s tests -p 'test_*.py' -v`.
Synthetic ELF/ZIP fixtures and test-only subprocess outputs are **not APK builds**.

## Final packaged artifact: static gates

The composite resolves verifier code from its own `github.action_path` trusted
checkout, never source/tools. For every `*-Signed.apk` in
`bin/Release/net10.0-android36.0/android-arm64/`:

```powershell
python ../workflow/tools/ci/verify_android_apk.py `
  --apk path/to/application-Signed.apk `
  --build-tools "$env:ANDROID_HOME/build-tools/36.0.0" `
  --report ../provenance/apk-verification/application.json `
  --summary ../provenance/apk-verification/application.md
```

The checker reads the **actual APK** with official aapt dump badging: exact target
36, minimum26 and only arm64-v8a; source declarations alone are insufficient.
It reads every .so, requires ARM64 ELF64 and PT_LOAD segments, rejects truncated
or malformed ELF, checks every LOAD alignment is a power of two >=16384, and
checks offset/address congruence modulo16384 (and the declared alignment).
ZIP_STORED native offsets are calculated from each **local** ZIP header including
filename/extra lengths and must be divisible by16384. ZIP_DEFLATED entries are
reported as compressed, still ELF-checked, but not failed for ZIP offset alignment.
No native libraries, unexpected ABI paths and malformed ZIP data fail closed.
The checker also runs official build-tools36 commands against the same APK:

```text
zipalign -c -P 16 -v 4 application-Signed.apk
apksigner verify --verbose --print-certs application-Signed.apk
```

Any nonzero subprocess exit, timeout or missing executable fails the gate. Reports
contain APK SHA256, manifest values, per-library offsets/LOAD detail, tool commands,
exit codes/output and errors. Dev artifacts include the APK, source-manifest.json
(SHA/workflow/configuration/toolchain intent), and extra
**provenance/apk-verification/** JSON/Markdown files. Reports remain local on
failure (artifact upload occurs only after successful build/verification).

## Runtime acceptance is separate and mandatory

Static PASS does **not** prove 16 KB execution. Before calling this migration
runtime-accepted, use a real Android16 ARM64 device/emulator with
`adb shell getconf PAGE_SIZE` returning **16384**, without relying on compatibility
mode. Install the final signed package; exercise cold start, navigation, theme and
language changes, Settings, calculator/input keyboard and insets, rotation,
background/foreground, consent-before-ad-request, failed consent update fail-closed,
privacy options, banner lifecycle, and interstitial dismissal/background paths.
An x86_64 emulator does not prove this ARM64-only package runs. Trimmed Release
plugin reflection/Activity changes must be exercised on the actual APK.

## Test package is not production

Dev builds retain test ads and runner-generated debug signing; no production key,
Play upload, release or AAB is enabled. For an in-place phone update, authorized
local tooling must re-sign with the **existing test certificate**, then repeat
signature/hash/static verification on that exact final APK. Never upload the
private key or uninstall to bypass certificate mismatch and lose user data.
A runner debug certificate is not the existing phone test certificate and is not
production signing. Release remains unconditionally fail closed.

Local SDK9-only environments cannot resolve the SDK10 pin. Python PASS is valid
only for those regressions; it is not a C# or Android build PASS. Hosted restore,
full executable regressions, actual API36 APK checks, same-test-cert signing, and
16 KB ARM64 runtime acceptance must be recorded separately.
