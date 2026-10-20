using System.Collections.Immutable;
using System.Text.Json;
using Trinkwasser.Core.Achievements;
using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Storage;
using static Trinkwasser.Core.Tests.TestTime;

namespace Trinkwasser.Core.Tests;

// Kalender: 5.10.2026 = Montag … 11.10. = Sonntag. Standard: Tagesziel 2000 ml, Wochenziel 10000 ml, Mo–Fr 08–17 Uhr.
public class AchievementEvaluatorTests
{
    private static readonly AchievementState FromMonday = AchievementState.StartingAt(At(2026, 10, 5, "00:00"));

    private static DrinkEntry E(DateTimeOffset time, int ml = 250) => new(time, ml);

    /// <summary>Tagesziel an einem Tag: 2000 ml um 9:00.</summary>
    private static DrinkEntry Goal(int day, int month = 10) => E(At(2026, month, day, "09:00"), 2000);

    private static AchievementProgress Of(IReadOnlyList<DrinkEntry> entries, string id, DateTimeOffset now,
        AchievementState? state = null, AppSettings? settings = null) =>
        AchievementEvaluator.Evaluate(entries, state ?? FromMonday, settings ?? Config(), now, Berlin)
            .Single(p => p.Achievement.Id == id);

    [Fact]
    public void GlassesBeforeTheStartDoNotCount()
    {
        var state = AchievementState.StartingAt(At(2026, 10, 7, "12:00"));
        var entries = new[] { E(At(2026, 10, 6, "10:00"), 10000), E(At(2026, 10, 7, "11:59"), 2000) };
        var now = At(2026, 10, 7, "13:00");

        Assert.False(Of(entries, AchievementCatalog.FirstGlass, now, state).IsReached);
        Assert.False(Of(entries, AchievementCatalog.FirstDailyGoal, now, state).IsReached);
        Assert.Equal(0, Of(entries, AchievementCatalog.VolumeId(10), now, state).Current);
    }

    [Fact]
    public void FirstGlassNeedsOneGlass()
    {
        var now = At(2026, 10, 5, "10:00");
        Assert.False(Of([], AchievementCatalog.FirstGlass, now).IsReached);
        Assert.True(Of([E(At(2026, 10, 5, "09:30"))], AchievementCatalog.FirstGlass, now).IsReached);
    }

    [Fact]
    public void DailyGoalShowsTodayAndStaysReachedFromAnEarlierDay()
    {
        var now = At(2026, 10, 7, "10:00");
        var today = Of([E(At(2026, 10, 7, "09:00"), 1500)], AchievementCatalog.FirstDailyGoal, now);
        Assert.Equal((1500, 2000, false), (today.Current, today.Target, today.IsReached));

        var earlier = Of([Goal(5), E(At(2026, 10, 7, "09:00"))], AchievementCatalog.FirstDailyGoal, now);
        Assert.True(earlier.IsReached);
    }

    [Fact]
    public void WeeklyGoalCountsMondayToSunday()
    {
        // 9750 ml in der Woche 5.–11.10., die nächsten 250 ml erst am Montag danach.
        var entries = new[] { E(At(2026, 10, 5, "09:00"), 5000), E(At(2026, 10, 11, "20:00"), 4750), E(At(2026, 10, 12, "08:00")) };
        var week = Of(entries, AchievementCatalog.FirstWeeklyGoal, At(2026, 10, 12, "09:00"));
        Assert.False(week.IsReached);
        Assert.Equal(250, week.Current);

        var reached = Of([.. entries, E(At(2026, 10, 11, "21:00"))], AchievementCatalog.FirstWeeklyGoal, At(2026, 10, 12, "09:00"));
        Assert.True(reached.IsReached);
    }

    [Fact]
    public void VolumeIsReachedExactlyAtTheLimit()
    {
        var now = At(2026, 10, 9, "12:00");
        var almost = new[] { E(At(2026, 10, 5, "09:00"), 5000), E(At(2026, 10, 6, "09:00"), 4999) };
        var p = Of(almost, AchievementCatalog.VolumeId(10), now);
        Assert.Equal((9999, 10000, false), (p.Current, p.Target, p.IsReached));

        Assert.True(Of([.. almost, E(At(2026, 10, 9, "09:00"), 1)], AchievementCatalog.VolumeId(10), now).IsReached);
    }

