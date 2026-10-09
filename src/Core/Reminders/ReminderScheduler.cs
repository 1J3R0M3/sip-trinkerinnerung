using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Reminders;

/// <param name="LastDrink">Letztes Glas: das Intervall startet danach neu.</param>
/// <param name="LastReminder">Letzte gezeigte Erinnerung: ohne Reaktion folgt die nächste ein Intervall später.</param>
/// <param name="SnoozeUntil">"In 10 Min. erinnern": ersetzt den nächsten regulären Termin.</param>
public readonly record struct ReminderState(DateTimeOffset? LastDrink, DateTimeOffset? LastReminder, DateTimeOffset? SnoozeUntil);

/// <summary>
/// Berechnet den nächsten Erinnerungstermin (reine Funktion, ohne Uhr und Timer):
/// <list type="bullet">
/// <item>Erinnert wird nur innerhalb der Arbeitszeit, ein Termin genau zum Arbeitsende entfällt.</item>
/// <item>Das Intervall zählt ab Arbeitsbeginn, dem letzten Glas oder der letzten Erinnerung – was zuletzt war.</item>
/// <item>Ist der Termin schon vorbei (Schlaf, Ruhezustand, App war beendet), wird er nicht nachgeholt:
///       die nächste Erinnerung kommt ein Intervall nach jetzt.</item>
/// <item>Nach Feierabend: nächster Arbeitstag zur Startzeit + Intervall.</item>
/// </list>
/// </summary>
public static class ReminderScheduler
{
    public static DateTimeOffset? Next(AppSettings settings, ReminderState state, DateTimeOffset now, TimeZoneInfo zone)
    {
        var interval = TimeSpan.FromMinutes(settings.IntervalMin);
        var today = WorkCalendar.LocalDate(now, zone);

        // Eine Woche plus ein Tag: so wird auch derselbe Wochentag der nächsten Woche erreicht.
        for (var i = 0; i <= 7; i++)
        {
            var date = today.AddDays(i);
            var day = WorkCalendar.For(settings, date);
            if (!day.Enabled) continue;

            var start = WorkCalendar.ToInstant(date, day.Start, zone);
            var end = WorkCalendar.ToInstant(date, day.End, zone);
            if (now >= end) continue;

            var anchor = start;
            if (i == 0)
            {
                if (state.SnoozeUntil is { } snooze && snooze > now && snooze >= start && snooze < end)
                    return snooze;
                if (state.LastDrink is { } drink && drink > anchor) anchor = drink;
                if (state.LastReminder is { } shown && shown > anchor) anchor = shown;
            }

            var candidate = anchor + interval;
            if (candidate <= now) candidate = now + interval;
            if (candidate < end) return candidate;
        }
        return null;
    }
}
