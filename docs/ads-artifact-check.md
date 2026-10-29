# Bounded static artifact ads check

This independent, secret-free tool reads packaged bytes. It never loads or executes Predict.dll, calls the plugin, starts Android, or makes an ad request. It is not wired into builds or CI.

```text
python tools/ci/ads_attestation.py app.apk --expected-mode Dev --dotnet /explicit/path/to/dotnet --lz4-dll /explicit/path/to/K4os.Compression.LZ4.dll --source-hash <nonzero-40-hex-commit> --report report.json
```

`--expected-mode` is mandatory and exactly `Dev` or `Production`. `--source-hash` is optional; when provided it must be nonzero and exactly 40 hex characters. It is a caller assertion, not evidence that the binary was built from that commit. Do not give the report the artifact's filename. Before inspection or helper compilation, the CLI rejects resolved-path and existing same-file/hardlink aliases of the artifact, explicit decoder, wrapper, templates, and explicit existing dotnet executable, with `report destination aliases protected input` (exit 2). Reports are serialized before writing, written to a sibling temporary file, flushed, and atomically replaced; serialization/write/replace failures preserve any prior report and remove the temporary file (exit 2). Inputs/tool files must remain stable during inspection; concurrent path replacement is not addressed by this bounded fix.

Exit codes: **0 PASS**, **1 FAIL** (supported, uniquely located consumer values mismatch), **2 INCONCLUSIVE** (including malformed/unsupported/tool failure). Unsupported inputs never PASS. JSON binds the complete artifact SHA256, selected ZIP entry and SHA256, stored assembly SHA256, decoded PE SHA256, template/wrapper hashes, helper binary hash, SDK version, decoder file fingerprint and assembly identity, and actual method/member/string tokens and instruction offsets.

## Supported envelope

* APK: only `lib/arm64-v8a/libassembly-store.so`.
* AAB: only `base/lib/arm64-v8a/libassembly-store.so`.
* No additional ABI/module assembly-store entries or separately packaged Predict.dll. Duplicate ZIP names, CRC errors anywhere in the archive, encrypted entries, malformed archives and oversized expansion are rejected.
* ELF64 little endian version 1, AArch64 shared object. Bounded section-table and string-table decoding, exactly one PROGBITS `payload`; no measured file offset is used.
* XABA format 3, exact version `0x80010003`, bounded counts, two 13-byte indexes per descriptor, bounded names, distinct names, mapping indexes and all data/auxiliary ranges. Exactly one `Predict.dll`. Metadata/data overlap and inter-assembly overlap rejected. Index descriptor references/ignore flags are checked; xxHash values are not recomputed and are not used for selecting the assembly.
* XALZ LZ4 block with output bounded to 16 MiB and exact decode length. PE/CLI metadata is read with `PEReader`/`MetadataReader`, not `Assembly.Load`.

The store format is pinned to dotnet/android commit `db080b91dbe7947a69005a203ea98d9bb207ad04`; this is deliberately not a generic assembly-store reader.

## Managed compiler shape only

The checker scans IL metadata, resolves exact instance `(string) -> void` MemberRefs from the `Plugin.MauiMtAdmob` assembly and requires exactly one of each consumer, each immediately preceded by `ldstr`:

* `Predict.Services.ConsentedBanner::UpdateBanner` -> `Plugin.MauiMtAdmob.Controls.MTAdView::set_AdsId`
* `Predict.Services.AndroidAdsConsentBackend::LoadInterstitial` -> `Plugin.MauiMtAdmob.Interfaces.IMTAdmob::LoadInterstitial`

It reads the inlined constants at consumers; it does not require an `AdUnits` type. Expected Dev IDs are Google's `3940256099942544/6300978111` and `3940256099942544/1033173712`; Production IDs are `3012910791046560/3302176099` and `3012910791046560/3511582164`, each with `ca-app-pub-` prefix.

This is **not general control-flow/dataflow proof**, runtime reachability proof, AOT/native code verification, manifest AdMob application-ID verification, consent/test-device verification, or proof of downstream plugin behavior. Missing or ambiguous recognized consumers and changed argument/compiler shapes are INCONCLUSIVE. Native managed header/AOT shapes are rejected. A release containing unsupported layouts remains blocked. A complete adversarial format/compiler matrix has not been certified.

## Portable helper boundary

The C# source and project are `.txt` templates so MAUI's recursive Compile glob cannot include them. The Python wrapper materializes an isolated temporary project outside the app/global.json. Its SDK policy selects a locally available .NET 9 feature band, targeting net9.0. It builds/runs only the helper, never the Android app. No NuGet package is added: supply an explicitly trusted existing K4os DLL; its actual SHA256 and assembly identity are reported. This DLL is executable tool code and must be trusted independently. No user-specific DLL path is embedded in production templates.

Verified locally with SDK 9.0.319 and installed K4os assembly 1.1.11.0, SHA256 `bab961bb87d556e004a9f0bbe10a0986e6f8697483fe1884dc86b657b280b79b`.

## Executed proof and limitations

`python -m unittest discover -s tests -p test_ads_attestation.py -v` exercises the real known Dev APK, Production-selector mismatch on those same actual bytes, malformed ELF/store counts/bounds/version, duplicate ZIP and extra ABI/module stores, malformed ZIP and unrelated-entry CRC corruption, invalid source hashes, and changed consumer-value / missing-consumer controls on the real extracted PE recompressed as a literal LZ4 block. The controls are explicitly fault-injection fixtures, not builds.

The reference APK is local optional evidence; tests requiring it or the installed helper environment explicitly skip when unavailable. The changed-PE controls currently additionally use the preserved sibling `spikes/wp2/Predict.dll` evidence. All such tests actually ran here without skips. No binary fixture is committed.

Known APK SHA256: `0b98a2c0ef60bb2d9ae76a6f1a98b7ad0cf6c4d081f5fa45cb4d9ca28e49acdd`.
Decoded PE SHA256: `eeffd2bd7768180f1009a279ccf5dd66d0dff10108f7c4ef9532ae31b34825b7`.

The AAB test is a **constructed ZIP fixture using the actual APK store**, proving the accepted entry path and extraction only. It is **not real .NET AAB proof**. No real Production AAB has been supplied or built. Split selection/payload equality, signed-vs-unsigned identity, a downloaded final AAB, general native/AOT formats, and the complete malicious-input matrix remain unverified and must not be inferred from PASS on this managed APK.
