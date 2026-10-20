using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Achievements;

/// <summary>
/// Stand eines Erfolgs. <see cref="Current"/> und <see cref="Target"/> sind in derselben
/// Einheit (bei <see cref="AchievementUnit.Liters"/> in ml, angezeigt wird in Litern).
/// </summary>
public sealed record AchievementProgress(Achievement Achievement, int Current, int Target, DateTimeOffset? UnlockedAt)
{
    public bool IsUnlocked => UnlockedAt is not null;

    /// <summary>Ziel erreicht, aber noch nicht als freigeschaltet gespeichert.</summary>
    public bool IsReached => Current >= Target;
}

/// <summary>
/// Berechnet den Fortschritt aller Erfolge aus dem Protokoll. Es zählen nur Gläser ab
/// <see cref="AchievementState.Since"/>; Tage und Wochen werden wie in den Statistiken in
/// lokaler Zeit gezählt und mit den aktuellen Zielen und Arbeitstagen gewertet.
/// </summary>
public static class AchievementEvaluator
{
    public static IReadOnlyList<AchievementProgress> Evaluate(
        IReadOnlyList<DrinkEntry> log, AchievementState state, AppSettings settings, DateTimeOffset now, TimeZoneInfo zone)
    {
        var entries = log.Where(e => e.Time >= state.Since).ToList();
        var today = WorkCalendar.LocalDate(now, zone);
        var totals = entries.GroupBy(e => WorkCalendar.LocalDate(e.Time, zone))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.AmountMl));
        int DayTotal(DateOnly date) => totals.GetValueOrDefault(date);

        var totalMl = entries.Sum(e => e.AmountMl);
        var streak = DrinkStatistics.Streak(entries, settings, today, zone);
        var weekStart = WorkCalendar.WeekStart(today);
        var weekTotal = Enumerable.Range(0, 7).Sum(i => DayTotal(weekStart.AddDays(i)));

        return [.. AchievementCatalog.All.Select(a =>
        {
            var (current, target) = a.Id switch
            {
                AchievementCatalog.FirstGlass => (entries.Count, 1),
                // Fortschritt: heute; erreicht, sobald es an irgendeinem Tag geklappt hat.
                AchievementCatalog.FirstDailyGoal => totals.Values.Any(t => t >= settings.DailyGoalMl)
                    ? (settings.DailyGoalMl, settings.DailyGoalMl)
                    : (DayTotal(today), settings.DailyGoalMl),
                AchievementCatalog.FirstWeeklyGoal => AnyWeekReached(totals, settings.WeeklyGoalMl)
                    ? (settings.WeeklyGoalMl, settings.WeeklyGoalMl)
                    : (weekTotal, settings.WeeklyGoalMl),
                AchievementCatalog.PerfectWeek => PerfectWeek(totals, settings, weekStart),
                AchievementCatalog.OnCue => (state.OnCueCount, AchievementCatalog.OnCueTarget),
                AchievementCatalog.EarlyBird => (EarlyBirdDays(entries, settings, zone), AchievementCatalog.EarlyBirdTarget),
                _ => a.Category switch
                {
                    AchievementCategory.Volume => (totalMl, a.Target * 1000),
                    AchievementCategory.Streak => (streak, a.Target),
                    _ => (0, Math.Max(1, a.Target)),
                },
            };
            var unlockedAt = state.Unlocked.TryGetValue(a.Id, out var at) ? at : (DateTimeOffset?)null;
            return new AchievementProgress(a, Math.Min(current, target), target, unlockedAt);
        })];
    }

    /// <summary>Erfolge, deren Ziel erreicht ist, die aber noch nicht freigeschaltet sind.</summary>
    public static IReadOnlyList<Achievement> NewlyReached(IEnumerable<AchievementProgress> progress) =>
        [.. progress.Where(p => !p.IsUnlocked && p.IsReached).Select(p => p.Achievement)];

    private static bool AnyWeekReached(Dictionary<DateOnly, int> totals, int weeklyGoalMl) =>
        totals.GroupBy(t => WorkCalendar.WeekStart(t.Key)).Any(w => w.Sum(t => t.Value) >= weeklyGoalMl);

    /// <summary>
    /// Eine Woche (Mo–So), in der an jedem Arbeitstag das Tagesziel erreicht wurde. Fortschritt:
    /// erreichte Arbeitstage der laufenden Woche.
    /// </summary>
    private static (int Current, int Target) PerfectWeek(Dictionary<DateOnly, int> totals, AppSettings settings, DateOnly thisWeek)
    {
        var workDays = Math.Max(1, settings.WorkDayCount);
        int Reached(DateOnly weekStart) => Enumerable.Range(0, 7)
            .Select(weekStart.AddDays)
            .Count(d => WorkCalendar.For(settings, d).Enabled && totals.GetValueOrDefault(d) >= settings.DailyGoalMl);

        var anyWeek = totals.Keys.Select(WorkCalendar.WeekStart).Distinct().Any(w => Reached(w) >= workDays);
        return anyWeek ? (workDays, workDays) : (Reached(thisWeek), workDays);
    }

    /// <summary>
    /// Arbeitstage, an denen das erste Glas spätestens eine Stunde nach Arbeitsbeginn getrunken
    /// wurde (früher, auch vor Arbeitsbeginn, zählt ebenso).
    /// </summary>
    private static int EarlyBirdDays(List<DrinkEntry> entries, AppSettings settings, TimeZoneInfo zone) =>
        entries.GroupBy(e => WorkCalendar.LocalDate(e.Time, zone)).Count(day =>
        {
            var work = WorkCalendar.For(settings, day.Key);
            if (!work.Enabled) return false;
            var deadline = WorkCalendar.ToInstant(day.Key, work.Start, zone) + AchievementCatalog.EarlyBirdWindow;
            return day.Min(e => e.Time) <= deadline;
        });
}