    [Fact]
    public void StreakAcrossAWeekendWithoutWork()
    {
        // Fr, (Sa/So frei, nichts getrunken), Mo → 2 Tage; Di dazu → 3 Tage.
        var entries = new List<DrinkEntry> { Goal(9), Goal(12) };
        Assert.Equal(2, Of(entries, AchievementCatalog.StreakId(3), At(2026, 10, 12, "18:00")).Current);

        entries.Add(Goal(13));
        Assert.True(Of(entries, AchievementCatalog.StreakId(3), At(2026, 10, 13, "18:00")).IsReached);
    }

    [Fact]
    public void StreakBreaksOnAMissedWorkDay()
    {
        var entries = new[] { Goal(5), Goal(6), Goal(8) };   // Mi 7.10. verpasst
        Assert.Equal(1, Of(entries, AchievementCatalog.StreakId(3), At(2026, 10, 8, "18:00")).Current);
    }

    [Fact]
    public void PerfectWeekNeedsEveryWorkDay()
    {
        var fourDays = new[] { Goal(5), Goal(6), Goal(7), Goal(8) };
        var p = Of(fourDays, AchievementCatalog.PerfectWeek, At(2026, 10, 9, "08:00"));
        Assert.Equal((4, 5, false), (p.Current, p.Target, p.IsReached));

        // Freitag dazu: erreicht, auch wenn Sa/So nichts getrunken wird.
        Assert.True(Of([.. fourDays, Goal(9)], AchievementCatalog.PerfectWeek, At(2026, 10, 9, "10:00")).IsReached);
        // Und bleibt es in der nächsten Woche.
        Assert.True(Of([.. fourDays, Goal(9)], AchievementCatalog.PerfectWeek, At(2026, 10, 14, "10:00")).IsReached);
    }

    [Fact]
    public void PerfectWeekCannotStartInTheMiddleOfAWeek()
    {
        // Gezählt wird erst ab Mittwoch: Montag und Dienstag fehlen in dieser Woche.
        var state = AchievementState.StartingAt(At(2026, 10, 7, "00:00"));
        var entries = new[] { Goal(5), Goal(6), Goal(7), Goal(8), Goal(9) };
        Assert.False(Of(entries, AchievementCatalog.PerfectWeek, At(2026, 10, 9, "10:00"), state).IsReached);
    }

    [Fact]
    public void EarlyBirdCountsFirstGlassUpToOneHourAfterStart()
    {
        var entries = new[]
        {
            E(At(2026, 10, 5, "09:00")),  // Mo: genau eine Stunde nach 8:00 → zählt
            E(At(2026, 10, 6, "09:01")),  // Di: zu spät
            E(At(2026, 10, 7, "07:30")),  // Mi: vor Arbeitsbeginn → zählt
            E(At(2026, 10, 7, "09:30")),
            E(At(2026, 10, 10, "08:15")), // Sa: kein Arbeitstag
        };
        var p = Of(entries, AchievementCatalog.EarlyBird, At(2026, 10, 10, "12:00"));
        Assert.Equal((2, 5), (p.Current, p.Target));
    }

    [Fact]
    public void EarlyBirdUsesTheWorkStartOfEachDay()
    {
        var settings = Config().WithDay(1, true, "10:00", "18:00");   // Dienstag ab 10:00
        var p = Of([E(At(2026, 10, 6, "10:45"))], AchievementCatalog.EarlyBird, At(2026, 10, 6, "12:00"), settings: settings);
        Assert.Equal(1, p.Current);
    }

    [Fact]
    public void EarlyBirdOnTheDayTheClocksGoBack()
    {
        // 25.10.2026: Ende der Sommerzeit. Sonntag als Arbeitstag ab 8:00 – 9:00 Winterzeit zählt noch.
        var settings = Config().WithDay(6, true);
        var state = AchievementState.StartingAt(At(2026, 10, 19, "00:00"));
        var p = Of([E(At(2026, 10, 25, "09:00"))], AchievementCatalog.EarlyBird, At(2026, 10, 25, "12:00"), state, settings);
        Assert.Equal(1, p.Current);
    }

    [Fact]
    public void OnCueComesFromTheCounter()
    {
        var state = FromMonday with { OnCueCount = 10 };
        Assert.True(Of([], AchievementCatalog.OnCue, At(2026, 10, 5, "10:00"), state).IsReached);
        Assert.Equal(9, Of([], AchievementCatalog.OnCue, At(2026, 10, 5, "10:00"), FromMonday with { OnCueCount = 9 }).Current);
    }

    [Fact]
    public void ProgressNeverExceedsTheTarget()
    {
        var p = Of([E(At(2026, 10, 5, "09:00"), 10000), E(At(2026, 10, 5, "10:00"), 10000)], AchievementCatalog.VolumeId(10), At(2026, 10, 5, "11:00"));
        Assert.Equal(10000, p.Current);
    }

