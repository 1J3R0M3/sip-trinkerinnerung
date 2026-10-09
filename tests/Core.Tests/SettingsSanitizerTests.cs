using System.Text.Json;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Tests;

public class SettingsSanitizerTests
{
    private static readonly AppSettings Defaults = AppSettings.Defaults("de-DE");

    private static TimeOnly T(string hhmm) => SettingsSanitizer.ParseTime(hhmm)!.Value;

    [Fact]
    public void DefaultsMatchThePrototype()
    {
        Assert.Equal("de", Defaults.Language);
        Assert.Equal("en", AppSettings.Defaults("fr-FR").Language);
        Assert.False(Defaults.SetupDone);
        Assert.False(Defaults.Autostart);
        Assert.Equal((2000, 10000, 250, 60), (Defaults.DailyGoalMl, Defaults.WeeklyGoalMl, Defaults.GlassMl, Defaults.IntervalMin));
        Assert.Equal([true, true, true, true, true, false, false], Defaults.WorkHours.Select(d => d.Enabled));
        Assert.All(Defaults.WorkHours, d => Assert.Equal((T("08:00"), T("17:00")), (d.Start, d.End)));
    }

    [Theory]
    [InlineData(100, 250)]
    [InlineData(250, 250)]
    [InlineData(1499.5, 1500)]
    [InlineData(10000, 10000)]
    [InlineData(20000, 10000)]
    public void DailyGoalIsRoundedAndClamped(double input, int expected) =>
        Assert.Equal(expected, SettingsLimits.DailyGoalMl.Clamp(input, 2000));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidNumberKeepsPreviousValue(double input) =>
        Assert.Equal(1234, SettingsLimits.GlassMl.Clamp(input, 1234));

    [Fact]
    public void AllLimitsMatchTheRequirements()
    {
        Assert.Equal(new Limit(250, 10000), SettingsLimits.DailyGoalMl);
        Assert.Equal(new Limit(250, 70000), SettingsLimits.WeeklyGoalMl);
        Assert.Equal(new Limit(50, 2000), SettingsLimits.GlassMl);
        Assert.Equal(new Limit(5, 240), SettingsLimits.IntervalMin);
    }

    [Fact]
    public void SanitizeClampsTypedInput()
    {
        var input = Defaults with { DailyGoalMl = 1, WeeklyGoalMl = 999999, GlassMl = 5000, IntervalMin = 1 };
        var result = SettingsSanitizer.Sanitize(input, Defaults);
        Assert.Equal((250, 70000, 2000, 5), (result.DailyGoalMl, result.WeeklyGoalMl, result.GlassMl, result.IntervalMin));
    }

    [Fact]
    public void UnknownLanguageKeepsPrevious() =>
        Assert.Equal("de", SettingsSanitizer.Sanitize(Defaults with { Language = "fr" }, Defaults).Language);

    [Fact]
    public void WorkDayWithEndBeforeStartKeepsPreviousTimesButTakesEnabled()
    {
        var days = Defaults.WorkHours.ToArray();
        days[0] = new WorkDay(false, T("18:00"), T("09:00"));
        days[5] = new WorkDay(true, T("10:00"), T("10:00"));
        var result = SettingsSanitizer.Sanitize(Defaults with { WorkHours = days }, Defaults);

        Assert.Equal(new WorkDay(false, T("08:00"), T("17:00")), result.WorkHours[0]);
        Assert.Equal(new WorkDay(true, T("08:00"), T("17:00")), result.WorkHours[5]);
    }

    [Fact]
    public void WorkDayEndingOneMinuteAfterStartIsValid()
    {
        var days = Defaults.WorkHours.ToArray();
        days[2] = new WorkDay(true, T("00:00"), T("00:01"));
        days[3] = new WorkDay(true, T("06:30"), T("23:59"));
        var result = SettingsSanitizer.Sanitize(Defaults with { WorkHours = days }, Defaults);
        Assert.Equal(days[2], result.WorkHours[2]);
        Assert.Equal(days[3], result.WorkHours[3]);
    }

    [Fact]
    public void SecondsAreCutOffSoEqualMinutesAreInvalid()
    {
        var days = Defaults.WorkHours.ToArray();
        days[1] = new WorkDay(true, new TimeOnly(9, 0, 10), new TimeOnly(9, 0, 50));
        days[2] = new WorkDay(true, new TimeOnly(9, 0, 59), new TimeOnly(12, 30, 59));
        var result = SettingsSanitizer.Sanitize(Defaults with { WorkHours = days }, Defaults);
        Assert.Equal(Defaults.WorkHours[1], result.WorkHours[1]);
        Assert.Equal(new WorkDay(true, T("09:00"), T("12:30")), result.WorkHours[2]);
    }

    [Fact]
    public void WorkHoursWithWrongLengthAreIgnored()
    {
        var input = Defaults with { WorkHours = [.. Defaults.WorkHours.Take(5)] };
        Assert.Equal(Defaults.WorkHours, SettingsSanitizer.Sanitize(input, Defaults).WorkHours);
    }

