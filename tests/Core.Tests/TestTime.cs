using Microsoft.Extensions.Time.Testing;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Tests;

/// <summary>Gemeinsame Hilfen: Berliner Zeitzone (mit Sommerzeit) und lokale Zeitpunkte.</summary>
internal static class TestTime
{
    public static TimeZoneInfo Berlin { get; } =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Berlin", out var tz) ? tz
            : TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    /// <summary>Lokale Berliner Zeit, z. B. At(2026, 10, 5, "09:30") – der 5.10.2026 ist ein Montag.</summary>
    public static DateTimeOffset At(int year, int month, int day, string hhmm) =>
        WorkCalendar.ToInstant(new DateOnly(year, month, day), TimeOnly.Parse(hhmm), Berlin);

    public static FakeTimeProvider Clock(DateTimeOffset start)
    {
        var clock = new FakeTimeProvider(start.ToUniversalTime());
        clock.SetLocalTimeZone(Berlin);
        return clock;
    }

    public static AppSettings Config(int intervalMin = 60) =>
        AppSettings.Defaults("de") with { IntervalMin = intervalMin, SetupDone = true };

    public static AppSettings WithDay(this AppSettings s, int index, bool enabled, string start = "08:00", string end = "17:00")
    {
        var days = s.WorkHours.ToArray();
        days[index] = new WorkDay(enabled, TimeOnly.Parse(start), TimeOnly.Parse(end));
        return s with { WorkHours = days };
    }
}
