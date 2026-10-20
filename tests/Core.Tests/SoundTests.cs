using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Reminders;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Sounds;
using static Trinkwasser.Core.Tests.TestTime;

namespace Trinkwasser.Core.Tests;

public class SoundSettingsTests
{
    private static readonly AppSettings Defaults = AppSettings.Defaults("de-DE");

    [Fact]
    public void EachOccasionHasItsOwnKnownDefault()
    {
        var defaults = Enum.GetValues<SoundOccasion>().Select(o => Defaults.Sound(o)).ToList();
        Assert.All(defaults, id => Assert.True(SoundCatalog.IsSipSound(id)));
        Assert.Equal(defaults.Count, defaults.Distinct().Count());
    }

    [Fact]
    public void SoundsSurviveSavingAndLoading()
    {
        var s = Defaults with { ReminderSound = SoundCatalog.Silent, SnoozeSound = SoundCatalog.Windows, GoalSound = "crystal" };
        var loaded = SettingsSanitizer.FromJson(SettingsSanitizer.ToJson(s), Defaults);
        Assert.Equal((SoundCatalog.Silent, SoundCatalog.Windows, "crystal"), (loaded.ReminderSound, loaded.SnoozeSound, loaded.GoalSound));
    }

    [Fact]
    public void SettingsFromBeforeSoundsGetTheDefaults()
    {
        // Einstellungsdatei aus 1.0/1.1: ohne "sounds".
        var loaded = SettingsSanitizer.FromJson("""{ "version": 1, "setupDone": true, "intervalMin": 30 }""", Defaults);
        Assert.Equal(30, loaded.IntervalMin);
        Assert.Equal((Defaults.ReminderSound, Defaults.SnoozeSound, Defaults.GoalSound), (loaded.ReminderSound, loaded.SnoozeSound, loaded.GoalSound));
    }

    [Fact]
    public void SettingsFromBeforeAchievementsGetTheAchievementDefault()
    {
        // Einstellungsdatei aus 1.2: Töne ohne "achievement".
        var loaded = SettingsSanitizer.FromJson("""{ "sounds": { "reminder": "plop", "snooze": "pour", "goal": "silent" } }""", Defaults);
        Assert.Equal("plop", loaded.ReminderSound);
        Assert.Equal(SoundCatalog.Default(SoundOccasion.Achievement), loaded.AchievementSound);
        Assert.Equal("windows", SettingsSanitizer.FromJson(SettingsSanitizer.ToJson(Defaults with { AchievementSound = "windows" }), Defaults).AchievementSound);
    }

    [Fact]
    public void UnknownSoundFallsBackPerOccasion()
    {
        var loaded = SettingsSanitizer.FromJson("""{ "sounds": { "reminder": "trompete", "snooze": 5, "goal": "silent" } }""", Defaults);
        Assert.Equal(Defaults.ReminderSound, loaded.ReminderSound);
        Assert.Equal(Defaults.SnoozeSound, loaded.SnoozeSound);
        Assert.Equal(SoundCatalog.Silent, loaded.GoalSound);
    }

    [Fact]
    public void SanitizeKeepsPreviousSoundForUnknownIds()
    {
        var previous = Defaults with { GoalSound = "pour" };
        var result = SettingsSanitizer.Sanitize(previous with { GoalSound = "gibt-es-nicht", ReminderSound = "plop" }, previous);
        Assert.Equal(("pour", "plop"), (result.GoalSound, result.ReminderSound));
    }

