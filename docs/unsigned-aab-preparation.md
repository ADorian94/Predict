# Unsigned production-profile AAB preparation (WP3)

This is **preparation, not release acceptance**. `release.yml` remains disabled.
Use the existing registered manual `dev-apk.yml` workflow on a reviewed feature
workflow ref; keep `artifact_kind=test-apk` (default) for the unchanged Dev APK
path, or explicitly choose `unsigned-production-aab`. `source_ref` still selects
the application checkout independently of the trusted workflow revision. A new
branch-only workflow is not introduced, and no main-branch merge is required by
this patch. Dispatch/merge is a separate parent-owned decision.

The opt-in job uses a fresh `windows-latest` runner, contents-read permissions,
credential-free separate workflow/app checkouts, no environment, no cache, and
no production secrets. Shared setup pins SDK/workload 10.0.403, MAUI 10.0.120,
Android 36.1.118, JDK 17 and Android platform 36 before regression tests.
The trusted Python builder runs ordinary `dotnet build` with Production + Release,
explicit PredictTestAds=false, both AndroidPackageFormat(s)=aab, ARM64 and
AndroidKeyStore=false. There is no IDE-mode trick or private target replacement.

**Key-free is not zero-signing:** the standard Android SDK CLI creates an unsigned
AAB and a runner-debug-signed side output using its automatically generated debug
key. No Dorian/private/production key is needed or read by the preparation helper.
Only the exact `bin/Release/net10.0-android36.0/android-arm64/hu.dorian.predict.aab`
is verified and exported; the known debug-signed side output is deleted after
successful verification. No Signed.aab, keys, binlogs or entire bin directories
are uploaded. A failed job uploads nothing and its ephemeral runner is discarded.

Verification rejects pre-existing bin/obj/export, missing/empty/stale output,
ambiguous/mixed package candidates, duplicate ZIP entries, signature metadata,
CRC errors, missing base structure and non-ARM64 native ABIs. It checks the
bundletool 1.18.3 JAR against the official digest recorded by WP4:
`a099cfa1543f55593bc2ed16a70a7c67fe54b1747bb7301f37fdfd6d91028e29`.
The official `validate` command must succeed, and its decoded base manifest must
match hu.dorian.predict, source versionCode/versionName, min26 and target36.
Source and workflow provenance come from their actual resolved checkout HEADs,
not from user-entered source refs. Only four explicit files are uploaded:
unsigned AAB, SHA256SUMS, source-manifest.json and preparation-report.json.

## Still blocked / not claimed

- No real Predict AAB has been built by the local stdlib tests. Their mocked
  subprocesses, fake JAR and ZIP data are explicitly fixtures, not Android proof.
- Hosted .NET 10 build/evaluated properties and exact output layout need review.
- AAB-aware artifact IL/production ads proof is pending; the old APK verifier is
  deliberately not repurposed. No C# helper or app globs are added here.
- Native ELF 16KB compatibility, bundletool-generated splits and installed-device
  acceptance remain pending gates. Structure validation is not these checks.
- Current versionCode 1 is not Play-ready. Version policy, signing, Play upload,
  release enablement and user approvals remain outside this preparation path.

Both machine reports explicitly avoid production-verified/Play-ready claims.
