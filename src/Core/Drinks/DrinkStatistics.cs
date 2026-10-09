using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Drinks;

/// <summary>Ein Tag im Wochenverlauf.</summary>
public sealed record DayStat(DateOnly Date, int TotalMl, int Glasses, bool IsWorkDay, bool GoalReached);

/// <summary>Auswertung einer Woche (Montag bis Sonntag).</summary>
public sealed record WeekStats(
    DateOnly WeekStart,
    IReadOnlyList<DayStat> Days,
    int TotalMl,
    int WeeklyGoalMl,
    int DailyGoalMl,
    int? AverageMl,
    int? BestHour,
    int GoalPercent,
    int DaysReached,
    int DaysCounted);

/// <summary>
/// Statistiken aus dem Trink-Protokoll. Tage werden nach lokaler Zeit gezählt.
/// Gewertet werden die aktuellen Ziele und Arbeitstage (frühere Einstellungen werden nicht gespeichert).
/// </summary>
public static class DrinkStatistics
{
    public static int TotalMl(IEnumerable<DrinkEntry> entries, DateOnly from, DateOnly to, TimeZoneInfo zone) =>
        entries.Where(e => WorkCalendar.LocalDate(e.Time, zone) is var d && d >= from && d <= to).Sum(e => e.AmountMl);

    public static int DayTotalMl(IEnumerable<DrinkEntry> entries, DateOnly date, TimeZoneInfo zone) =>
        TotalMl(entries, date, date, zone);

    /// <summary>
    /// Woche ab <paramref name="weekStart"/> (Montag). Durchschnitt und Zielerreichung zählen nur
    /// Tage bis einschließlich <paramref name="today"/>, und davon nur Arbeitstage sowie Tage, an
    /// denen etwas getrunken wurde – ein freier Sonntag ohne Einträge drückt den Schnitt nicht.
    /// </summary>
    public static WeekStats ForWeek(IReadOnlyList<DrinkEntry> entries, AppSettings settings, DateOnly weekStart, DateOnly today, TimeZoneInfo zone)
    {
        var weekEnd = weekStart.AddDays(6);
        var inWeek = entries.Select(e => (Entry: e, Date: WorkCalendar.LocalDate(e.Time, zone)))
            .Where(x => x.Date >= weekStart && x.Date <= weekEnd)
            .ToList();

        var days = Enumerable.Range(0, 7).Select(i =>
        {
            var date = weekStart.AddDays(i);
            var ofDay = inWeek.Where(x => x.Date == date).ToList();
            var total = ofDay.Sum(x => x.Entry.AmountMl);
            return new DayStat(date, total, ofDay.Count, WorkCalendar.For(settings, date).Enabled, total >= settings.DailyGoalMl);
        }).ToList();

        var counted = days.Where(d => d.Date <= today && (d.IsWorkDay || d.TotalMl > 0)).ToList();
        var total = days.Sum(d => d.TotalMl);

        return new WeekStats(
            weekStart,
            days,
            total,
            settings.WeeklyGoalMl,
            settings.DailyGoalMl,
            counted.Count > 0 ? (int)Math.Round(counted.Average(d => d.TotalMl), MidpointRounding.AwayFromZero) : null,
            BestHour(inWeek.Select(x => x.Entry), zone),
            Percent(total, settings.WeeklyGoalMl),
            counted.Count(d => d.GoalReached),
            counted.Count);
    }

    /// <summary>Anteil in Prozent, kaufmännisch gerundet (250 von 2000 → 13).</summary>
    public static int Percent(int value, int goal) =>
        goal > 0 ? (int)Math.Round(value * 100.0 / goal, MidpointRounding.AwayFromZero) : 0;

    /// <summary>Stunde (0–23) mit den meisten Gläsern; bei Gleichstand die frühere. Null ohne Einträge.</summary>
    public static int? BestHour(IEnumerable<DrinkEntry> entries, TimeZoneInfo zone) =>
        entries.GroupBy(e => WorkCalendar.LocalHour(e.Time, zone))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => (int?)g.Key)
            .FirstOrDefault();

    /// <summary>
    /// Anzahl Tage in Folge mit erreichtem Tagesziel, rückwärts ab heute. Heute zählt mit, wenn das
    /// Ziel schon erreicht ist, sonst bricht es die Serie (noch) nicht. Tage ohne Arbeitszeit
    /// unterbrechen die Serie nicht; wird an ihnen das Ziel erreicht, zählen sie mit.
    /// </summary>
    public static int Streak(IReadOnlyList<DrinkEntry> entries, AppSettings settings, DateOnly today, TimeZoneInfo zone)
    {
        if (entries.Count == 0) return 0;
        var totals = entries.GroupBy(e => WorkCalendar.LocalDate(e.Time, zone))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.AmountMl));
        var first = totals.Keys.Min();

        var streak = 0;
        for (var date = today; date >= first; date = date.AddDays(-1))
        {
            var reached = totals.GetValueOrDefault(date) >= settings.DailyGoalMl;
            if (reached) streak++;
            else if (date != today && WorkCalendar.For(settings, date).Enabled) break;
        }
        return streak;
    }
}
