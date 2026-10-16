using Microsoft.Win32;
using Trinkwasser.Core.Sounds;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Trinkwasser.App.Services;

/// <summary>
/// Spielt einen Ton zum Probehören in den Einstellungen ab. Benachrichtigungen spielen ihren
/// Ton selbst (siehe <see cref="NotificationService"/>); das hier ist nur die Vorschau.
/// </summary>
public sealed class SoundPreview : IDisposable
{
    private readonly MediaPlayer _player = new() { AudioCategory = MediaPlayerAudioCategory.Alerts };

    /// <summary>Gibt false zurück, wenn es nichts abzuspielen gibt (lautlos oder Datei fehlt).</summary>
    public bool Play(string sound)
    {
        var uri = sound switch
        {
            SoundCatalog.Silent => null,
            SoundCatalog.Windows => WindowsNotificationSound(),
            _ when SoundCatalog.IsSipSound(sound) => NotificationService.SoundUri(sound),
            _ => null,
        };
        if (uri is null) return false;

        _player.Pause();
        _player.Source = MediaSource.CreateFromUri(uri);
        _player.Play();
        return true;
    }

    /// <summary>
    /// Der Ton, den Windows für Benachrichtigungen verwendet (Systemsteuerung → Sound → Sounds →
    /// "Benachrichtigung"). Leer = dort auf "(Kein)" gestellt.
    /// </summary>
    private static Uri? WindowsNotificationSound()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"AppEvents\Schemes\Apps\.Default\Notification.Default\.Current");
            if (key?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path))
            {
                path = Environment.ExpandEnvironmentVariables(path);
                // Manche Schemata speichern nur den Dateinamen.
                if (!Path.IsPathRooted(path)) path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", path);
                return File.Exists(path) ? new Uri(path) : null;
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
        }

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Windows Notify System Generic.wav");
        return File.Exists(fallback) ? new Uri(fallback) : null;
    }

    public void Dispose() => _player.Dispose();
}
