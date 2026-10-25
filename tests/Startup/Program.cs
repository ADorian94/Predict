using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml.Linq;
using Predict;
using Predict.Services;

internal static class Program
{
    static int Main()
    {
        var context = new UiContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            PendingConsentDoesNotHideInputs(context);
            Console.WriteLine("PASS pending consent does not hide weight/reps/RPE inputs");
            PopupWaitsForNativeLoadedAndDispatcher(context);
            Console.WriteLine("PASS first popup waits for native Loaded and dispatcher");
            KnownDecisionComparedWithPendingFirstLaunch(context);
            Console.WriteLine("PASS recorded decision skips popup; unknown decision may remain pending without hiding inputs");
            AnimationFailureStillRevealsInputs(context);
            Console.WriteLine("PASS entrance animation failure restores all inputs without escaping async lifecycle");
            PendingPromptIsNotDuplicated(context);
            Console.WriteLine("PASS repeated Loaded/appearance/navigation does not duplicate a pending popup");
            NavigationBeforeDispatchDefersPopup(context);
            Console.WriteLine("PASS navigation/unload before dispatch skips stale popup and retries on return");
            PresentationFailureRetriesOnReturn(context);
            Console.WriteLine("PASS synchronous/asynchronous consent failures are caught and retry on return");
            DispatcherFailureRetriesOnReturn(context);
            Console.WriteLine("PASS rejected/throwing dispatcher retries without hiding inputs");
            PopupWaitsForAllEntranceTasks(context);
            Console.WriteLine("PASS popup waits for all entrance animation tasks");
            Console.WriteLine("Startup tests: 9 passed, 0 failed");
            return 0;
        }
        catch (Exception e) { Console.WriteLine("FAIL " + e.Message); return 1; }
    }

    static void PendingConsentDoesNotHideInputs(UiContext context)
    {
        MixpanelService.Reset();
        var page = new MainPage(new Predict.ViewModels.MainViewModel());
        page.RaiseLoaded();
        page.Appear();
        context.Until(() => page.Cards.All(c => c.Opacity == 1 && c.TranslationY == 0));
        page.Dispatcher.Drain();
        Check(page.AlertCalls == 1, "expected one explicit consent popup");
        Check(!page.Answer.Task.IsCompleted, "prompt must still be unresolved");
    }

    static void PopupWaitsForNativeLoadedAndDispatcher(UiContext context)
    {
        MixpanelService.Reset();
        var page = new MainPage(new Predict.ViewModels.MainViewModel());
        page.Appear();
        context.Until(() => page.Cards.All(c => c.Opacity == 1 && c.TranslationY == 0));
        Check(page.AlertCalls == 0, "first popup was requested before native Loaded");
        page.RaiseLoaded();
        Check(page.AlertCalls == 0, "popup must wait for the dispatcher after Loaded");
        page.Dispatcher.Drain();
        Check(page.AlertCalls == 1 && page.PromptWasLoaded && page.PromptWasDispatched,
            "popup must be requested once on loaded UI dispatcher");
        page.Answer.SetResult(false);
        context.Flush();
    }

    static void KnownDecisionComparedWithPendingFirstLaunch(UiContext context)
    {
        MixpanelService.Ensure = _ => Task.CompletedTask; // already recorded refusal or acceptance
        var returning = new MainPage(new Predict.ViewModels.MainViewModel());
        returning.RaiseLoaded(); returning.Appear();
        context.Until(() => returning.Cards.All(c => c.Opacity == 1 && c.TranslationY == 0));
        returning.Dispatcher.Drain();
        Check(returning.AlertCalls == 0, "recorded choice must not prompt again");
        returning.Disappear(); returning.Appear(); returning.Dispatcher.Drain();
        Check(returning.AlertCalls == 0 && returning.Cards.All(c => c.Fades == 1),
            "later appearance must not replay entrance or bypass recorded choice");
        PendingConsentDoesNotHideInputs(context);
    }

    static void AnimationFailureStillRevealsInputs(UiContext context)
    {
        MixpanelService.Reset();
        var page = new MainPage(new Predict.ViewModels.MainViewModel());
        foreach (var card in page.Cards) card.FailAnimation = true;
        page.RaiseLoaded(); page.Appear();
        context.Until(() => page.Cards.All(c => c.Opacity == 1 && c.TranslationY == 0));
        page.Dispatcher.Drain();
        Check(page.AlertCalls == 1, "animation failure must not suppress ready popup");
        page.Answer.SetResult(false); context.Flush();
    }

    static MainPage ReadyPage(UiContext context)
    {
        var page = new MainPage(new Predict.ViewModels.MainViewModel());
        page.RaiseLoaded(); page.Appear();
        context.Until(() => page.Dispatcher.Count == 1);
        return page;
    }

    static void PendingPromptIsNotDuplicated(UiContext context)
    {
        MixpanelService.Reset();
        var page = ReadyPage(context);
        page.RaiseLoaded(); page.Appear();
        Check(page.Dispatcher.Count == 1, "duplicate readiness must not queue multiple prompts");
        page.Dispatcher.Drain();
        page.RaiseLoaded(); page.Disappear(); page.Appear(); page.Dispatcher.Drain();
        Check(page.AlertCalls == 1 && !page.Answer.Task.IsCompleted,
            "pending prompt must not be duplicated on navigation");
        Check(page.Cards.All(c => c.Opacity == 1 && c.Fades == 1), "pending navigation hid inputs or replayed entrance");
        Check(((Predict.ViewModels.MainViewModel)page.BindingContext!).Refreshes == 3,
            "every appearance must continue refreshing units");
        page.Answer.SetResult(false); context.Flush();
    }

    static void NavigationBeforeDispatchDefersPopup(UiContext context)
    {
        foreach (var unload in new[] { false, true })
        {
            MixpanelService.Reset();
            var page = ReadyPage(context);
            if (unload) page.Unload(); else page.Disappear();
            page.Dispatcher.Drain();
            Check(page.AlertCalls == 0, "stale dispatched popup appeared after navigation/unload");
            if (unload) page.RaiseLoaded(); else page.Appear();
            page.Dispatcher.Drain();
            Check(page.AlertCalls == 1, "return to ready page must retry deferred popup");
            page.Answer.SetResult(false); context.Flush();
        }
        MixpanelService.Reset();
        var leavingEarly = new MainPage(new Predict.ViewModels.MainViewModel());
        leavingEarly.Appear(); leavingEarly.Disappear(); leavingEarly.RaiseLoaded();
        context.Until(() => leavingEarly.Cards.All(c => c.Opacity == 1 && c.TranslationY == 0));
        leavingEarly.Dispatcher.Drain();
        Check(leavingEarly.AlertCalls == 0, "Loaded on a hidden page must not prompt");
        leavingEarly.Appear(); leavingEarly.Dispatcher.Drain();
        Check(leavingEarly.AlertCalls == 1, "deferred first launch must retry on return");
        leavingEarly.Answer.SetResult(false); context.Flush();
    }

    static void PresentationFailureRetriesOnReturn(UiContext context)
    {
        foreach (var synchronous in new[] { false, true })
        {
            MixpanelService.Reset();
            var page = ReadyPage(context);
            MixpanelService.Ensure = _ => synchronous
                ? throw new InvalidOperationException("synchronous boundary failure")
                : Task.FromException(new InvalidOperationException("asynchronous boundary failure"));
            page.Dispatcher.Drain(); context.Flush();
            Check(page.Cards.All(c => c.Opacity == 1), "presentation failure hid inputs");
            MixpanelService.Reset();
            page.Disappear(); page.Appear(); page.Dispatcher.Drain();
            Check(page.AlertCalls == 1, "failed consent attempt must be retried next appearance");
            page.Answer.SetResult(false); context.Flush();
        }
    }

    static void DispatcherFailureRetriesOnReturn(UiContext context)
    {
        foreach (var throwing in new[] { false, true })
        {
            MixpanelService.Reset();
            var page = new MainPage(new Predict.ViewModels.MainViewModel());
            page.Dispatcher.Throw = throwing; page.Dispatcher.Reject = !throwing;
            page.RaiseLoaded(); page.Appear();
            context.Until(() => page.Cards.All(c => c.Opacity == 1 && c.TranslationY == 0));
            Check(page.AlertCalls == 0, "unavailable dispatcher cannot present popup");
            page.Dispatcher.Throw = page.Dispatcher.Reject = false;
            page.Disappear(); page.Appear(); page.Dispatcher.Drain();
            Check(page.AlertCalls == 1, "dispatch failure must release scheduling guard for retry");
            page.Answer.SetResult(false); context.Flush();
        }
    }

    static void PopupWaitsForAllEntranceTasks(UiContext context)
    {
        MixpanelService.Reset();
        var page = new MainPage(new Predict.ViewModels.MainViewModel());
        var gates = page.Cards.Select(_ => new TaskCompletionSource()).ToArray();
        for (int i = 0; i < gates.Length; i++) page.Cards[i].AnimationCompletion = gates[i].Task;
        page.RaiseLoaded(); page.Appear();
        context.Until(() => page.Cards.All(c => c.Fades == 1));
        Check(page.Dispatcher.Count == 0, "popup queued before entrance tasks finished");
        gates[0].SetResult(); gates[1].SetResult(); context.Flush();
        Check(page.Dispatcher.Count == 0, "popup must also await RPE entrance task");
        gates[2].SetResult(); context.Until(() => page.Dispatcher.Count == 1);
        page.Dispatcher.Drain();
        Check(page.AlertCalls == 1, "popup missing after all entrance tasks completed");
        page.Answer.SetResult(false); context.Flush();
    }

    internal static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }
}

