namespace Predict.Services;

// Presentation scheduling only. Analytics and advertising retain independent decisions.
public static class AdsConsentPresentation
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public static bool IsBusy => Gate.CurrentCount == 0;
    public static IDisposable? TryEnter() => Gate.Wait(0) ? new Lease() : null;
    private sealed class Lease : IDisposable
    {
        private int released;
        public void Dispose() { if (Interlocked.Exchange(ref released, 1) == 0) Gate.Release(); }
    }
    public static async Task<T> RunAsync<T>(Func<Task<T>> present)
    {
        await Gate.WaitAsync();
        try { return await present(); }
        finally { Gate.Release(); }
    }
}
