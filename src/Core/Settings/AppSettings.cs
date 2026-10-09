using Trinkwasser.Core.Localization;

namespace Trinkwasser.Core.Settings;

/// <summary>Arbeitszeit eines Wochentags. Ende liegt immer nach dem Beginn (keine Schichten über Mitternacht).</summary>
public sealed record WorkDay(bool Enabled, TimeOnly Start, TimeOnly End)
{
    public static WorkDay Default(bool enabled) => new(enabled, new TimeOnly(8, 0), new TimeOnly(17, 0));
}

/// <summary>
/// Alle Einstellungen der App. Werte von außen (Datei, Oberfläche) laufen immer durch
/// <see cref="SettingsSanitizer"/>, damit hier nur gültige Werte stehen.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public bool SetupDone { get; init; }
    public string Language { get; init; } = Localizer.English;
    public int DailyGoalMl { get; init; } = 2000;
    public int WeeklyGoalMl { get; init; } = 10000;
    public int GlassMl { get; init; } = 250;
    public int IntervalMin { get; init; } = 60;

    /// <summary>
    /// Wunsch aus dem Einrichtungsassistenten. Maßgeblich ist der Zustand der StartupTask in
    /// Windows, denn der Nutzer kann den Autostart dort jederzeit selbst ausschalten.
    /// </summary>
    public bool Autostart { get; init; }

    /// <summary>Index 0 = Montag … 6 = Sonntag.</summary>
    public IReadOnlyList<WorkDay> WorkHours { get; init; } = DefaultWorkHours;

    public static IReadOnlyList<WorkDay> DefaultWorkHours { get; } =
        [.. Enumerable.Range(0, 7).Select(i => WorkDay.Default(enabled: i < 5))];

    /// <summary>Standardwerte; die Sprache richtet sich nach der Windows-Sprache.</summary>
    public static AppSettings Defaults(string? windowsLanguage) =>
        new() { Language = Localizer.Normalize(windowsLanguage) };

    public int WorkDayCount => WorkHours.Count(d => d.Enabled);

    // Records vergleichen Listen nur per Referenz; für "hat sich etwas geändert?" zählt der Inhalt.
    public bool Equals(AppSettings? other) =>
        other is not null
        && Version == other.Version && SetupDone == other.SetupDone && Language == other.Language
        && DailyGoalMl == other.DailyGoalMl && WeeklyGoalMl == other.WeeklyGoalMl
        && GlassMl == other.GlassMl && IntervalMin == other.IntervalMin && Autostart == other.Autostart
        && WorkHours.SequenceEqual(other.WorkHours);

    public override int GetHashCode() =>
        HashCode.Combine(SetupDone, Language, DailyGoalMl, WeeklyGoalMl, GlassMl, IntervalMin, Autostart);
}
