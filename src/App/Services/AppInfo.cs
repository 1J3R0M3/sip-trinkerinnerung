using Windows.ApplicationModel;
using Windows.System;

namespace Trinkwasser.App.Services;

/// <summary>Version und Art des installierten Pakets, für Einstellungen → Info.</summary>
public static class AppInfo
{
    /// <summary>"1.2.0" aus der Paketversion 1.2.0.0 (die vierte Stelle ist im Store immer 0).</summary>
    public static string Version { get; } = Read(id => $"{id.Version.Major}.{id.Version.Minor}.{id.Version.Build}", "?");

    /// <summary>Debug-Build "Sip (Dev)" mit eigener Paketkennung, siehe App.csproj.</summary>
    public static bool IsDevBuild { get; } = Read(id => id.Name.EndsWith(".Dev", StringComparison.Ordinal), false);

    public static Task OpenAsync(string url) => Launcher.LaunchUriAsync(new Uri(url)).AsTask();

    private static T Read<T>(Func<PackageId, T> read, T fallback)
    {
        try
        {
            return read(Package.Current.Id);
        }
        catch (InvalidOperationException)
        {
            // Unverpackt gestartet: kein Paket.
            return fallback;
        }
    }
}
