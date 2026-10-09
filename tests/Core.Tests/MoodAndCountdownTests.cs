using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Reminders;
using static Trinkwasser.Core.Tests.TestTime;

namespace Trinkwasser.Core.Tests;

// 5.10.2026 = Montag (Arbeitstag 8–17 Uhr), 10.10.2026 = Samstag (frei).
public class MoodAndCountdownTests
{
    private static Mood MoodAt(string time, int todayMl, int day = 5) =>
        HydrationMood.For(Config(), todayMl, At(2026, 10, day, time), Berlin);

    [Fact]
    public void GoalReachedBeatsEverything() => Assert.Equal(Mood.GoalReached, MoodAt("08:05", 2000));

    [Theory]
    [InlineData("07:00", 0, Mood.Okay)]    // vor der Arbeit: nichts aufzuholen
    [InlineData("08:30", 0, Mood.Okay)]    // Soll noch unter einem Glas
    [InlineData("08:30", 250, Mood.OnTrack)]
    [InlineData("12:30", 1000, Mood.OnTrack)] // Hälfte der Zeit, Hälfte des Ziels
    [InlineData("12:30", 750, Mood.Okay)]
    [InlineData("12:30", 500, Mood.Behind)]
    [InlineData("12:30", 250, Mood.Thirsty)]
    [InlineData("18:00", 1500, Mood.Okay)]   // nach Feierabend zählt das volle Ziel
    public void MoodFollowsTheExpectedProgressDuringWork(string time, int ml, Mood expected) =>
        Assert.Equal(expected, MoodAt(time, ml));

    [Fact]
    public void FreeDaysUseEightToEight()
    {
        // Samstag 14:00 = Hälfte von 8–20 Uhr → Soll 1000 ml.
        Assert.Equal(1000, HydrationMood.ExpectedMl(Config(), At(2026, 10, 10, "14:00"), Berlin), 1);
        Assert.Equal(Mood.OnTrack, MoodAt("14:00", 1000, day: 10));
    }

    [Fact]
    public void CountdownRunsFromFullToEmpty()
    {
        var start = At(2026, 10, 5, "09:00");
        var next = At(2026, 10, 5, "10:00");

        Assert.Equal(1, Countdown.Between(start, next, start).RemainingFraction);
        var half = Countdown.Between(start, next, At(2026, 10, 5, "09:30"));
        Assert.Equal(0.5, half.RemainingFraction, 3);
        Assert.Equal("30:00", half.Format());
        var over = Countdown.Between(start, next, At(2026, 10, 5, "10:00:05"));
        Assert.Equal((0, "00:00"), (over.RemainingFraction, over.Format()));
    }

    [Fact]
    public void CountdownShowsHoursWhenLong() =>
        Assert.Equal("2:05:09", Countdown.Between(At(2026, 10, 5, "08:00"), At(2026, 10, 5, "11:00"), At(2026, 10, 5, "08:54:51")).Format());
}
