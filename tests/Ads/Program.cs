using Predict.Services;
int passed = 0;
void Check(bool ok, string text) { if (!ok) throw new Exception("FAIL: " + text); }
void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
Run("default off, live permission precedes SDK; callback precedes readiness", () => {
 var b = new Backend(); var g = new AdsConsentCoordinator(b);
 Check(!g.IsReady, "default off"); g.Start();
 Check(b.Updates == 1 && b.Inits == 0 && !g.IsReady, "request UMP before any SDK call");
 b.Allowed = true; b.Update!(false);
 Check(b.Inits == 1 && !g.IsReady, "actual permission permits init even update error; not ready until callback");
 b.Initialized!(); Check(g.IsReady, "SDK callback unlocks ads");
});
Run("gather once on success, errors/denial never imply permission", () => {
 var b = new Backend(); var g = new AdsConsentCoordinator(b); g.Start(); g.Start();
 b.Update!(true); b.Update!(true);
 Check(b.Forms == 1 && b.Updates == 1, "one update and one required-form call");
 b.Form!(); Check(!g.IsReady && b.Inits == 0, "denial/default off");
 var error = new Backend(); new AdsConsentCoordinator(error).Start(); error.Update!(false);
 Check(error.Forms == 0 && error.Inits == 0, "error is not permission");
});
Run("readiness changes, privacy form suspends and clears cache; callback rereads UMP", () => {
 var b = new Backend { Allowed = true, Required = true }; var g = new AdsConsentCoordinator(b);
 g.Start(); b.Initialized!(); Check(g.IsReady, "ready before choices");
 var open = typeof(AdsConsentCoordinator).GetMethod("ShowPrivacyOptions");
 Check(open != null, "advertising privacy options reopening is missing");
 open!.Invoke(g, null); open.Invoke(g, null);
 Check(!g.IsReady && !b.Enabled && b.PrivacyForms == 1 && b.Clears == 1, "suspend before form; no duplicate");
 b.Allowed = false; b.Privacy!(); Check(!g.IsReady && b.Inits == 1, "reread live denied permission");
});
Run("SDK update/form callbacks coalesce and notify readiness once", () => {
 var b = new Backend { Allowed = true }; var g = new AdsConsentCoordinator(b);
 var changed = typeof(AdsConsentCoordinator).GetEvent("Changed"); Check(changed != null, "UI readiness notifications missing");
 int ready = 0; EventHandler handler = (_,_) => { if(g.IsReady) ready++; };
 changed!.AddEventHandler(g, handler);
 g.Start(); b.Update!(true); b.Form!(); b.Initialized!(); b.Initialized!();
 Check(b.Inits == 1 && ready == 1 && b.Enabled, "one init, one readiness notification");
 b.Allowed = false; Check(!g.IsReady, "permission must be queried live even before notification");
});
Run("background defers forms, resume rechecks; replaced activity ignores old UMP callbacks", () => {
 var b = new Backend(); var g = new AdsConsentCoordinator(b); g.Start(); var old = b.Update!;
 var pause = typeof(AdsConsentCoordinator).GetMethod("Pause");
 Check(pause != null, "lifecycle pause missing"); pause!.Invoke(g, null);
 b.Allowed = true; old(true); Check(b.Forms == 0 && b.Inits == 0 && !g.IsReady, "background cannot prompt or init");
 typeof(AdsConsentCoordinator).GetMethod("Resume")!.Invoke(g, null);
 Check(b.Forms == 1, "resume presents deferred required form"); b.Form!(); b.Initialized!();
 Check(g.IsReady, "foreground returns ready");
 typeof(AdsConsentCoordinator).GetMethod("Detach")!.Invoke(g,null); b.Allowed = false; g.Start();
 old(true); Check(b.Forms == 1 && !g.IsReady && b.Updates == 2, "stale callback ignored after replacement");
 b.Allowed = true; b.Update!(true); b.Form!(); Check(g.IsReady && b.Inits == 1, "new activity no double SDK init");
});
Run("optional unavailable UMP/form/SDK never escapes into app usability", () => {
 foreach (var op in new[] { "RequestUpdate", "GatherConsent", "ShowPrivacyOptions", "InitializeAds" }) {
  var b = new Backend { ThrowAt = op, Required = true }; var g = new AdsConsentCoordinator(b);
  g.Start();
  if(op == "GatherConsent") b.Update!(true);
  if(op == "ShowPrivacyOptions") g.ShowPrivacyOptions();
  if(op == "InitializeAds") { b.Allowed = true; b.Update!(false); }
  Check(!g.IsReady, "unavailable boundary stays off " + op);
 }
});
Run("interstitial preload and show are gated, idempotent, and navigation completes on pause", () => {
 var b = new Backend(); var g = new AdsConsentCoordinator(b);
 var preload = typeof(AdsConsentCoordinator).GetMethod("PreloadInterstitial");
 Check(preload != null, "gated interstitial path missing");
 preload!.Invoke(g, null); Check(b.Loads == 0 && b.Queries == 0, "no SDK access before ready");
 g.Start(); b.Allowed = true; b.Update!(false); b.Initialized!();
 preload.Invoke(g,null); preload.Invoke(g,null);
 Check(b.Loads == 1, "one preload per ready epoch");
 b.Loaded = true;
 var showing = (Task)typeof(AdsConsentCoordinator).GetMethod("TryShowInterstitialAsync")!.Invoke(g,null)!; Check(b.Shows == 1 && !showing.IsCompleted, "wait for close");
 g.Pause(); Check(showing.IsCompleted, "background must release navigation");
 b.Closed!(); Check(b.Loads == 1, "no reload in background");
 g.Resume(); Check(b.Loads == 2, "resume permits one fresh preload");
});
Run("privacy popup serialization exists independently of analytics decisions", () => {
 Check(typeof(AdsConsentCoordinator).Assembly.GetType("Predict.Services.AdsConsentPresentation") != null,
 "simultaneous optional privacy popup exclusion missing");
});
Run("privacy presentations cannot overlap and release after errors", () => {
 var first = new TaskCompletionSource<bool>(); int second = 0;
 var a = AdsConsentPresentation.RunAsync(() => first.Task);
 var b = AdsConsentPresentation.RunAsync(() => { second++; return Task.FromResult(false); });
 Check(second == 0 && !b.IsCompleted, "UMP and analytics popup overlap");
 first.SetResult(false); Task.WaitAll(a,b);
 Check(second == 1, "waiting independent popup never ran");
 try { AdsConsentPresentation.RunAsync<bool>(() => throw new InvalidOperationException()).GetAwaiter().GetResult(); } catch(InvalidOperationException) {}
 Check(AdsConsentPresentation.RunAsync(() => Task.FromResult(true)).Result, "failed popup leaked serialization lock");
});
Run("a returning-session consent form invalidates prior requests before it opens", () => {
 var b = new Backend { Allowed = true }; var g = new AdsConsentCoordinator(b);
 g.PreloadInterstitial(); g.Start(); b.Initialized!(); Check(b.Loads == 1, "returning session preload");
 b.Update!(true);
 Check(!g.IsReady && b.Clears == 1 && !b.Enabled, "gathering must invalidate old interstitial and gate banners before form");
 b.Allowed = false; b.Form!(); Check(!g.IsReady && b.Loads == 1, "form denial cannot reload");
});
Run("consent gathering cannot overlap an already shown interstitial", () => {
 var b = new Backend { Allowed = true, Loaded = true }; var g = new AdsConsentCoordinator(b);
 g.Start(); b.Initialized!(); _ = g.TryShowInterstitialAsync(); b.Update!(true);
 Check(b.Forms == 0, "consent form overlapped interstitial");
 b.Closed!(); Check(b.Forms == 1, "deferred consent gathering missing after ad close");
});
Run("duplicate interstitial close callbacks cannot reload twice", () => {
 var b = new Backend { Allowed = true, Loaded = true }; var g = new AdsConsentCoordinator(b);
 g.PreloadInterstitial(); g.Start(); b.Initialized!(); _ = g.TryShowInterstitialAsync();
 b.Closed!(); int loads = b.Loads; b.Closed!();
 Check(b.Loads == loads, "duplicate close caused another ad request");
});
Run("destroyed activity cancels pending ad navigation and ignores stale close", () => {
 var b = new Backend { Allowed = true, Loaded = true }; var g = new AdsConsentCoordinator(b);
 g.PreloadInterstitial(); g.Start(); b.Initialized!(); var task = g.TryShowInterstitialAsync(); var old = b.Closed!;
 g.Detach(); Check(task.IsCompleted, "detach stranded navigation");
 g.Start(); b.Loaded = true; var next = g.TryShowInterstitialAsync();
 Check(b.Shows == 2, "destroyed activity retained the old showing state");
 int loads = b.Loads; old(); Check(!next.IsCompleted && b.Loads == loads, "stale close touched new activity navigation/preload");
 b.Closed!(); Check(next.IsCompleted, "current close missing");
});
Run("analytics and interstitial presentation share scheduling, not consent", () => {
 var b = new Backend { Allowed = true, Loaded = true }; var g = new AdsConsentCoordinator(b);
 g.Start(); b.Initialized!();
 var analytics = new TaskCompletionSource<bool>(); var prompt = AdsConsentPresentation.RunAsync(() => analytics.Task);
 var skipped = g.TryShowInterstitialAsync();
 Check(skipped.IsCompleted && b.Shows == 0 && b.Queries == 0, "interstitial overlapped analytics prompt");
 analytics.SetResult(false); prompt.GetAwaiter().GetResult();
 var shown = g.TryShowInterstitialAsync(); int next = 0;
 var waiting = AdsConsentPresentation.RunAsync(() => { next++; return Task.FromResult(false); });
 Check(next == 0, "analytics popup overlapped shown native ad");
 b.Closed!(); Task.WaitAll(shown, waiting); Check(next == 1, "ad close failed to release optional popup scheduling");
});
Run("failed preload admission permits a later explicit request without immediate retry", () => {
 var b = new Backend { Allowed = true, ThrowAt = "LoadInterstitial" }; var g = new AdsConsentCoordinator(b);
 g.Start(); b.Initialized!();
 g.PreloadInterstitial(); Check(b.Loads == 1, "failure must not start an automatic retry loop");
 g.PreloadInterstitial(); Check(b.Loads == 2, "load exception must release preload admission for a later result");
});
Console.WriteLine($"Ads tests: {passed} passed, 0 failed");
sealed class Backend : IAdsConsentBackend {
 public bool Allowed, Required; public string ThrowAt = ""; public bool CanRequestAds => Allowed; public bool PrivacyOptionsRequired => Required;
 public int Updates, Inits, Forms, PrivacyForms, Clears, Loads, Queries, Shows; public bool Enabled, Loaded;
 public Action<bool>? Update; public Action? Initialized, Form, Privacy, Closed;
 public void RequestUpdate(Action<bool> done) { Updates++; Update = done; if (ThrowAt == "RequestUpdate") throw new InvalidOperationException("unavailable boundary"); }
 public void GatherConsent(Action done) { Forms++; Form = done; if (ThrowAt == "GatherConsent") throw new InvalidOperationException("unavailable boundary"); }
 public void ShowPrivacyOptions(Action done) { PrivacyForms++; Privacy = done; if (ThrowAt == "ShowPrivacyOptions") throw new InvalidOperationException("unavailable boundary"); }
 public void InitializeAds(Action done) { Inits++; Initialized = done; if (ThrowAt == "InitializeAds") throw new InvalidOperationException("unavailable boundary"); }
 public void SetAdsEnabled(bool enabled) => Enabled = enabled;
 public void ClearInterstitial() { Clears++; Loaded = false; }
 public void LoadInterstitial() { Loads++; if (ThrowAt == "LoadInterstitial") throw new InvalidOperationException("offline/no-fill boundary"); }
 public bool IsInterstitialLoaded() { Queries++; return Loaded; }
 public void ShowInterstitial(Action done) { Shows++; Closed = done; }
}
