using Trinkwasser.Core.Localization;
using Windows.ApplicationModel;
using Windows.System;

namespace Trinkwasser.App.Services;

/// <summary>
/// Autostart über die StartupTask im Paketmanifest. Hat der Nutzer den Autostart in den
/// Windows-Einstellungen ausgeschaltet (<see cref="StartupTaskState.DisabledByUser"/>),
/// darf die App ihn nicht selbst wieder einschalten – das geht nur dort.
/// </summary>
public sealed class AutostartService
{
    public const string TaskId = "TrinkwasserStartup";

    public async Task<AutostartState> GetStateAsync()
    {
        try
        {
            var task = await GetTaskAsync();
            return Map(task.State);
        }
        catch (Exception)
        {
            // Z. B. unverpackt gestartet oder TaskId fehlt im Manifest.
            return AutostartState.Unavailable;
        }
    }

    /// <summary>Schaltet den Autostart um und liefert den Zustand danach.</summary>
    public async Task<AutostartState> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await GetTaskAsync();
            if (enabled) return Map(await task.RequestEnableAsync());
            if (task.State == StartupTaskState.Enabled) task.Disable();
            return Map(task.State);
        }
        catch (Exception)
        {
            return AutostartState.Unavailable;
        }
    }

    public static Task OpenWindowsSettingsAsync() =>
        Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps")).AsTask();

    // Bewusst auf einem Hintergrund-Thread: Der erste Aufruf von StartupTask.GetAsync auf dem
    // UI-Thread während eines Layout-Durchgangs (z. B. im Loaded-Ereignis) lässt WinUI wegen
    // Wiedereintritts mit 0xC000027B abstürzen.
    private static Task<StartupTask> GetTaskAsync() => Task.Run(() => StartupTask.GetAsync(TaskId).AsTask());

    private static AutostartState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => AutostartState.Enabled,
        StartupTaskState.DisabledByUser => AutostartState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => AutostartState.DisabledByPolicy,
        StartupTaskState.EnabledByPolicy => AutostartState.EnabledByPolicy,
        _ => AutostartState.Disabled,
    };
}
