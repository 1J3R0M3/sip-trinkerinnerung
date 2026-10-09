namespace Trinkwasser.Core.Settings;

/// <summary>
/// Umrechnung zwischen Kalendertagen, Arbeitszeiten und Zeitpunkten. Alle Tage und Uhrzeiten
/// sind lokal (Zeitzone des Nutzers); gerechnet wird mit absoluten Zeitpunkten, damit
/// Intervalle über eine Zeitumstellung hinweg stimmen.
/// </summary>
public static class WorkCalendar
{
    /// <summary>0 = Montag … 6 = Sonntag (wie <see cref="AppSettings.WorkHours"/>).</summary>
    public static int DayIndex(DayOfWeek day) => ((int)day + 6) % 7;

    public static WorkDay For(AppSettings settings, DateOnly date) => settings.WorkHours[DayIndex(date.DayOfWeek)];

    /// <summary>Montag der Woche, in der <paramref name="date"/> liegt.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-DayIndex(date.DayOfWeek));

    public static DateOnly LocalDate(DateTimeOffset time, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, zone).DateTime);

    public static int LocalHour(DateTimeOffset time, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(time, zone).Hour;

    /// <summary>
    /// Lokale Uhrzeit an einem Tag als Zeitpunkt. Fällt sie in die übersprungene Stunde der
    /// Sommerzeit-Umstellung, gilt die erste gültige Zeit danach; gibt es sie bei der
    /// Winterzeit-Umstellung doppelt, gilt das erste Vorkommen.
    /// </summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(15);
        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