    [Fact]
    public void JsonRoundTrip()
    {
        var days = Defaults.WorkHours.ToArray();
        days[6] = new WorkDay(true, T("09:15"), T("12:45"));
        var settings = Defaults with
        {
            SetupDone = true, Language = "en", DailyGoalMl = 2500, WeeklyGoalMl = 17500,
            GlassMl = 300, IntervalMin = 45, Autostart = true, WorkHours = days,
        };
        var json = SettingsSanitizer.ToJson(settings);
        Assert.Contains("\"start\": \"09:15\"", json);
        Assert.Equal(settings, SettingsSanitizer.FromJson(json, Defaults));
    }

    [Fact]
    public void ReadsThePrototypeFormat()
    {
        const string json = """
            {"version":1,"setupDone":true,"language":"en","dailyGoalMl":"1800","weeklyGoalMl":9000.4,
             "glassMl":330,"intervalMin":30,"autostart":false,
             "workHours":[{"enabled":true,"start":"07:30","end":"16:00"},{"enabled":true,"start":"08:00","end":"17:00"},
                          {"enabled":true,"start":"08:00","end":"17:00"},{"enabled":true,"start":"08:00","end":"17:00"},
                          {"enabled":true,"start":"08:00","end":"12:00"},{"enabled":false,"start":"08:00","end":"17:00"},
                          {"enabled":false,"start":"08:00","end":"17:00"}]}
            """;
        var s = SettingsSanitizer.FromJson(json, Defaults);
        Assert.True(s.SetupDone);
        Assert.Equal("en", s.Language);
        Assert.Equal((1800, 9000, 330, 30), (s.DailyGoalMl, s.WeeklyGoalMl, s.GlassMl, s.IntervalMin));
        Assert.Equal(new WorkDay(true, T("07:30"), T("16:00")), s.WorkHours[0]);
        Assert.Equal(T("12:00"), s.WorkHours[4].End);
    }

    [Fact]
    public void InvalidFieldsInFileFallBackToDefaults()
    {
        const string json = """
            {"setupDone":"yes","language":42,"dailyGoalMl":"viel","glassMl":null,"intervalMin":-3,
             "workHours":[{"enabled":true,"start":"8:00","end":"17:00"},null,{"enabled":true,"start":"25:00","end":"26:00"},
                          {"enabled":true,"start":"20:00","end":"06:00"},{},{"enabled":true},{"enabled":1}]}
            """;
        var s = SettingsSanitizer.FromJson(json, Defaults);
        Assert.False(s.SetupDone);
        Assert.Equal("de", s.Language);
        Assert.Equal(2000, s.DailyGoalMl);
        Assert.Equal(250, s.GlassMl);
        Assert.Equal(5, s.IntervalMin);
        Assert.Equal(new WorkDay(true, T("08:00"), T("17:00")), s.WorkHours[0]); // "8:00" ist kein HH:mm
        Assert.Equal(Defaults.WorkHours[1], s.WorkHours[1]);
        Assert.Equal(new WorkDay(true, T("08:00"), T("17:00")), s.WorkHours[2]);
        Assert.Equal(new WorkDay(true, T("08:00"), T("17:00")), s.WorkHours[3]); // über Mitternacht
        Assert.False(s.WorkHours[4].Enabled);
        Assert.True(s.WorkHours[5].Enabled);
        Assert.False(s.WorkHours[6].Enabled);
    }

    [Theory]
    [InlineData("{\"theme\":\"dark\"}", AppTheme.Dark)]
    [InlineData("{\"theme\":\"light\"}", AppTheme.Light)]
    [InlineData("{\"theme\":\"system\"}", AppTheme.System)]
    [InlineData("{\"theme\":\"Dark\"}", AppTheme.System)]
    [InlineData("{\"theme\":2}", AppTheme.System)]
    [InlineData("{}", AppTheme.System)]
    public void ThemeDefaultsToSystem(string json, AppTheme expected) =>
        Assert.Equal(expected, SettingsSanitizer.FromJson(json, Defaults).Theme);

    [Fact]
    public void ThemeRoundTripsAndInvalidValueKeepsPrevious()
    {
        var dark = Defaults with { Theme = AppTheme.Dark };
        Assert.Contains("\"theme\": \"dark\"", SettingsSanitizer.ToJson(dark));
        Assert.Equal(AppTheme.Dark, SettingsSanitizer.Sanitize(Defaults with { Theme = (AppTheme)42 }, dark).Theme);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{kaputt")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    public void BrokenJsonThrows(string json) =>
        Assert.ThrowsAny<JsonException>(() => SettingsSanitizer.FromJson(json, Defaults));

    [Theory]
    [InlineData("00:00", true)]
    [InlineData("23:59", true)]
    [InlineData("24:00", false)]
    [InlineData("8:00", false)]
    [InlineData("08:60", false)]
    [InlineData("08:00:00", false)]
    [InlineData(null, false)]
    public void ParsesOnlyHHmm(string? text, bool valid) =>
        Assert.Equal(valid, SettingsSanitizer.ParseTime(text).HasValue);
}
