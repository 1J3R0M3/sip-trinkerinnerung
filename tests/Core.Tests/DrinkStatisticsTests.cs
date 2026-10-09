using Trinkwasser.Core.Drinks;
using static Trinkwasser.Core.Tests.TestTime;

namespace Trinkwasser.Core.Tests;

// Kalender: 5.10.2026 = Montag … 11.10. = Sonntag, 12.10. = Montag.
public class DrinkStatisticsTests
{
    private static DrinkEntry E(DateTimeOffset time, int ml = 250) => new(time, ml);

    /// <summary>Gläser à 250 ml an einem Tag ab 9:00 stündlich.</summary>
    private static IEnumerable<DrinkEntry> Glasses(int y, int m, int d, int count) =>
        Enumerable.Range(0, count).Select(i => E(At(y, m, d, $"{9 + i:00}:00")));

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void WeekStartsOnMonday()
    {
        Assert.Equal(D(2026, 10, 5), Trinkwasser.Core.Settings.WorkCalendar.WeekStart(D(2026, 10, 5)));
        Assert.Equal(D(2026, 10, 5), Trinkwasser.Core.Settings.WorkCalendar.WeekStart(D(2026, 10, 11)));
        Assert.Equal(D(2026, 10, 12), Trinkwasser.Core.Settings.WorkCalendar.WeekStart(D(2026, 10, 12)));
    }

    [Fact]
    public void WeekChangeAtMidnightBetweenSundayAndMonday()
    {
        var entries = new[] { E(At(2026, 10, 11, "23:59")), E(At(2026, 10, 12, "00:00"), 500) };
        var s = Config();

        var first = DrinkStatistics.ForWeek(entries, s, D(2026, 10, 5), D(2026, 10, 12), Berlin);
        var second = DrinkStatistics.ForWeek(entries, s, D(2026, 10, 12), D(2026, 10, 12), Berlin);

        Assert.Equal(250, first.TotalMl);
        Assert.Equal(250, first.Days[6].TotalMl);
        Assert.Equal(500, second.TotalMl);
        Assert.Equal(500, second.Days[0].TotalMl);
    }

    [Fact]
    public void DaysAreCountedInLocalTimeEvenIfStoredWithAnotherOffset()
    {
        // 23:30 UTC am Sonntag ist in Berlin schon Montag 01:30.
        var entries = new[] { E(new DateTimeOffset(2026, 10, 11, 23, 30, 0, TimeSpan.Zero)) };
        var week = DrinkStatistics.ForWeek(entries, Config(), D(2026, 10, 12), D(2026, 10, 12), Berlin);
        Assert.Equal(250, week.Days[0].TotalMl);
    }

    [Fact]
    public void WeekTotalsAverageAndGoalRate()
    {
        // Mo 2000, Di 1000, Mi 0 (Arbeitstag), heute = Mi; Sa/So frei ohne Einträge.
        var entries = Glasses(2026, 10, 5, 8).Concat(Glasses(2026, 10, 6, 4)).ToList();
        var week = DrinkStatistics.ForWeek(entries, Config(), D(2026, 10, 5), D(2026, 10, 7), Berlin);

        Assert.Equal(3000, week.TotalMl);
        Assert.Equal(1000, week.AverageMl);          // (2000 + 1000 + 0) / 3 Tage bis heute
        Assert.Equal(30, week.GoalPercent);          // 3000 von 10000
        Assert.Equal((1, 3), (week.DaysReached, week.DaysCounted));
        Assert.Equal([8, 4, 0, 0, 0, 0, 0], week.Days.Select(d => d.Glasses));
    }

    [Fact]
    public void FreeDayWithDrinksCountsForTheAverage()
    {
        var entries = Glasses(2026, 10, 10, 4).ToList(); // Samstag
        var week = DrinkStatistics.ForWeek(entries, Config(), D(2026, 10, 5), D(2026, 10, 11), Berlin);
        Assert.Equal(6, week.DaysCounted);           // Mo–Fr + Sa (mit Einträgen), Sonntag nicht
        Assert.Equal(167, week.AverageMl);
    }

    [Fact]
    public void NoAverageBeforeTheWeekStarted()
    {
        var week = DrinkStatistics.ForWeek([], Config(), D(2026, 10, 12), D(2026, 10, 11), Berlin);
        Assert.Null(week.AverageMl);
        Assert.Null(week.BestHour);
        Assert.Equal(0, week.GoalPercent);
    }

