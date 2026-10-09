using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Tests;

public class WorkHoursTests
{
    private static readonly IReadOnlyList<WorkDay> Defaults = AppSettings.DefaultWorkHours;

    private static TimeOnly T(string hhmm) => SettingsSanitizer.ParseTime(hhmm)!.Value;

    [Fact]
    public void DefaultsAreValid() => Assert.Equal(WorkHoursError.None, WorkHoursValidator.Validate(Defaults));

    [Fact]
    public void AtLeastOneWorkDayIsRequired() =>
        Assert.Equal(WorkHoursError.NoWorkDay,
            WorkHoursValidator.Validate([.. Defaults.Select(d => d with { Enabled = false })]));

    [Theory]
    [InlineData("17:00", "17:00")]
    [InlineData("17:00", "08:00")]
    [InlineData("22:00", "06:00")] // Schicht über Mitternacht: nicht unterstützt
    public void EndMustBeAfterStartOnActiveDays(string start, string end)
    {
        var days = Defaults.ToArray();
        days[2] = new WorkDay(true, T(start), T(end));
        Assert.Equal(WorkHoursError.TimeRange, WorkHoursValidator.Validate(days));
    }

    [Fact]
    public void InactiveDaysAreNotChecked()
    {
        var days = Defaults.ToArray();
        days[6] = new WorkDay(false, T("17:00"), T("08:00"));
        Assert.Equal(WorkHoursError.None, WorkHoursValidator.Validate(days));
    }

    [Fact]
    public void CopiesMondayOnlyToActiveDays()
    {
        var days = Defaults.ToArray();
        days[0] = new WorkDay(true, T("07:00"), T("15:30"));
        days[3] = new WorkDay(true, T("10:00"), T("12:00"));
        var result = WorkHoursValidator.CopyMondayToActiveDays(days);

        for (var i = 0; i < 5; i++) Assert.Equal(new WorkDay(true, T("07:00"), T("15:30")), result[i]);
        Assert.Equal(Defaults[5], result[5]);
        Assert.Equal(Defaults[6], result[6]);
    }

    [Theory]
    [InlineData(2000, 5, 10000)]
    [InlineData(2000, 1, 2000)]
    [InlineData(10000, 7, 70000)]
    [InlineData(2000.4, 3, 6000)]
    public void SuggestsWeeklyGoal(double daily, int days, int expected) =>
        Assert.Equal(expected, SettingsLimits.SuggestWeeklyGoal(daily, days));

    [Theory]
    [InlineData(2000, 0)]
    [InlineData(double.NaN, 5)]
    [InlineData(0, 5)]
    public void NoSuggestionWithoutWorkDaysOrDailyGoal(double daily, int days) =>
        Assert.Null(SettingsLimits.SuggestWeeklyGoal(daily, days));

    [Fact]
    public void IntervalChoicesKeepAnUnlistedValue()
    {
        Assert.Same(SettingsLimits.IntervalChoices, SettingsLimits.IntervalChoicesWith(60));
        Assert.Equal([15, 20, 30, 45, 60, 75, 90, 120, 180], SettingsLimits.IntervalChoicesWith(75));
        Assert.Equal(5, SettingsLimits.IntervalChoicesWith(5)[0]);
    }
}