// A single-threaded UI pump: timers resume on the UI thread, async-void errors fail the test.
internal sealed class UiContext : SynchronizationContext
{
    readonly ConcurrentQueue<Action> work = new();
    public override void Post(SendOrPostCallback callback, object? state) => work.Enqueue(() => callback(state));
    public void Until(Func<bool> done)
    {
        var timeout = Stopwatch.StartNew();
        while (!done())
        {
            if (work.TryDequeue(out var action)) action();
            else Thread.Sleep(1);
            if (timeout.Elapsed > TimeSpan.FromSeconds(2))
                throw new Exception("weight/reps/RPE inputs did not become visible while consent was pending (2s deadline)");
        }
        Flush();
    }
    public void Flush() { while (work.TryDequeue(out var action)) action(); }
}

// Only the MAUI/platform boundary is replaced; the complete production MainPage is linked above.
public class ContentPage
{
    public object? BindingContext { get; set; }
    public bool IsLoaded { get; private set; }
    public event EventHandler? Loaded;
    public FakeDispatcher Dispatcher { get; } = new();
    public TaskCompletionSource<bool> Answer { get; set; } = new();
    public int AlertCalls { get; private set; }
    public Func<Task<bool>>? AlertOverride { get; set; }
    public bool PromptWasLoaded { get; private set; }
    public bool PromptWasDispatched { get; private set; }
    protected virtual void OnAppearing() { }
    protected virtual void OnDisappearing() { }
    public void Appear() => OnAppearing();
    public void Disappear() => OnDisappearing();
    public void RaiseLoaded() { IsLoaded = true; Loaded?.Invoke(this, EventArgs.Empty); }
    public void Unload() => IsLoaded = false;
    public Task<bool> DisplayAlert(string title, string message, string accept, string cancel)
    {
        AlertCalls++;
        PromptWasLoaded = IsLoaded;
        PromptWasDispatched = Dispatcher.Executing;
        Program.Check(title == "Optional usage analytics" && message == MixpanelService.AnalyticsDisclosure
            && accept == "Allow" && cancel == "No thanks", "explicit disclosure/choices changed");
        return AlertOverride?.Invoke() ?? Answer.Task;
    }
}
public sealed class FakeDispatcher
{
    readonly Queue<Action> work = new();
    public bool Executing { get; private set; }
    public bool Reject { get; set; }
    public bool Throw { get; set; }
    public int Count => work.Count;
    public bool Dispatch(Action action)
    {
        if (Throw) throw new InvalidOperationException("dispatcher unavailable");
        if (Reject) return false;
        work.Enqueue(action); return true;
    }
    public void Drain()
    {
        while (work.TryDequeue(out var action))
        {
            Executing = true;
            try { action(); } finally { Executing = false; }
        }
    }
}
public class VisualElement
{
    public double Opacity { get; set; }
    public double TranslationY { get; set; }
    public bool FailAnimation { get; set; }
    public Task AnimationCompletion { get; set; } = Task.CompletedTask;
    public int Fades { get; private set; }
    public Task FadeTo(double value, uint duration)
    {
        Fades++;
        if (FailAnimation) return Task.FromException(new InvalidOperationException("animation failed"));
        Opacity = value; return AnimationCompletion;
    }
    public Task TranslateTo(double x, double y, uint duration, Easing easing)
    { TranslationY = y; return Task.CompletedTask; }
    public Task ScaleTo(double value, uint duration) => Task.CompletedTask;
}
public sealed class Easing { public static Easing CubicOut { get; } = new(); }

