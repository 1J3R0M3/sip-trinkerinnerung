using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Notifications;

namespace Trinkwasser.App.Services;

/// <summary>
/// Zeigt Erinnerungen als Windows-Benachrichtigung mit den Buttons "Getrunken" und
/// "In 10 Min. erinnern". Die AppUserModelID kommt aus dem MSIX-Paket; die COM-Aktivierung
/// für Klicks bei beendeter App steht in Package.appxmanifest.
/// </summary>
public sealed class NotificationService(Localizer loc)
{
    private const string Tag = "reminder";

    /// <summary>Klick auf eine Benachrichtigung, während die App läuft (Hintergrund-Thread).</summary>
    public event Action<ToastAction>? ActionInvoked;

    /// <summary>
    /// Muss vor dem Auswerten der Startaktivierung aufgerufen werden. Der Handler wird
    /// vor <see cref="AppNotificationManager.Register()"/> angehängt, sonst gehen Klicks verloren.
    /// </summary>
    public void Register()
    {
        var manager = AppNotificationManager.Default;
        manager.NotificationInvoked += (_, e) => ActionInvoked?.Invoke(ToastArguments.Parse(e.Arguments));
        manager.Register();
    }

    public void Unregister() => AppNotificationManager.Default.Unregister();

    public void ShowReminder(int glassMl)
    {
        var notification = new AppNotificationBuilder()
            .AddArgument(ToastArguments.ActionKey, ToastArguments.ToArgument(ToastAction.Open))
            .AddText(loc.ToastTitle)
            .AddText(loc.ToastBody(glassMl))
            .AddButton(new AppNotificationButton(loc.ToastDrank)
                .AddArgument(ToastArguments.ActionKey, ToastArguments.ToArgument(ToastAction.Drank)))
            .AddButton(new AppNotificationButton(loc.ToastSnooze(ToastArguments.SnoozeMinutes))
                .AddArgument(ToastArguments.ActionKey, ToastArguments.ToArgument(ToastAction.Snooze)))
            .BuildNotification();
        // Gleicher Tag: eine neue Erinnerung ersetzt die alte im Info-Center.
        notification.Tag = Tag;
        notification.Group = Tag;
        AppNotificationManager.Default.Show(notification);
    }
}
