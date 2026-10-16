using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Reminders;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Sounds;

namespace Trinkwasser.App.Services;

/// <summary>
/// "Getrunken" und "Rückgängig" – gleich, ob aus der Benachrichtigung, dem Tray oder der
/// Startseite: Glas eintragen, Erinnerungen neu planen, offene Erinnerung ausblenden und
/// beim Erreichen des Tagesziels einmal am Tag gratulieren.
/// Speicherfehler (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>) gehen an den Aufrufer.
/// </summary>
public sealed class DrinkActions(DrinkLog log, ReminderEngine reminders, NotificationService notifications,
    SettingsService settings, DailyGoalNotice goalNotice)
{
    public DrinkEntry AddGlass()
    {
        var entry = log.Add(settings.Current.GlassMl);
        reminders.OnDrank();
        notifications.RemoveReminder();

        var s = settings.Current;
        if (goalNotice.ShouldAnnounce(log.Entries, s.DailyGoalMl))
            notifications.ShowGoalReached(s.DailyGoalMl, s.Sound(SoundOccasion.GoalReached));
        return entry;
    }

    public DrinkEntry? Undo()
    {
        var removed = log.RemoveLast();
        reminders.Reschedule();
        return removed;
    }
}
