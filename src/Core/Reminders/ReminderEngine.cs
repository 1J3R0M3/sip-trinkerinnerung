using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Reminders;

/// <summary>Reguläre Erinnerung oder die nach "In 10 Min. erinnern" (eigener Ton).</summary>
public enum ReminderKind
{
    Regular,
    Snooze,
}

/// <summary>
/// Plant die Erinnerungen und meldet, wann eine fällig ist. Ein kurzer Takt prüft regelmäßig;
/// so fällt auf, wenn der Rechner geschlafen hat oder die Uhr verstellt wurde (der Abstand
/// zwischen zwei Takten passt dann nicht). In dem Fall wird neu geplant, ohne die verpasste
/// Erinnerung nachzuholen.
/// </summary>
/// <remarks>
/// Der Timer läuft auf einem Hintergrund-Thread: <see cref="Due"/> und <see cref="NextChanged"/>
/// kommen von dort. <paramref name="settings"/> und <paramref name="lastDrink"/> müssen
/// threadsicher lesbar sein (unveränderliche Objekte bzw. ausgetauschte Listen).
/// </remarks>
public sealed class ReminderEngine(TimeProvider clock, Func<AppSettings> settings, Func<DateTimeOffset?> lastDrink) : IDisposable
{
    public static readonly TimeSpan Tick = TimeSpan.FromSeconds(20);

    /// <summary>Größerer Abstand zwischen zwei Takten: Schlaf, Ruhezustand oder verstellte Uhr.</summary>
    public static readonly TimeSpan JumpThreshold = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan SnoozeDuration = TimeSpan.FromMinutes(Notifications.ToastArguments.SnoozeMinutes);

    private readonly Lock _lock = new();
    private ITimer? _timer;
    private DateTimeOffset? _lastTick;
    private DateTimeOffset? _lastReminder;
    private DateTimeOffset? _snoozeUntil;
    private DateTimeOffset? _since;

    /// <summary>Nächster geplanter Termin; null = keiner in der nächsten Woche (keine Arbeitstage).</summary>
    public DateTimeOffset? Next { get; private set; }

    /// <summary>Beginn des laufenden Intervalls (Termin − Intervall bzw. − Snooze) – Kreis voll.</summary>
    public DateTimeOffset? PeriodStart { get; private set; }

    /// <summary><see cref="Next"/> und <see cref="PeriodStart"/> zusammen, ohne dass der Timer-Thread dazwischenfunkt.</summary>
    public (DateTimeOffset? Next, DateTimeOffset? PeriodStart) Snapshot
    {
        get
        {
            lock (_lock) return (Next, PeriodStart);
        }
    }

    /// <summary>Eine Erinnerung ist fällig (Hintergrund-Thread), regulär oder nach "In 10 Min. erinnern".</summary>
    public event EventHandler<ReminderKind>? Due;

    /// <summary><see cref="Next"/> hat sich geändert (Hintergrund- oder aufrufender Thread).</summary>
    public event EventHandler? NextChanged;

    /// <param name="withTimer">Nur für Tests false: dann wird <see cref="Poll"/> von Hand aufgerufen.</param>
    public void Start(bool withTimer = true)
    {
        Reschedule();
        if (withTimer) _timer ??= clock.CreateTimer(_ => Poll(), null, Tick, Tick);
    }

    /// <summary>Nach "Getrunken": Intervall neu starten, ein Snooze entfällt.</summary>
    public void OnDrank()
    {
        lock (_lock) _snoozeUntil = null;
        Reschedule();
    }

    /// <summary>"In 10 Min. erinnern".</summary>
    public void Snooze()
    {
        lock (_lock) _snoozeUntil = clock.GetUtcNow() + SnoozeDuration;
        Reschedule();
    }

    /// <summary>Neu planen, z. B. nach geänderten Einstellungen oder einem gelöschten Glas.</summary>
    /// <remarks>Ist der eigentliche Termin schon vorbei, zählt das Intervall ab jetzt.</remarks>
    public void Reschedule()
    {
        lock (_lock) _since = clock.GetUtcNow();
        Plan();
    }

    /// <summary>Plant mit dem bisherigen <see cref="_since"/>, damit der Termin nicht bei jedem Takt mitwandert.</summary>
    private void Plan()
    {
        bool changed;
        lock (_lock)
        {
            var next = ReminderScheduler.Next(settings(),
                new ReminderState(lastDrink(), _lastReminder, _snoozeUntil, _since),
                clock.GetUtcNow(), clock.LocalTimeZone);
            changed = next != Next;
            Next = next;
            // Der Kreis steht für ein ganzes Intervall (beim Snooze: 10 Minuten), auch wenn die
            // App mitten darin gestartet wurde.
            var length = next is { } n && n == _snoozeUntil ? SnoozeDuration : TimeSpan.FromMinutes(settings().IntervalMin);
            PeriodStart = next - length;
        }
        if (changed) NextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ein Takt: prüft, ob eine Erinnerung fällig ist (sonst vom Timer aufgerufen).</summary>
    public void Poll()
    {
        var now = clock.GetUtcNow();
        bool due;
        var kind = ReminderKind.Regular;
        lock (_lock)
        {
            var jumped = _lastTick is { } last && (now < last || now - last > JumpThreshold);
            _lastTick = now;
            // Aufgewacht oder Uhr verstellt: nicht auslösen, nur neu planen. Der Planer legt
            // einen verpassten Termin dann ein Intervall nach jetzt.
            if (jumped) _since = now;
            due = !jumped && Next is { } next && now >= next;
            if (due)
            {
                if (Next == _snoozeUntil) kind = ReminderKind.Snooze;
                _lastReminder = now;
                _snoozeUntil = null;
            }
        }
        Plan();
        if (due) Due?.Invoke(this, kind);
    }

    public void Dispose() => _timer?.Dispose();
}
