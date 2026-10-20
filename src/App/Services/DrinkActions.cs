using Trinkwasser.Core.Achievements;
using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Reminders;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Sounds;

namespace Trinkwasser.App.Services;

/// <summary>
/// "Getrunken" und "Rückgängig" – gleich, ob aus der Benachrichtigung, dem Tray oder der
/// Startseite: Glas eintragen, Erinnerungen neu planen, offene Erinnerung ausblenden,
/// beim Erreichen des Tagesziels einmal am Tag gratulieren und neue Erfolge melden.
/// Speicherfehler des Glases (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>)
/// gehen an den Aufrufer; Speicherfehler der Erfolge landen nur im Protokoll, denn das Glas ist dann schon eingetragen.
/// </summary>
public sealed class DrinkActions(DrinkLog log, ReminderEngine reminders, NotificationService notifications,
    SettingsService settings, DailyGoalNotice goalNotice, AchievementTracker achievements, Action<Exception> logError)
{
    /// <summary>
    /// Abstand, wenn "Tagesziel erreicht" und ein Erfolg zugleich fällig sind: Gleichzeitig klingen
    /// zwei Töne schief, und Windows zeigt die zweite Meldung dann oft nur im Info-Center. So
    /// lange bleibt eine Benachrichtigung bei den Standardeinstellungen von Windows sichtbar.
    /// </summary>
    public static TimeSpan SecondNoticeDelay { get; } = TimeSpan.FromSeconds(6);

    /// <param name="fromReminder">"Getrunken" direkt in einer Erinnerung (zählt für "Aufs Wort").</param>
    public DrinkEntry AddGlass(bool fromReminder = false)
    {
        var entry = log.Add(settings.Current.GlassMl);
        reminders.OnDrank();
        notifications.RemoveReminder();

        var s = settings.Current;
        var goalShown = goalNotice.ShouldAnnounce(log.Entries, s.DailyGoalMl);
        if (goalShown)
            notifications.ShowGoalReached(s.DailyGoalMl, s.Sound(SoundOccasion.GoalReached));

        try
        {
            if (goalShown && goalNotice.AnnouncedOn is { } day) achievements.RecordGoalNotice(day);
            if (fromReminder) achievements.RecordOnCue();
            var unlocked = achievements.Check(log.Entries, s);
            if (unlocked.Count > 0)
            {
                var sound = s.Sound(SoundOccasion.Achievement);
                if (goalShown) _ = ShowAchievementsLaterAsync(unlocked, sound);
                else notifications.ShowAchievements(unlocked, sound);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logError(e);
        }
        return entry;
    }

    public DrinkEntry? Undo()
    {
        var removed = log.RemoveLast();
        reminders.Reschedule();
        return removed;
    }

    /// <summary>Erfolg nach "Tagesziel erreicht" melden, nicht gleichzeitig (siehe <see cref="SecondNoticeDelay"/>).</summary>
    private async Task ShowAchievementsLaterAsync(IReadOnlyList<Achievement> unlocked, string sound)
    {
        try
        {
            await Task.Delay(SecondNoticeDelay);
            notifications.ShowAchievements(unlocked, sound);
        }
        catch (Exception e)
        {
            // Läuft unbeobachtet weiter; ein Fehler darf die App nicht beenden. Der Erfolg ist ohnehin gespeichert.
            logError(e);
        }
    }
}
