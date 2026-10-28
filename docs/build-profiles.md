# Explicit application build profiles

`Predict.csproj` alone imports `build/Predict.BuildProfiles.targets`; console test
projects are intentionally unaffected. Missing/empty/misspelled profiles fail
closed. Profile names, ads booleans and Production Release are case-sensitive.

| Profile | Explicit ads input | Effective compiler tokens | Packaging |
| --- | --- | --- | --- |
| Dev | `PredictTestAds=true` | `DEBUG` or `PREDICT_TEST_ADS` required | Existing test APK path |
| Production | `PredictTestAds=false` | Neither token allowed | Release, AAB only, API36/min26, ARM64, `hu.dorian.predict` |

Compiler token matching accepts semicolon/comma delimiters and surrounding
whitespace, not substrings (`NOT_DEBUG` is not `DEBUG`). Evaluation-time defaults
are not evidence: execution-time guards inspect effective imported/CLI values
before CoreCompile, Android `_BuildApkEmbed`, and `_CopyPackage`. Separate guards
recheck properties even when an earlier guard has already run. Cross-targeting
outer builds defer validation to the Android inner build. They are not artifacts.

Errors: PREDICT001 profile; 002 explicit ads/profile pairing; 003 Production
Release; 004 forbidden Production defines; 005 missing Dev test define; 006
Production identity/platform/ABI/AAB-only packaging. Negative fixtures require
the expected code and absence of their artifact marker, not merely nonzero exit.

## Commands (PowerShell, pinned Android host only)

Existing trimmed test APK (also used by the shared CI/Dev action):

```powershell
dotnet build Predict.csproj -c Release -f net10.0-android36.0 `
  -p:PredictBuildProfile=Dev -p:PredictTestAds=true -p:AndroidPackageFormats=apk `
  -p:RuntimeIdentifier=android-arm64 -p:PublishTrimmed=true -p:AndroidKeyStore=false
```

Production **unsigned build preparation**, not signing/release permission:

```powershell
dotnet build Predict.csproj -c Release -f net10.0-android36.0 `
  -p:PredictBuildProfile=Production -p:PredictTestAds=false `
  -p:AndroidPackageFormats=aab -p:AndroidPackageFormat=aab `
  -p:ApplicationId=hu.dorian.predict -p:SupportedOSPlatformVersion=26.0 `
  -p:RuntimeIdentifier=android-arm64 -p:PublishTrimmed=true -p:AndroidKeyStore=false
```

Use fresh isolated bin/obj outputs for each profile. This documents desired build
inputs, not an executed Android command or verified unsigned artifact. Production
guards do not require a key or `AndroidKeyStore=true`; signing is a separate gate.
Final versionCode, artifact-bound ad verification, actual package validation,
signing authorization and hosted Android build remain separate release work.
`release.yml` stays disabled. Never promote a test APK by renaming/re-signing it.

## Real MSBuild guard fixtures without Android

```powershell
$env:PREDICT_DOTNET_HOST = 'C:/Users/corta/AppData/Local/Microsoft/dotnet/dotnet.exe'
$env:PREDICT_MSBUILD_DLL = 'C:/Users/corta/AppData/Local/Microsoft/dotnet/sdk/9.0.319/MSBuild.dll'
python -m unittest discover -s tests -p 'test_build_profiles.py' -v
python -m unittest discover -s tests -p 'test_*.py' -v
```

The harness invokes installed MSBuild.dll directly on non-SDK projects, bypassing
SDK selection only for fixtures; it does not alter global.json or build the app
with .NET9. Tests include late imported/CLI defines, modeled public entrypoints,
inner/outer properties and property mutation after initial validation. Modeled
Build/Rebuild/Publish/SignAndroidPackage dependency graphs are not proof of the
real Android target graph: hosted pinned SDK/workload execution is still needed.
The provenance manifest records Dev caller intent, not independent artifact proof.
