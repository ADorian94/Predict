# Advertising privacy (Android, free UMP)

## Permission and order

`AndroidAdsConsentBackend` uses the already installed `Xamarin.Google.UserMessagingPlatform` 4.0.0.2 directly. Its binding namespace is **`Xamarin.Google.UserMesssagingPlatform`** (three s letters). No paid plugin consent wrapper or new dependency is required.

Every fresh Activity session requests UMP information on first resume. Immediately after starting the update, and on success, form dismissal and errors, the coordinator queries the actual `CanRequestAds()` result. Neither a request error nor a personalization choice is a locally invented advertising decision. `CanRequestAds()` means permission to request some ads, not consent for personalized ads.

The plugin `Init` runs at most once per process, only after UMP permits requests and with a resumed Activity. The service subscribes to `OnMobileAdsInitialized` **before** calling `Init`. It posts the callback onto the Android main queue: the plugin sets its own initialized flag only at the end of `Init`, even if the SDK callback is synchronous. Banners/interstitial requests remain disabled until that callback and live UMP permission both allow them.

## Requests and withdrawal

The three original banner slots instantiate `ConsentedBanner`, an empty Grid, not an invisible native ad. Only readiness creates `MTAdView`. Original units, 60px requested slot heights, History margin and `BannerAdLifecycle` loaded/failed collapse-and-restore behavior remain. Suspension removes the view, pauses/destroys the native `AdView`, detaches plugin singleton events and disconnects its handler. Unloaded/recycled hosts do not keep requesting ads.

`ResultViewModel` asks the coordinator for a preload; it never queries the plugin itself. Deferred readiness coalesces requests to one outstanding preload per ready epoch. A failed load releases admission for a later explicit result request; it does not trigger an automatic failure retry loop. If a privacy change invalidates an in-flight load, its success **or failure** drains that old request before executing any queued, still-permitted replacement. Loaded checks, every-third-exit display and post-close reload all recheck the gate. Pause/destruction release navigation waits; stale/duplicate callbacks cannot complete a newer navigation or preload twice. Native late-load results from invalidated consent epochs are discarded before another load is admitted.

Settings shows **Advertising privacy choices** only when UMP reports `PrivacyOptionsRequirementStatus.Required`. The generated command calls Google's `ShowPrivacyOptionsForm`, not Reset or the plugin's licensed wrapper. Opening suspends requests and invalidates cached interstitials before presenting the form. Dismissal, including form error, queries UMP again.

## Lifecycle and independent analytics

Own Activity references are weak; replacement invalidates old UMP/form/show callbacks. Resume rechecks live permission; a background Activity never presents consent or initializes the SDK. Pending native forms are cancelled on destruction and deferred UI is checked again before showing.

A small shared presentation semaphore prevents overlapping UMP, analytics and interstitial presentation. It schedules UI only: **Mixpanel's separate consent state, decision persistence, disclosure, defaults and HTTP filtering are unchanged.** A blocked/unavailable ad service never hides calculator inputs or prevents ordinary navigation. UMP update/configuration/network failure is non-fatal and never grants permission by itself.

## Pinned plugin 2.4.0 compatibility (review when upgrading)

Installed binary inspection established two non-public boundaries that have no supported public alternative in this version:

- The banner handler and `IMTAdViewHandler` interface are internal. Its public `Destroy()` method removes singleton event subscriptions; `DisconnectHandler` alone does not do that or destroy native refresh. The host invokes that verified method through reflection and preserves it with `DynamicDependency`.
- The plugin's `CurrentActivity` getter retains the Activity passed to first Init; its setter is private. Calling Init again would reinitialize advertising SDKs. The Android adapter therefore invokes the preserved setter to release a destroyed Activity and bind the current one without a second Init. Interstitial display itself uses MAUI's `Platform.CurrentActivity` in the installed plugin.
- Interstitial close/failure events belong to the plugin singleton, with no native-ad identity. Activity destruction releases the coordinator's navigation wait and scheduling lease immediately, but retains the outstanding native presentation's terminal subscriptions as a retirement barrier (no Activity captured). Replacement Activities skip interstitial shows until that old close/failure arrives and removes the old handlers. If it never arrives, interstitials stay skipped for the process lifetime; navigation, calculators and privacy scheduling do not wait. A timeout would not establish native retirement and is intentionally not used.

These are deliberately narrow, version-pinned compatibility shims, not a general reflection framework. A trimmed native Android build and rotation/background testing must verify them. Console doubles cannot establish JNI, trimming, renderer or real UMP delivery behavior.

## Verification and native acceptance

See `tests/README.md` for production-linked console test commands. Android adapter tests replace SDK/platform APIs only; banner tests replace MAUI/native APIs while linking the real host/lifecycle. The implementation was also C#-compiled against the installed real Android/MAUI/UMP/plugin reference assemblies without running an Android packaging/build pipeline. That is an API compile check, not an APK build or phone test.

Before native acceptance, use explicit Google test-ad units (`PredictTestAds=true`, or Debug), never click live ads, and verify:

1. Clean launch: UMP before SDK/ad requests; analytics independently selectable; calculator always available.
2. Existing decision/offline/config error: actual UMP result governs requests; no invented consent or duplicate prompt.
3. Advertising privacy entry when Required: dismiss with changes/error, cached interstitial invalidation and banner native refresh teardown/recreation.
4. Rotation/recreation/background during update/form/SDK initialization/interstitial; no stale callbacks, duplicate Init or navigation hang.
5. All three banner placements: reserved size, real load failure collapse, successful restoration; RTS omission and existing analytics UI unchanged.
6. Confirm the preserved private plugin members survive the actual trimmed build.

The parent observed an existing published **Predict GDPR** European-regulations message in AdMob screenshots. Creating/publishing a replacement is not a known blocker. Targeting and privacy-policy URL were not visible; actual device UMP delivery still needs exercising. Store readiness, app-ads.txt and Play closed-testing requirements are separate from this source integration. No IDs, SDK versions, signing, workflow or console state are changed by this work.
