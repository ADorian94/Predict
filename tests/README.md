# Predict regression checks

Run from the repository root with the .NET 9 SDK and Python available. The
single CI entry point discovers **every executable** `tests/**/*.csproj`, runs
both banner ad configurations and the explicit banner lifecycle variants, then
runs every `test_*.py` file (stdlib `unittest`; no pip dependencies):

```sh
python tools/ci/run_regressions.py
```

Use `--dotnet /path/to/dotnet` when the SDK is not on PATH. A failing process or
five-minute per-command timeout stops the run; no failures are swallowed.
The equivalent current individual commands are:

```sh
dotnet run --project tests/Ads/Predict.AdsTests.csproj -c Release
dotnet run --project tests/AndroidAds/Predict.AndroidAdsTests.csproj -c Release
dotnet run --project tests/AdViews/Predict.AdViewsTests.csproj -c Release
dotnet run --project tests/AdViews/Predict.AdViewsTests.csproj -c Release -- --unload-repro
dotnet run --project tests/AdViews/Predict.AdViewsTests.csproj -c Release -- --detach-during-remove
dotnet run --project tests/Interstitial/Predict.InterstitialTests.csproj -c Release
dotnet run --project tests/Startup/Predict.StartupTests.csproj -c Release
dotnet run --project tests/Settings/Predict.SettingsTests.csproj -c Release
dotnet run --project tests/Privacy/Predict.PrivacyTests.csproj -c Release
dotnet run --project tests/Rpe/Predict.RpeTests.csproj -c Release
dotnet run --project tests/Predict.BannerTests.csproj -c Release -p:PredictTestAds=true
dotnet run --project tests/Predict.BannerTests.csproj -c Release -p:PredictTestAds=false
python -m unittest discover -s tests -p 'test_*.py' -v
```

See [CI and manual APK build documentation](../docs/ci.md) for the pinned hosted
recipe, workflow/source ref separation, artifact provenance, local phone re-signing,
and the deliberately disabled production-release path.

The advertising tests compile the production coordinator and presentation scheduler. They query a controllable UMP boundary, not a persisted substitute consent flag. `AndroidAds` compiles the actual Android adapter against narrow platform/SDK doubles; it verifies callback ordering, failed-load explicit retry (without retry loops), invalidated-load replacement on success/failure, and Activity replacement without claiming device integration. Its interstitial retirement regressions raise close/failure events on the doubled plugin singleton after a replacement show attempt, not merely a captured old closure; they verify immediate navigation completion while the old native presentation remains unresolved, then a new show only after the old terminal event. `AdViews` compiles the actual banner host and existing `BannerAdLifecycle`; it verifies no control construction before readiness, failure/load slot behavior, native destroy and error recovery. `Interstitial` compiles the actual source-generated ResultViewModel commands and production coordinator. Settings tests use the actual source generator and verify advertising privacy is independent of Mixpanel. See `docs/advertising-consent.md` for native acceptance and the pinned plugin compatibility shims.

The startup harness links the complete production MainPage and reads its actual XAML card visibility. Platform/dispatcher/animation/consent boundaries are controllable doubles: tests cover pending first prompt versus recorded decision, native Loaded scheduling, navigation, failure/retry and animation failure. This is not a native Android renderer or phone-popup verification.

The privacy console tests link the production Mixpanel service and replace only platform preferences/app information and HTTP transport. They do not send live Mixpanel events. They exercise explicit first-run decisions, legacy unknown-consent preferences, remembered refusal, prompt failure/retry, competing prompts/settings choices, filtering, identifier rotation and cancellation.

The RPE tests link the production calculator/models/history service. `Rpe/Baseline` contains the pre-fix calculator/model snapshot with namespace-only changes, to check preservation of supported outputs. The full input grid has2160 combinations. Unsupported RTS rows are omitted only for11–20 repetitions, on screen and share; their values are excluded from the seven-formula average.

Banner tests run in both test-ad and normal Release configurations. These checks and source-wiring tests are not a physical Android UI or ad-delivery test. Android builds require the MAUI Android workload, JDK and Android SDK separately.

On the current Windows development host, tests and the Android build were also run with the child-process-only environment setting `DOTNET_TieredCompilation=0` because of a previously observed runtime crash. This is a workaround, not a permanent system repair. Do not disable OS security or change global settings to run these checks.

`Predict.csproj` explicitly excludes `tests/**/*.cs` from the MAUI app. Test projects have explicit Compile items so nested test executables and their generated obj files cannot be accidentally compiled into one another.

No APKs, signing keys, credentials, bin/obj directories or Python bytecode belong in this directory or Git history.
