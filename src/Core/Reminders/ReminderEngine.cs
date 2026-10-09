using Trinkwasser.Core.Settings;

namespace Trinkwasser.Core.Reminders;

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

    /// <summary>Nächster geplanter Termin; null = keiner in der nächsten Woche (keine Arbeitstage).</summary>
    public DateTimeOffset? Next { get; private set; }

    /// <summary>Eine Erinnerung ist fällig (Hintergrund-Thread).</summary>
    public event EventHandler? Due;

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
    public void Reschedule()
    {
        bool changed;
        lock (_lock)
        {
            var next = ReminderScheduler.Next(settings(), new ReminderState(lastDrink(), _lastReminder, _snoozeUntil),
                clock.GetUtcNow(), clock.LocalTimeZone);
            changed = next != Next;
            Next = next;
        }
        if (changed) NextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ein Takt: prüft, ob eine Erinnerung fällig ist (sonst vom Timer aufgerufen).</summary>
    public void Poll()
    {
        var now = clock.GetUtcNow();
        bool due;
        lock (_lock)
        {
            var jumped = _lastTick is { } last && (now < last || now - last > JumpThreshold);
            _lastTick = now;
            // Aufgewacht oder Uhr verstellt: nicht auslösen, nur neu planen. Der Planer legt
            // einen verpassten Termin dann ein Intervall nach jetzt.
            due = !jumped && Next is { } next && now >= next;
            if (due)
            {
                _lastReminder = now;
                _snoozeUntil = null;
            }
        }
        Reschedule();
        if (due) Due?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _timer?.Dispose();
}