namespace Predict
{
    public partial class MainPage
    {
        VisualElement WeightCard = new(), RepsCard = new(), RpeCard = new(), CalcButton = new();
        public VisualElement[] Cards => [WeightCard, RepsCard, RpeCard];
        void InitializeComponent()
        {
            var xaml = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "MainPage.xaml"));
            XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";
            foreach (var (name, card) in new[] { ("WeightCard", WeightCard), ("RepsCard", RepsCard), ("RpeCard", RpeCard) })
            {
                var element = xaml.Descendants().Single(e => (string?)e.Attribute(x + "Name") == name);
                card.Opacity = (double?)element.Attribute("Opacity") ?? 1;
                card.TranslationY = (double?)element.Attribute("TranslationY") ?? 0;
            }
        }
    }
}
namespace Predict.ViewModels
{
    public sealed class MainViewModel
    {
        public int Refreshes { get; private set; }
        public void RefreshUnit() => Refreshes++;
    }
}
namespace Predict.Services
{
    // Deliberately controllable consent boundary, not a reimplementation of the backend.
    // Real decision persistence / concurrency is covered by tests/Privacy.
    public static class MixpanelService
    {
        public const string AnalyticsDisclosure = "test disclosure";
        public static Func<Func<Task<bool>>, Task> Ensure { get; set; } = prompt => prompt();
        public static Task EnsureAnalyticsConsentAsync(Func<Task<bool>> prompt) => Ensure(prompt);
        public static void Reset() => Ensure = prompt => prompt();
    }
}
