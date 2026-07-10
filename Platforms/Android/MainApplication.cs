using Android.App;
using Android.Runtime;

namespace Predict;

[Application]
public class MainApplication : MauiApplication
{
    public static string CrashLogPath = string.Empty;

    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        CrashLogPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "crash_log.txt");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            try { System.IO.File.WriteAllText(CrashLogPath, $"[UnhandledException]\n{ex}"); } catch { }
        };

        AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
        {
            try { System.IO.File.WriteAllText(CrashLogPath, $"[AndroidUnhandled]\n{e.Exception}"); } catch { }
        };

    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