    [Fact]
    public void UnlockedAchievementsKeepTheirDateAndAreNotNew()
    {
        var at = At(2026, 10, 5, "09:30");
        var state = FromMonday with { Unlocked = ImmutableDictionary<string, DateTimeOffset>.Empty.Add(AchievementCatalog.FirstGlass, at) };
        var progress = AchievementEvaluator.Evaluate([E(at)], state, Config(), At(2026, 10, 5, "10:00"), Berlin);

        Assert.Equal(at, progress.Single(p => p.Achievement.Id == AchievementCatalog.FirstGlass).UnlockedAt);
        Assert.DoesNotContain(AchievementEvaluator.NewlyReached(progress), a => a.Id == AchievementCatalog.FirstGlass);
    }
}

public sealed class AchievementTrackerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "trinkwasser-tests", Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Clock(At(2026, 10, 5, "09:15"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string FilePath => Path.Combine(_folder, AchievementState.FileName);

    private AchievementTracker Create()
    {
        var tracker = new AchievementTracker(new JsonFileStore(_folder, _clock), _clock);
        tracker.Load();
        return tracker;
    }

    [Fact]
    public void FirstStartFixesTheStartTime()
    {
        Create();
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(At(2026, 10, 5, "09:15"), Create().State.Since);
    }

    [Fact]
    public void CheckUnlocksOnceAndKeepsItAfterUndo()
    {
        var tracker = Create();
        var changed = 0;
        tracker.Changed += (_, _) => changed++;
        DrinkEntry[] entries = [new(At(2026, 10, 5, "09:20"), 250)];
        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal([AchievementCatalog.FirstGlass], tracker.Check(entries, Config()).Select(a => a.Id));
        Assert.Empty(tracker.Check(entries, Config()));
        Assert.Equal(1, changed);

        // "Rückgängig": das Glas ist weg, der Erfolg bleibt – auch nach einem Neustart.
        var reloaded = Create();
        Assert.Empty(reloaded.Check([], Config()));
        Assert.Equal(At(2026, 10, 5, "09:20"), reloaded.State.Unlocked[AchievementCatalog.FirstGlass]);
    }

    [Fact]
    public void SeveralAchievementsCanUnlockAtOnce()
    {
        var tracker = Create();
        _clock.Advance(TimeSpan.FromMinutes(5));
        var unlocked = tracker.Check([new(At(2026, 10, 5, "09:20"), 10000)], Config());
        Assert.Equal(
            [AchievementCatalog.FirstGlass, AchievementCatalog.FirstDailyGoal, AchievementCatalog.FirstWeeklyGoal, AchievementCatalog.VolumeId(10)],
            unlocked.Select(a => a.Id));
    }

    [Fact]
    public void OnCueCounterSurvivesReload()
    {
        var tracker = Create();
        tracker.RecordOnCue();
        tracker.RecordOnCue();
        Assert.Equal(2, Create().State.OnCueCount);
    }

    [Fact]
    public void GoalNoticeDaySurvivesReload()
    {
        var tracker = Create();
        var changed = 0;
        tracker.Changed += (_, _) => changed++;
        tracker.RecordGoalNotice(new DateOnly(2026, 10, 5));
        tracker.RecordGoalNotice(new DateOnly(2026, 10, 5));   // gleicher Tag: nicht noch einmal speichern
        Assert.Equal(1, changed);
        Assert.Equal(new DateOnly(2026, 10, 5), Create().State.GoalNoticeOn);
    }

    [Fact]
    public void FileWithoutGoalNoticeDayIsValid() =>
        Assert.Null(AchievementState.Parse("""{ "version": 1, "since": "2026-10-05T09:15:00+02:00" }""").GoalNoticeOn);

    [Fact]
    public void BrokenFileIsBackedUpAndCountingStartsOver()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ kaputt");

        var tracker = Create();
        Assert.NotNull(tracker.BrokenFileBackup);
        Assert.True(File.Exists(tracker.BrokenFileBackup));
        Assert.Equal(At(2026, 10, 5, "09:15"), tracker.State.Since);
        Assert.Equal(AchievementState.Parse(File.ReadAllText(FilePath)).Since, tracker.State.Since);
    }

    [Fact]
    public void FailedSaveKeepsTheStateInMemory()
    {
        var tracker = Create();
        // Ein Ordner mit dem Namen der Temp-Datei lässt das Schreiben scheitern.
        Directory.CreateDirectory(FilePath + ".tmp");
        Assert.Throws<UnauthorizedAccessException>(tracker.RecordOnCue);
        Assert.Equal(0, tracker.State.OnCueCount);
    }

    [Fact]
    public void ParseSkipsUnknownAchievementsAndRequiresStart()
    {
        var state = AchievementState.Parse("""
            { "version": 1, "since": "2026-10-05T09:15:00+02:00", "onCue": 3,
              "unlocked": { "first-glass": "2026-10-05T09:20:00+02:00", "gibt-es-nicht": "2026-10-05T09:20:00+02:00", "on-cue": 7 } }
            """);
        Assert.Equal(3, state.OnCueCount);
        Assert.Equal([AchievementCatalog.FirstGlass], state.Unlocked.Keys);

        Assert.Throws<JsonException>(() => AchievementState.Parse("""{ "onCue": 3 }"""));
        Assert.Throws<JsonException>(() => AchievementState.Parse("[]"));
    }

    [Fact]
    public void JsonRoundTrip()
    {
        var state = AchievementState.StartingAt(At(2026, 10, 5, "09:15")) with
        {
            OnCueCount = 4,
            GoalNoticeOn = new DateOnly(2026, 10, 7),
            Unlocked = ImmutableDictionary<string, DateTimeOffset>.Empty.Add(AchievementCatalog.StreakId(3), At(2026, 10, 7, "18:00")),
        };
        var loaded = AchievementState.Parse(state.ToJson());
        Assert.Equal((state.Since, state.OnCueCount, state.GoalNoticeOn), (loaded.Since, loaded.OnCueCount, loaded.GoalNoticeOn));
        Assert.Equal(state.Unlocked, loaded.Unlocked);
    }
}

