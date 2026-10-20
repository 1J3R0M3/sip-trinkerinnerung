using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Trinkwasser.Core.Achievements;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Notifications;
using Trinkwasser.Core.Sounds;

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

    /// <param name="sound">Id aus <see cref="SoundCatalog"/>.</param>
    public void ShowReminder(int glassMl, string sound)
    {
        var builder = new AppNotificationBuilder()
            .AddArgument(ToastArguments.ActionKey, ToastArguments.ToArgument(ToastAction.Open))
            .AddText(loc.ToastTitle)
            .AddText(loc.ToastBody(glassMl))
            .AddButton(new AppNotificationButton(loc.ToastDrank)
                .AddArgument(ToastArguments.ActionKey, ToastArguments.ToArgument(ToastAction.Drank)))
            .AddButton(new AppNotificationButton(loc.ToastSnooze(ToastArguments.SnoozeMinutes))
                .AddArgument(ToastArguments.ActionKey, ToastArguments.ToArgument(ToastAction.Snooze)));
        var notification = WithSound(builder, sound).BuildNotification();
        // Gleicher Tag: eine neue Erinnerung ersetzt die alte im Info-Center.
        notification.Tag = Tag;
        notification.Group = Tag;
        AppNotificationManager.Default.Show(notification);
    }

    /// <summary>"Tagesziel erreicht" – ohne Buttons, ein Klick öffnet die App.</summary>
    public void ShowGoalReached(int goalMl, string sound)
    {
        var builder = new AppNotificationBuilder()
            .AddArgument(ToastArguments.ActionKey, ToastArguments.ToArgument(ToastAction.Open))
            .AddText(loc.ToastGoalTitle)
            .AddText(loc.ToastGoalBody(goalMl));
        AppNotificationManager.Default.Show(WithSound(builder, sound).BuildNotification());
    }

    /// <summary>
    /// "Erfolg freigeschaltet" mit dem Badge als Bild; mehrere Erfolge auf einmal in einer
    /// Benachrichtigung. Ein Klick öffnet die Erfolgsseite.
    /// </summary>
    public void ShowAchievements(IReadOnlyList<Achievement> achievements, string sound)
    {
        if (achievements.Count == 0) return;
        var builder = new AppNotificationBuilder()
            .AddArgument(ToastArguments.ActionKey, ToastArguments.ToArgument(ToastAction.Achievements))
            .SetAppLogoOverride(BadgeUri(achievements[0].Id, unlocked: true), AppNotificationImageCrop.Default);
        builder = achievements is [var single]
            ? builder.AddText(loc.ToastAchievementTitle).AddText(loc.ToastAchievementBody(single.Id))
            : builder.AddText(loc.ToastAchievementsTitle(achievements.Count))
                .AddText(string.Join(", ", achievements.Select(a => loc.AchName(a.Id))));
        AppNotificationManager.Default.Show(WithSound(builder, sound).BuildNotification());
    }

    /// <summary>Bild eines Erfolgs (aus tools/gen-badges.cs), freigeschaltet oder grau mit Schloss.</summary>
    public static Uri BadgeUri(string id, bool unlocked) =>
        new($"ms-appx:///Assets/Achievements/{id}{(unlocked ? "" : ".locked")}.png");

    /// <summary>Datei eines mitgelieferten Tons; auch für das Probehören in den Einstellungen.</summary>
    public static Uri SoundUri(string id) => new($"ms-appx:///Assets/Sounds/{id}.wav");

    private static AppNotificationBuilder WithSound(AppNotificationBuilder builder, string sound) => sound switch
    {
        SoundCatalog.Silent => builder.MuteAudio(),
        // Ohne Angabe spielt Windows seinen Standardton.
        SoundCatalog.Windows => builder,
        _ when SoundCatalog.IsSipSound(sound) => builder.SetAudioUri(SoundUri(sound)),
        _ => builder,
    };

    /// <summary>Blendet eine offene Erinnerung aus, z. B. nachdem in der App ein Glas eingetragen wurde.</summary>
    public void RemoveReminder() => _ = RemoveReminderAsync();

    private static async Task RemoveReminderAsync()
    {
        try
        {
            await AppNotificationManager.Default.RemoveByTagAndGroupAsync(Tag, Tag);
        }
        catch (Exception)
        {
            // Nicht wichtig genug für eine Fehlermeldung: die Erinnerung bleibt dann einfach stehen.
        }
    }
}
