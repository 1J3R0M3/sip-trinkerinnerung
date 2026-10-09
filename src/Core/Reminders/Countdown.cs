namespace Trinkwasser.Core.Reminders;

/// <summary>Restzeit bis zur nächsten Erinnerung für den Countdown-Kreis.</summary>
public readonly record struct Countdown(TimeSpan Remaining, double RemainingFraction)
{
    /// <param name="periodStart">Seit wann der aktuelle Termin gilt (Kreis voll).</param>
    /// <param name="next">Termin der Erinnerung (Kreis leer).</param>
    public static Countdown Between(DateTimeOffset periodStart, DateTimeOffset next, DateTimeOffset now)
    {
        var remaining = next - now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        var total = next - periodStart;
        var fraction = total > TimeSpan.Zero ? Math.Clamp(remaining / total, 0, 1) : 0;
        return new Countdown(remaining, fraction);
    }

    /// <summary>"12:05" (Minuten:Sekunden) oder "1:12:05" ab einer Stunde.</summary>
    public string Format() =>
        Remaining.TotalHours >= 1
            ? $"{(int)Remaining.TotalHours}:{Remaining.Minutes:00}:{Remaining.Seconds:00}"
            : $"{Remaining.Minutes:00}:{Remaining.Seconds:00}";
}