public class AchievementCatalogTests
{
    [Fact]
    public void IdsAreUnique() =>
        Assert.Equal(AchievementCatalog.All.Count, AchievementCatalog.All.Select(a => a.Id).Distinct().Count());

    [Theory]
    [InlineData(Localizer.German)]
    [InlineData(Localizer.English)]
    public void EveryAchievementHasNameAndDescription(string language)
    {
        var loc = new Localizer(language, _ => "Sip");
        Assert.All(AchievementCatalog.All, a =>
        {
            Assert.DoesNotContain("ach.", loc.AchName(a.Id));
            Assert.DoesNotContain("ach.", loc.AchDescription(a.Id));
            Assert.DoesNotContain("{", loc.AchDescription(a.Id));
        });
        Assert.All(Enum.GetValues<AchievementCategory>(), c => Assert.DoesNotContain("ach.", loc.AchCategory(c)));
    }

    /// <summary>Badges erzeugt tools/gen-badges.cs; neue Erfolge brauchen dort einen Eintrag.</summary>
    [Fact]
    public void EveryAchievementHasBothBadges()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Trinkwasser.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var assets = Path.Combine(dir.FullName, "src", "App", "Assets", "Achievements");
        Assert.All(AchievementCatalog.All, a =>
        {
            Assert.True(File.Exists(Path.Combine(assets, a.Id + ".png")), a.Id);
            Assert.True(File.Exists(Path.Combine(assets, a.Id + ".locked.png")), a.Id + ".locked");
        });
    }

    [Fact]
    public void ProgressTexts()
    {
        var de = new Localizer(Localizer.German, _ => "Sip");
        var en = new Localizer(Localizer.English, _ => "Sip");
        AchievementProgress P(string id, int current, int target) => new(AchievementCatalog.Find(id)!, current, target, null);

        Assert.Equal("37,2 von 50 l", de.AchProgress(P(AchievementCatalog.VolumeId(50), 37290, 50000)));
        Assert.Equal("9.9 of 10 l", en.AchProgress(P(AchievementCatalog.VolumeId(10), 9999, 10000)));
        Assert.Equal("1.000 von 1.000 l", de.AchProgress(P(AchievementCatalog.VolumeId(1000), 1000000, 1000000)));
        Assert.Equal("Heute 1.500 von 2.000 ml", de.AchProgress(P(AchievementCatalog.FirstDailyGoal, 1500, 2000)));
        Assert.Equal("2 of 5 working days", en.AchProgress(P(AchievementCatalog.EarlyBird, 2, 5)));
        Assert.Equal(string.Empty, de.AchProgress(P(AchievementCatalog.FirstGlass, 0, 1)));
        Assert.Equal("Erster Schluck, noch gesperrt. Das erste Glas mit Sip getrunken.", de.AchAutomationName(P(AchievementCatalog.FirstGlass, 0, 1)));
    }
}
