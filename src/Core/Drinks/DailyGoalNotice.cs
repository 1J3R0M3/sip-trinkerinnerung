using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Drinks;

/// <summary>
/// Entscheidet, ob nach einem eingetragenen Glas "Tagesziel erreicht" gemeldet wird: nur wenn
/// genau dieses Glas das Ziel überschreitet und höchstens einmal am Tag. Wer ein Glas
/// rückgängig macht und wieder einträgt, bekommt die Meldung nicht noch einmal
/// (gemerkt wird das nur im Speicher; nach einem Neustart der App wäre sie wieder möglich).
/// </summary>
public sealed class DailyGoalNotice(TimeProvider clock)
{
    private readonly Lock _lock = new();
    private DateOnly? _announcedOn;

    /// <param name="entries">Protokoll nach dem Eintragen, das neue Glas ist der letzte Eintrag.</param>
    public bool ShouldAnnounce(IReadOnlyList<DrinkEntry> entries, int dailyGoalMl)
    {
        if (entries is not [.., var added]) return false;
        var zone = clock.LocalTimeZone;
        var today = WorkCalendar.LocalDate(clock.GetUtcNow(), zone);
        if (WorkCalendar.LocalDate(added.Time, zone) != today) return false;

        var after = DrinkStatistics.DayTotalMl(entries, today, zone);
        var before = after - added.AmountMl;
        if (before >= dailyGoalMl || after < dailyGoalMl) return false;

        lock (_lock)
        {
            if (_announcedOn == today) return false;
            _announcedOn = today;
            return true;
        }
    }
}
