namespace Trinkwasser.Core.Settings;

public enum WorkHoursError
{
    None,
    /// <summary>Kein Wochentag ist aktiv.</summary>
    NoWorkDay,
    /// <summary>Bei einem aktiven Tag liegt das Ende nicht nach dem Beginn.</summary>
    TimeRange,
}

/// <summary>Prüft die Arbeitszeiten aus der Oberfläche, bevor gespeichert wird.</summary>
public static class WorkHoursValidator
{
    public static WorkHoursError Validate(IReadOnlyList<WorkDay> days)
    {
        if (!days.Any(d => d.Enabled)) return WorkHoursError.NoWorkDay;
        if (days.Any(d => d.Enabled && d.End <= d.Start)) return WorkHoursError.TimeRange;
        return WorkHoursError.None;
    }

    /// <summary>"Montag auf alle aktiven Tage übertragen": Zeiten des Montags für jeden aktiven Tag.</summary>
    public static IReadOnlyList<WorkDay> CopyMondayToActiveDays(IReadOnlyList<WorkDay> days) =>
        [.. days.Select((d, i) => i > 0 && d.Enabled ? d with { Start = days[0].Start, End = days[0].End } : d)];
}
