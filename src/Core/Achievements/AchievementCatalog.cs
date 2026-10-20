namespace Trinkwasser.Core.Achievements;

/// <summary>Kategorie eines Erfolgs; bestimmt die Form des Badges und die Gruppe auf der Seite.</summary>
public enum AchievementCategory
{
    FirstSteps,
    Volume,
    Streak,
    Habit,
}

/// <summary>Worin der Fortschritt gemessen wird (für die Anzeige "37 von 50 l").</summary>
public enum AchievementUnit
{
    Glasses,
    Milliliters,
    Liters,
    Days,
    WorkDays,
    Times,
}

/// <summary>
/// Ein Erfolg. <paramref name="Target"/> ist das Ziel in <paramref name="Unit"/>; bei Zielen,
/// die von den Einstellungen abhängen (Tages-, Wochenziel, Arbeitstage), ist es 0 und wird
/// bei der Auswertung bestimmt. Name, Beschreibung und Badge hängen an der <paramref name="Id"/>.
/// </summary>
public sealed record Achievement(string Id, AchievementCategory Category, AchievementUnit Unit, int Target);

/// <summary>Alle Erfolge in der Reihenfolge der Erfolgsseite.</summary>
public static class AchievementCatalog
{
    public const string FirstGlass = "first-glass";
    public const string FirstDailyGoal = "first-daily-goal";
    public const string FirstWeeklyGoal = "first-weekly-goal";
    public const string PerfectWeek = "perfect-week";
    public const string OnCue = "on-cue";
    public const string EarlyBird = "early-bird";

    /// <summary>"Aufs Wort": so oft direkt in der Erinnerung auf "Getrunken" klicken.</summary>
    public const int OnCueTarget = 10;

    /// <summary>"Früher Vogel": an so vielen Arbeitstagen früh das erste Glas.</summary>
    public const int EarlyBirdTarget = 5;

    /// <summary>"Früher Vogel": erstes Glas spätestens so lange nach Arbeitsbeginn.</summary>
    public static TimeSpan EarlyBirdWindow { get; } = TimeSpan.FromHours(1);

    public static IReadOnlyList<int> VolumeLiters { get; } = [10, 50, 100, 250, 500, 1000];
    public static IReadOnlyList<int> StreakDays { get; } = [3, 7, 14, 30, 100];

    public static IReadOnlyList<Achievement> All { get; } =
    [
        new(FirstGlass, AchievementCategory.FirstSteps, AchievementUnit.Glasses, 1),
        new(FirstDailyGoal, AchievementCategory.FirstSteps, AchievementUnit.Milliliters, 0),
        new(FirstWeeklyGoal, AchievementCategory.FirstSteps, AchievementUnit.Milliliters, 0),
        .. VolumeLiters.Select(l => new Achievement(VolumeId(l), AchievementCategory.Volume, AchievementUnit.Liters, l)),
        .. StreakDays.Select(d => new Achievement(StreakId(d), AchievementCategory.Streak, AchievementUnit.Days, d)),
        new(PerfectWeek, AchievementCategory.Habit, AchievementUnit.WorkDays, 0),
        new(OnCue, AchievementCategory.Habit, AchievementUnit.Times, OnCueTarget),
        new(EarlyBird, AchievementCategory.Habit, AchievementUnit.WorkDays, EarlyBirdTarget),
    ];

    public static string VolumeId(int liters) => $"volume-{liters}";
    public static string StreakId(int days) => $"streak-{days}";

    public static Achievement? Find(string id) => All.FirstOrDefault(a => a.Id == id);
}