    [Fact]
    public void BestHourIsTheHourWithMostGlasses()
    {
        var entries = new[]
        {
            E(At(2026, 10, 5, "09:10")), E(At(2026, 10, 5, "14:05")), E(At(2026, 10, 6, "14:55"), 100),
            E(At(2026, 10, 7, "14:30")), E(At(2026, 10, 7, "09:45"), 2000),
        };
        // 14 Uhr: 3 Gläser, 9 Uhr: 2 Gläser (Menge zählt nicht).
        Assert.Equal(14, DrinkStatistics.BestHour(entries, Berlin));
    }

    [Fact]
    public void BestHourTieGoesToTheEarlierHour()
    {
        var entries = new[] { E(At(2026, 10, 5, "15:00")), E(At(2026, 10, 5, "10:30")) };
        Assert.Equal(10, DrinkStatistics.BestHour(entries, Berlin));
    }

    [Fact]
    public void BestHourUsesLocalTimeAcrossTheClockChange()
    {
        // 25.10.2026: 02:30 gibt es zweimal (MESZ, dann MEZ) – beides ist die Stunde 2.
        var entries = new[]
        {
            E(new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2))),
            E(new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(1))),
            E(At(2026, 10, 25, "09:00")),
        };
        Assert.Equal(2, DrinkStatistics.BestHour(entries, Berlin));
    }

    [Fact]
    public void StreakIsNotBrokenByTheWeekend()
    {
        // Do + Fr erreicht, Sa/So frei ohne Einträge, Mo erreicht → 3.
        var entries = Glasses(2026, 10, 8, 8).Concat(Glasses(2026, 10, 9, 8)).Concat(Glasses(2026, 10, 12, 8)).ToList();
        Assert.Equal(3, DrinkStatistics.Streak(entries, Config(), D(2026, 10, 12), Berlin));
    }

    [Fact]
    public void MissedWorkDayBreaksTheStreak()
    {
        // Mi erreicht, Do verfehlt (Arbeitstag), Fr erreicht → 1.
        var entries = Glasses(2026, 10, 7, 8).Concat(Glasses(2026, 10, 8, 3)).Concat(Glasses(2026, 10, 9, 8)).ToList();
        Assert.Equal(1, DrinkStatistics.Streak(entries, Config(), D(2026, 10, 9), Berlin));
    }

    [Fact]
    public void TodayInProgressDoesNotBreakTheStreak()
    {
        var entries = Glasses(2026, 10, 8, 8).Concat(Glasses(2026, 10, 9, 2)).ToList();
        Assert.Equal(1, DrinkStatistics.Streak(entries, Config(), D(2026, 10, 9), Berlin));
        entries.AddRange(Glasses(2026, 10, 9, 6).Select(e => e with { Time = e.Time.AddMinutes(30) }));
        Assert.Equal(2, DrinkStatistics.Streak(entries, Config(), D(2026, 10, 9), Berlin));
    }

    [Fact]
    public void FreeDayWithReachedGoalCountsForTheStreak()
    {
        var entries = Glasses(2026, 10, 9, 8).Concat(Glasses(2026, 10, 10, 8)).ToList(); // Fr + Sa
        Assert.Equal(2, DrinkStatistics.Streak(entries, Config(), D(2026, 10, 11), Berlin));
    }

    [Fact]
    public void StreakOverTheClockChangeWeekend()
    {
        // Fr 23.10. und Mo 26.10. erreicht; dazwischen Wochenende mit Zeitumstellung.
        var entries = Glasses(2026, 10, 23, 8).Concat(Glasses(2026, 10, 26, 8)).ToList();
        Assert.Equal(2, DrinkStatistics.Streak(entries, Config(), D(2026, 10, 26), Berlin));
    }

    [Theory]
    [InlineData(250, 2000, 13)] // 12,5 % → 13, nicht Banker's Rounding
    [InlineData(2000, 2000, 100)]
    [InlineData(3000, 2000, 150)]
    [InlineData(0, 2000, 0)]
    public void PercentRoundsHalfUp(int value, int goal, int expected) =>
        Assert.Equal(expected, DrinkStatistics.Percent(value, goal));

    [Fact]
    public void NoEntriesNoStreak() => Assert.Equal(0, DrinkStatistics.Streak([], Config(), D(2026, 10, 9), Berlin));
}
