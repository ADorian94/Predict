# Predict regression checks

Run from the repository root with the .NET 9 SDK and Python available:

```sh
dotnet run --project tests/Settings/Predict.SettingsTests.csproj -c Release
dotnet run --project tests/Privacy/Predict.PrivacyTests.csproj -c Release
dotnet run --project tests/Rpe/Predict.RpeTests.csproj -c Release
dotnet run --project tests/Predict.BannerTests.csproj -c Release -p:PredictTestAds=true
dotnet run --project tests/Predict.BannerTests.csproj -c Release -p:PredictTestAds=false
python -m unittest discover -s tests -p 'test_*wiring.py' -v
```

The privacy console tests link the production Mixpanel service and replace only platform preferences/app information and HTTP transport. They do not send live Mixpanel events. They exercise explicit first-run decisions, legacy unknown-consent preferences, remembered refusal, prompt failure/retry, competing prompts/settings choices, filtering, identifier rotation and cancellation.

The RPE tests link the production calculator/models/history service. `Rpe/Baseline` contains the pre-fix calculator/model snapshot with namespace-only changes, to check preservation of supported outputs. The full input grid has2160 combinations. Unsupported RTS rows are omitted only for11–20 repetitions, on screen and share; their values are excluded from the seven-formula average.

Banner tests run in both test-ad and normal Release configurations. These checks and source-wiring tests are not a physical Android UI or ad-delivery test. Android builds require the MAUI Android workload, JDK and Android SDK separately.

On the current Windows development host, tests and the Android build were also run with the child-process-only environment setting `DOTNET_TieredCompilation=0` because of a previously observed runtime crash. This is a workaround, not a permanent system repair. Do not disable OS security or change global settings to run these checks.

`Predict.csproj` explicitly excludes `tests/**/*.cs` from the MAUI app. Test projects have explicit Compile items so nested test executables and their generated obj files cannot be accidentally compiled into one another.

No APKs, signing keys, credentials, bin/obj directories or Python bytecode belong in this directory or Git history.
