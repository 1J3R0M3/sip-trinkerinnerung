using Trinkwasser.Core.Reminders;
using Trinkwasser.Core.Settings;
using static Trinkwasser.Core.Tests.TestTime;

namespace Trinkwasser.Core.Tests;

// Kalender: 5.10.2026 = Montag, 9.10. = Freitag, 12.10. = Montag.
public class ReminderSchedulerTests
{
    private static DateTimeOffset? Next(AppSettings s, DateTimeOffset now, DateTimeOffset? drink = null,
        DateTimeOffset? shown = null, DateTimeOffset? snooze = null) =>
        ReminderScheduler.Next(s, new ReminderState(drink, shown, snooze), now, Berlin);

    [Fact]
    public void BeforeWorkTheFirstReminderIsStartPlusInterval() =>
        Assert.Equal(At(2026, 10, 5, "09:00"), Next(Config(), At(2026, 10, 5, "06:30")));

    [Fact]
    public void IntervalRestartsAfterEachDrink() =>
        Assert.Equal(At(2026, 10, 5, "11:15"), Next(Config(), At(2026, 10, 5, "10:20"), drink: At(2026, 10, 5, "10:15")));

    [Fact]
    public void WithoutReactionTheNextReminderFollowsOneIntervalLater() =>
        Assert.Equal(At(2026, 10, 5, "12:00"),
            Next(Config(), At(2026, 10, 5, "11:00"), drink: At(2026, 10, 5, "09:40"), shown: At(2026, 10, 5, "11:00")));

    [Fact]
    public void DrinkBeforeWorkDoesNotMoveTheFirstReminder() =>
        Assert.Equal(At(2026, 10, 5, "09:00"), Next(Config(), At(2026, 10, 5, "07:50"), drink: At(2026, 10, 5, "07:45")));

    [Fact]
    public void MissedReminderIsNotCaughtUp()
    {
        // Laptop war von 10:00 bis 13:30 zugeklappt: keine sofortige Erinnerung, sondern ein Intervall später.
        var next = Next(Config(), At(2026, 10, 5, "13:30"), shown: At(2026, 10, 5, "10:00"));
        Assert.Equal(At(2026, 10, 5, "14:30"), next);
    }

    [Fact]
    public void MissedReminderCountsFromSinceSoTheTermDoesNotWander()
    {
        // App um 10:30 gestartet, ohne Glas: der Termin bleibt bei 11:30, auch wenn später neu geplant wird.
        var since = At(2026, 10, 5, "10:30");
        var state = new ReminderState(null, null, null, since);
        Assert.Equal(At(2026, 10, 5, "11:30"), ReminderScheduler.Next(Config(), state, At(2026, 10, 5, "10:30:20"), Berlin));
        Assert.Equal(At(2026, 10, 5, "11:30"), ReminderScheduler.Next(Config(), state, At(2026, 10, 5, "11:29:40"), Berlin));
    }

    [Theory]
    [InlineData("15:59", "16:59")] // letzte Erinnerung kurz vor Feierabend
    [InlineData("16:00", null)]    // fiele genau auf 17:00 = Arbeitsende → entfällt
    [InlineData("16:30", null)]
    public void NoReminderAtOrAfterTheEndOfWork(string drink, string? expectedToday)
    {
        var next = Next(Config(), At(2026, 10, 5, drink), drink: At(2026, 10, 5, drink));
        Assert.Equal(expectedToday is null ? At(2026, 10, 6, "09:00") : At(2026, 10, 5, expectedToday), next);
    }

    [Fact]
    public void AfterWorkTheNextWorkDayStartsAtStartPlusInterval() =>
        Assert.Equal(At(2026, 10, 6, "09:00"), Next(Config(), At(2026, 10, 5, "17:00")));

    [Fact]
    public void WeekendIsSkipped() =>
        Assert.Equal(At(2026, 10, 12, "09:00"), Next(Config(), At(2026, 10, 9, "18:00")));

    [Fact]
    public void SameWeekdayNextWeekIsFoundWhenItIsTheOnlyWorkDay()
    {
        var s = Config();
        for (var i = 1; i < 7; i++) s = s.WithDay(i, enabled: false);
        Assert.Equal(At(2026, 10, 12, "09:00"), Next(s, At(2026, 10, 5, "17:30")));
    }

    [Fact]
    public void NoWorkDaysMeansNoReminder()
    {
        var s = Config() with { WorkHours = [.. AppSettings.DefaultWorkHours.Select(d => d with { Enabled = false })] };
        Assert.Null(Next(s, At(2026, 10, 5, "09:00")));
    }

    [Fact]
    public void IntervalLongerThanTheWorkDayNeverReminds()
    {
        var s = Config(intervalMin: 90);
        for (var i = 0; i < 7; i++) s = s.WithDay(i, enabled: true, "08:00", "09:00");
        Assert.Null(Next(s, At(2026, 10, 5, "07:00")));
    }

    [Fact]
    public void SnoozeReplacesTheRegularReminder() =>
        Assert.Equal(At(2026, 10, 5, "10:10"),
            Next(Config(), At(2026, 10, 5, "10:00"), shown: At(2026, 10, 5, "10:00"), snooze: At(2026, 10, 5, "10:10")));

    [Fact]
    public void SnoozeAfterTheEndOfWorkIsDropped() =>
        Assert.Equal(At(2026, 10, 6, "09:00"),
            Next(Config(), At(2026, 10, 5, "16:55"), shown: At(2026, 10, 5, "16:55"), snooze: At(2026, 10, 5, "17:05")));

    [Fact]
    public void IndividualTimesPerWeekday()
    {
        var s = Config(intervalMin: 30).WithDay(1, enabled: true, "12:00", "20:00");
        Assert.Equal(At(2026, 10, 6, "12:30"), Next(s, At(2026, 10, 5, "17:00")));
    }

    // Zeitumstellung: 29.3.2026 (Sommerzeit, 2:00 → 3:00) und 25.10.2026 (Winterzeit, 3:00 → 2:00), beides Sonntage.
    [Fact]
    public void IntervalCountsRealTimeOverTheSpringForwardChange()
    {
        var s = Config().WithDay(6, enabled: true, "01:00", "06:00");
        var next = Next(s, At(2026, 3, 29, "00:30"));
        // 01:00 MEZ + 60 Minuten = 03:00 MESZ (die Stunde 2:00–3:00 gibt es nicht).
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 3, 0, 0, TimeSpan.FromHours(2)), next);
    }

    [Fact]
    public void IntervalCountsRealTimeOverTheFallBackChange()
    {
        var s = Config().WithDay(6, enabled: true, "01:00", "06:00");
        var shown = new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2)); // erstes 02:30 (MESZ)
        var next = Next(s, shown, shown: shown);
        // Eine echte Stunde später ist es wieder 02:30, jetzt MEZ.
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(1)), next);
    }

    [Fact]
    public void WorkStartInsideTheSkippedHourMovesToTheNextValidTime()
    {
        var s = Config(intervalMin: 30).WithDay(6, enabled: true, "02:30", "06:00");
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 3, 30, 0, TimeSpan.FromHours(2)), Next(s, At(2026, 3, 28, "20:00")));
    }

    [Fact]
    public void WorkDaysAfterTheChangeKeepTheirLocalTimes()
    {
        // Freitag vor der Umstellung um 18:00, nächster Arbeitstag Montag 26.10. (MEZ).
        var next = Next(Config(), At(2026, 10, 23, "18:00"));
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(1)), next);
    }
}
