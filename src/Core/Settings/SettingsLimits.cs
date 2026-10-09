namespace Trinkwasser.Core.Settings;

/// <summary>Erlaubter Bereich eines Zahlenwerts (beide Grenzen eingeschlossen).</summary>
public readonly record struct Limit(int Min, int Max)
{
    /// <summary>Rundet und klemmt auf die Grenzen; keine Zahl (NaN, ∞) → <paramref name="fallback"/>.</summary>
    public int Clamp(double value, int fallback) =>
        double.IsFinite(value) ? (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), Min, Max) : fallback;
}

public static class SettingsLimits
{
    public static Limit DailyGoalMl { get; } = new(250, 10000);
    public static Limit WeeklyGoalMl { get; } = new(250, 70000);
    public static Limit GlassMl { get; } = new(50, 2000);
    public static Limit IntervalMin { get; } = new(5, 240);

    /// <summary>Auswahl im Assistenten und in den Einstellungen.</summary>
    public static IReadOnlyList<int> IntervalChoices { get; } = [15, 20, 30, 45, 60, 90, 120, 180];

    /// <summary>
    /// Auswahlliste fürs Intervall. Ein Wert, der nicht in der Liste steht (z. B. von Hand in
    /// die Datei geschrieben), wird an passender Stelle ergänzt statt verworfen.
    /// </summary>
    public static IReadOnlyList<int> IntervalChoicesWith(int current) =>
        IntervalChoices.Contains(current) ? IntervalChoices : [.. IntervalChoices.Append(current).Order()];

    /// <summary>
    /// Vorschlag fürs Wochenziel: Tagesziel × Anzahl Arbeitstage, auf die Grenzen geklemmt.
    /// Ohne Arbeitstage oder ohne gültiges Tagesziel gibt es keinen Vorschlag (null).
    /// </summary>
    public static int? SuggestWeeklyGoal(double dailyGoalMl, int workDays) =>
        workDays > 0 && double.IsFinite(dailyGoalMl) && dailyGoalMl > 0
            ? WeeklyGoalMl.Clamp(Math.Round(dailyGoalMl, MidpointRounding.AwayFromZero) * workDays, WeeklyGoalMl.Min)
            : null;
}
