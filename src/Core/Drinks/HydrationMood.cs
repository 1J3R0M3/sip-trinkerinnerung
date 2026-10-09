using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Drinks;

/// <summary>Stimmung des Smileys auf der Startseite, von schlecht nach gut.</summary>
public enum Mood
{
    Thirsty,
    Behind,
    Okay,
    OnTrack,
    GoalReached,
}

/// <summary>
/// Bewertet nicht nur den Anteil am Tagesziel, sondern ob man für die Uhrzeit im Plan liegt:
/// Das Soll steigt über die Arbeitszeit gleichmäßig von 0 auf das Tagesziel. An Tagen ohne
/// Arbeitszeit gilt 8–20 Uhr. Um 9 Uhr ohne Glas ist man also nicht "durstig", um 15 Uhr schon.
/// </summary>
public static class HydrationMood
{
    private static readonly TimeOnly FreeDayStart = new(8, 0);
    private static readonly TimeOnly FreeDayEnd = new(20, 0);

    public static Mood For(AppSettings settings, int todayMl, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (todayMl >= settings.DailyGoalMl) return Mood.GoalReached;

        var expected = ExpectedMl(settings, now, zone);
        // Zu Beginn des Tages (Soll unter einem Glas) gibt es nichts aufzuholen.
        if (expected < settings.GlassMl) return todayMl > 0 ? Mood.OnTrack : Mood.Okay;

        var ratio = todayMl / expected;
        return ratio switch
        {
            >= 1 => Mood.OnTrack,
            >= 0.75 => Mood.Okay,
            >= 0.4 => Mood.Behind,
            _ => Mood.Thirsty,
        };
    }

    /// <summary>Soll bis jetzt: Tagesziel × vergangener Anteil des Zeitfensters.</summary>
    public static double ExpectedMl(AppSettings settings, DateTimeOffset now, TimeZoneInfo zone)
    {
        var date = WorkCalendar.LocalDate(now, zone);
        var day = WorkCalendar.For(settings, date);
        var (from, to) = day.Enabled ? (day.Start, day.End) : (FreeDayStart, FreeDayEnd);
        var start = WorkCalendar.ToInstant(date, from, zone);
        var end = WorkCalendar.ToInstant(date, to, zone);
        var fraction = Math.Clamp((now - start) / (end - start), 0, 1);
        return settings.DailyGoalMl * fraction;
    }
}
