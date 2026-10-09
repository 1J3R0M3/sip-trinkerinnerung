using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Reminders;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.App.Services;

/// <summary>
/// "Getrunken" und "Rückgängig" – gleich, ob aus der Benachrichtigung, dem Tray oder der
/// Startseite: Glas eintragen, Erinnerungen neu planen, offene Erinnerung ausblenden.
/// Speicherfehler (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>) gehen an den Aufrufer.
/// </summary>
public sealed class DrinkActions(DrinkLog log, ReminderEngine reminders, NotificationService notifications, SettingsService settings)
{
    public DrinkEntry AddGlass()
    {
        var entry = log.Add(settings.Current.GlassMl);
        reminders.OnDrank();
        notifications.RemoveReminder();
        return entry;
    }

    public DrinkEntry? Undo()
    {
        var removed = log.RemoveLast();
        reminders.Reschedule();
        return removed;
    }
}
