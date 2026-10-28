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
- The builder now requires bounded Production managed-consumer attestation and
  all AAB native ELF PT_LOAD 16KB checks BEFORE creating the export directory.
  It reuses ads_attestation.py and inspect_elf, not an app string heuristic or
  the APK ZIP-offset gate. Hash-bound results are embedded in preparation-report.json;
  the four-file allowlist is unchanged. FAIL/INCONCLUSIVE, tool provenance drift
  or artifact mismatch stops preparation with nonzero exit and no export.
- Helpers are from the running trusted workflow checkout, not application source.
  Tool hashes are captured before app build and checked before/after inspection.
  This detects drift; separate writable checkouts are NOT an adversarial sandbox.
- Default decoder resolution uses the selected dotnet executable root and exactly
  packs/Microsoft.Android.Sdk.Windows/36.1.118/tools/K4os.Compression.LZ4.dll,
  pinned to c1dc314a38fc53f15ab9fe9a24ced2729ee4cf7dbdd271d2b8782b08f9c86a77.
  SDK 9.0.319 must already exist under that root; actual helper-selected SDK must
  equal 9.0.319. No SDK install, arbitrary glob, alternate pack or missing-tool fallback.
  Shared setup explicitly provisions SDK 9.0.319 alongside 10.0.403; the prior
  successful hosted run confirmed 9.0.319 installed. The exact default decoder
  path/hash and helper-selected SDK still require a new gated hosted run;
  missing or mismatching prerequisites intentionally BLOCK preparation.
- Standalone actual-byte verification supports explicit --dotnet and --lz4-dll
  (same decoder hash required): python tools/ci/preparation_gates.py app.aab
  --dotnet <executable> --lz4-dll <decoder> --report <absent-report.json>.
- Split generation, APK ZIP alignment, signer identity and installed-device
  acceptance remain pending. No automatic split check is claimed; no keys created.
- Managed consumer proof is bounded as documented in ads-artifact-check.md, not
  native AOT/runtime/dataflow or manifest AdMob app-ID proof.
- Current versionCode 1 is not Play-ready. Version policy, signing, Play upload,
  release enablement and user approvals remain outside this preparation path.

Both machine reports explicitly avoid production-verified/Play-ready claims.
