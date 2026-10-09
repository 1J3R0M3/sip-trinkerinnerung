using Microsoft.Extensions.Time.Testing;
using Trinkwasser.Core.Reminders;
using Trinkwasser.Core.Settings;
using static Trinkwasser.Core.Tests.TestTime;

namespace Trinkwasser.Core.Tests;

public sealed class ReminderEngineTests : IDisposable
{
    private readonly FakeTimeProvider _clock = Clock(At(2026, 10, 5, "08:30"));
    private readonly List<DateTimeOffset> _shown = [];
    private AppSettings _settings = Config();
    private DateTimeOffset? _lastDrink;
    private readonly ReminderEngine _engine;

    public ReminderEngineTests()
    {
        _engine = new ReminderEngine(_clock, () => _settings, () => _lastDrink);
        _engine.Due += (_, _) => _shown.Add(_clock.GetUtcNow());
    }

    public void Dispose() => _engine.Dispose();

    /// <summary>Lässt die Uhr in Takten laufen, wie der Timer es im Betrieb tut.</summary>
    private void RunUntil(DateTimeOffset end)
    {
        while (_clock.GetUtcNow() < end)
        {
            _clock.Advance(ReminderEngine.Tick);
            _engine.Poll();
        }
    }

    [Fact]
    public void RemindsEveryIntervalDuringWorkAndStopsAfterwards()
    {
        _engine.Start(withTimer: false);
        RunUntil(At(2026, 10, 5, "18:00"));

        Assert.Equal(8, _shown.Count); // 9:00 … 16:00
        Assert.All(_shown.Zip(Enumerable.Range(9, 8)), x =>
            Assert.Equal(At(2026, 10, 5, $"{x.Second:00}:00"), x.First, TimeSpan.FromSeconds(20)));
        Assert.Equal(At(2026, 10, 6, "09:00"), _engine.Next);
    }

    [Fact]
    public void DrinkingRestartsTheInterval()
    {
        _engine.Start(withTimer: false);
        RunUntil(At(2026, 10, 5, "08:50"));
        _lastDrink = _clock.GetUtcNow();
        _engine.OnDrank();

        Assert.Equal(At(2026, 10, 5, "09:50"), _engine.Next);
        RunUntil(At(2026, 10, 5, "09:49"));
        Assert.Empty(_shown);
    }

    [Fact]
    public void SnoozeRemindsAgainAfterTenMinutes()
    {
        _engine.Start(withTimer: false);
        RunUntil(At(2026, 10, 5, "09:00:20"));
        Assert.Single(_shown);

        _engine.Snooze();
        RunUntil(At(2026, 10, 5, "09:11"));
        Assert.Equal(2, _shown.Count);
        Assert.Equal(_shown[0] + TimeSpan.FromMinutes(10), _shown[1], TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void AfterSleepMissedRemindersAreNotCaughtUp()
    {
        _engine.Start(withTimer: false);
        RunUntil(At(2026, 10, 5, "09:30"));
        Assert.Single(_shown);

        // Ruhezustand: während des Schlafs kommt kein Takt; danach steht die Uhr auf 13:30.
        _clock.SetUtcNow(At(2026, 10, 5, "13:30").ToUniversalTime());
        _engine.Poll();

        Assert.Single(_shown);
        Assert.Equal(At(2026, 10, 5, "14:30"), _engine.Next);
    }

    [Fact]
    public void ClockSetBackIsDetectedAndRescheduled()
    {
        // FakeTimeProvider kann nicht rückwärts laufen, daher eine einfache Uhr zum Stellen.
        var clock = new ManualClock { Now = At(2026, 10, 5, "11:00:10") };
        using var engine = new ReminderEngine(clock, () => _settings, () => null);
        var shown = 0;
        engine.Due += (_, _) => shown++;
        engine.Start(withTimer: false);
        engine.Poll();
        Assert.Equal(At(2026, 10, 5, "12:00:10"), engine.Next);

        clock.Now = At(2026, 10, 5, "10:00");
        engine.Poll();
        Assert.Equal(0, shown);
        Assert.Equal(At(2026, 10, 5, "11:00"), engine.Next);
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => Berlin;
    }

    [Fact]
    public void ChangedSettingsTakeEffectOnReschedule()
    {
        _engine.Start(withTimer: false);
        _settings = _settings with { IntervalMin = 15 };
        _engine.Reschedule();
        // 8:00 + 15 min liegt schon zurück (jetzt 8:30) → ein Intervall nach jetzt.
        Assert.Equal(At(2026, 10, 5, "08:45"), _engine.Next);
    }

    [Fact]
    public void PeriodCoversTheWholeIntervalOrTheSnooze()
    {
        _engine.Start(withTimer: false);
        Assert.Equal(At(2026, 10, 5, "09:00"), _engine.Next);
        Assert.Equal(At(2026, 10, 5, "08:00"), _engine.Snapshot.PeriodStart);

        RunUntil(At(2026, 10, 5, "09:00:20"));
        _engine.Snooze();
        var (next, start) = _engine.Snapshot;
        Assert.Equal(TimeSpan.FromMinutes(10), next - start);
    }

    [Fact]
    public void TimerDrivesThePolling()
    {
        _engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Single(_shown);
    }
}