    [Fact]
    public void WithSoundChangesOnlyThatOccasion()
    {
        var s = Defaults.WithSound(SoundOccasion.Snooze, SoundCatalog.Silent);
        Assert.Equal(SoundCatalog.Silent, s.Sound(SoundOccasion.Snooze));
        Assert.Equal(Defaults.ReminderSound, s.Sound(SoundOccasion.Reminder));
        Assert.Equal(Defaults.GoalSound, s.Sound(SoundOccasion.GoalReached));
        Assert.NotEqual(Defaults, s);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void EverySoundAndOccasionHasAName(string language)
    {
        var loc = new Localizer(language, _ => "Sip");
        Assert.All(SoundCatalog.All, id => Assert.NotEqual("sound." + id, loc.SoundName(id)));
        Assert.All(Enum.GetValues<SoundOccasion>(), o => Assert.DoesNotContain("{", loc.SoundOccasionName(o)));
    }
}

public sealed class ReminderKindTests : IDisposable
{
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Clock(At(2026, 10, 5, "08:30"));
    private readonly List<ReminderKind> _kinds = [];
    private readonly ReminderEngine _engine;

    public ReminderKindTests()
    {
        _engine = new ReminderEngine(_clock, () => Config(), () => null);
        _engine.Due += (_, kind) => _kinds.Add(kind);
    }

    public void Dispose() => _engine.Dispose();

    private void RunFor(TimeSpan span)
    {
        var end = _clock.GetUtcNow() + span;
        while (_clock.GetUtcNow() < end)
        {
            _clock.Advance(ReminderEngine.Tick);
            _engine.Poll();
        }
    }

    [Fact]
    public void SnoozedReminderIsReportedAsSnoozeAndTheNextOneAsRegularAgain()
    {
        _engine.Start(withTimer: false);
        RunFor(TimeSpan.FromMinutes(31)); // 9:00 regulär
        _engine.Snooze();
        RunFor(TimeSpan.FromMinutes(11)); // 9:11 Snooze
        RunFor(TimeSpan.FromMinutes(60)); // 10:11 regulär

        Assert.Equal([ReminderKind.Regular, ReminderKind.Snooze, ReminderKind.Regular], _kinds);
    }
}

public class DailyGoalNoticeTests
{
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Clock(At(2026, 10, 5, "10:00"));
    private readonly List<DrinkEntry> _entries = [];
    private readonly DailyGoalNotice _notice;

    public DailyGoalNoticeTests() => _notice = new DailyGoalNotice(_clock);

    private bool Drink(int ml = 500, int goal = 2000)
    {
        _entries.Add(new DrinkEntry(_clock.GetUtcNow(), ml));
        _clock.Advance(TimeSpan.FromMinutes(5));
        return _notice.ShouldAnnounce(_entries, goal);
    }

    [Fact]
    public void AnnouncedExactlyWhenTheGlassCrossesTheGoal() =>
        Assert.Equal([false, false, false, true, false], [Drink(), Drink(), Drink(), Drink(), Drink()]);

    [Fact]
    public void UndoAndDrinkAgainDoesNotAnnounceTwice()
    {
        Drink(1500);
        Assert.True(Drink());
        _entries.RemoveAt(_entries.Count - 1);
        Assert.False(Drink());
    }

    [Fact]
    public void RememberedDayFromBeforeARestartIsNotAnnouncedAgain()
    {
        Drink(1500);
        Assert.True(Drink());
        Assert.Equal(new DateOnly(2026, 10, 5), _notice.AnnouncedOn);

        // Neustart: neue Instanz mit dem gespeicherten Tag; Rückgängig und neu eintragen.
        var restarted = new DailyGoalNotice(_clock, _notice.AnnouncedOn);
        _entries.RemoveAt(_entries.Count - 1);
        _entries.Add(new DrinkEntry(_clock.GetUtcNow(), 500));
        Assert.False(restarted.ShouldAnnounce(_entries, 2000));

        // Ein gespeicherter Tag von gestern hindert nicht.
        Assert.True(new DailyGoalNotice(_clock, new DateOnly(2026, 10, 4)).ShouldAnnounce(_entries, 2000));
    }

    [Fact]
    public void NextDayAnnouncesAgain()
    {
        Assert.True(Drink(2000));
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.True(Drink(2000));
    }

    [Fact]
    public void YesterdaysTotalDoesNotCount()
    {
        Drink(1900);
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.False(Drink(250));
    }

    [Fact]
    public void EmptyLogAnnouncesNothing() => Assert.False(_notice.ShouldAnnounce([], 2000));
}
