using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Trinkwasser.App;

/// <summary>
/// Eigener Einstiegspunkt: Läuft schon eine Instanz, wird die Aktivierung (Start aus dem
/// Startmenü, Klick auf eine Benachrichtigung, Autostart) an sie weitergereicht und dieser
/// Prozess beendet sich sofort.
/// </summary>
public static partial class Program
{
    private const string InstanceKey = "main";

    [STAThread]
    private static int Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            RedirectActivation(mainInstance, activation);
            return 0;
        }

        Application.Start(p =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App(activation);
        });
        return 0;
    }

    // RedirectActivationToAsync darf den STA-Thread nicht einfach blockieren, weil COM-Aufrufe
    // weiterlaufen müssen. Deshalb wird mit CoWaitForMultipleObjects auf ein Ereignis gewartet
    // (Muster aus der Windows-App-SDK-Dokumentation).
    private static void RedirectActivation(AppInstance target, AppActivationArguments activation)
    {
        var done = CreateEventW(IntPtr.Zero, true, false, null);
        try
        {
            Task.Run(() =>
            {
                target.RedirectActivationToAsync(activation).AsTask().Wait();
                SetEvent(done);
            });
            // Die laufende Instanz darf ihr Fenster in den Vordergrund holen.
            AllowSetForegroundWindow(target.ProcessId);
            _ = CoWaitForMultipleObjects(0, uint.MaxValue, 1, [done], out _);
        }
        finally
        {
            CloseHandle(done);
        }
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateEventW(IntPtr attributes, [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState, string? name);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetEvent(IntPtr handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("ole32.dll")]
    private static partial uint CoWaitForMultipleObjects(uint flags, uint timeout, uint count, IntPtr[] handles, out uint index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint processId);
}
