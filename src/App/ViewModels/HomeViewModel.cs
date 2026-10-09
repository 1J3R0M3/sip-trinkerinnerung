using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Trinkwasser.App.Services;
using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Reminders;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.App.ViewModels;

/// <summary>
/// Startseite: Fortschritt heute und in dieser Woche, "+1 Glas", "Rückgängig" und die nächste Erinnerung.
/// Alle Werte werden bei jeder Änderung neu berechnet (das Protokoll ist klein).
/// </summary>
public sealed partial class HomeViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settings;
    private readonly DrinkLog _log;
    private readonly ReminderEngine _reminders;
    private readonly DrinkActions _actions;
    private readonly TimeProvider _clock;
    private readonly Action _sendTest;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _minuteTimer;

    public HomeViewModel(Localizer loc, SettingsService settings, DrinkLog log, ReminderEngine reminders,
        DrinkActions actions, TimeProvider clock, Action sendTest)
    {
        Loc = loc;
        _settings = settings;
        _log = log;
        _reminders = reminders;
        _actions = actions;
        _clock = clock;
        _sendTest = sendTest;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        Loc.LanguageChanged += OnChanged;
        _settings.Changed += OnChanged;
        _log.Changed += OnChanged;
        _reminders.NextChanged += OnNextChanged;
        // Einmal pro Minute: Tageswechsel um Mitternacht und "morgen"/"heute" in der Erinnerung.
        _minuteTimer = _dispatcher.CreateTimer();
        _minuteTimer.Interval = TimeSpan.FromMinutes(1);
        _minuteTimer.Tick += (_, _) => Refresh();
        _minuteTimer.Start();
    }

    public Localizer Loc { get; }

    /// <summary>Text zum Vorlesen nach "+1 Glas" oder "Rückgängig".</summary>
    public event EventHandler<string>? Announce;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    public bool HasError => ErrorMessage.Length > 0;

    private AppSettings S => _settings.Current;
    private DateTimeOffset Now => _clock.GetLocalNow();
    private DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
    private TimeZoneInfo Zone => _clock.LocalTimeZone;

    public int TodayMl => DrinkStatistics.DayTotalMl(_log.Entries, Today, Zone);
    public int WeekMl => DrinkStatistics.TotalMl(_log.Entries, WorkCalendar.WeekStart(Today), Today.AddDays(6), Zone);

    public double DailyGoal => S.DailyGoalMl;
    public double WeeklyGoal => S.WeeklyGoalMl;
    public double TodayBarValue => Math.Min(TodayMl, S.DailyGoalMl);
    public double WeekBarValue => Math.Min(WeekMl, S.WeeklyGoalMl);

    public string TodayText => Loc.HomeProgress(TodayMl, S.DailyGoalMl);
    public string WeekText => Loc.HomeProgress(WeekMl, S.WeeklyGoalMl);
    public string TodayStatus => TodayMl >= S.DailyGoalMl ? Loc.HomeGoalReached : Loc.HomePercent(Percent(TodayMl, S.DailyGoalMl));
    public string WeekStatus => WeekMl >= S.WeeklyGoalMl ? Loc.HomeGoalReached : Loc.HomePercent(Percent(WeekMl, S.WeeklyGoalMl));

    public string AddGlassText => Loc.HomeAddGlass(S.GlassMl);

    /// <summary>Rückgängig nur für Einträge von heute – Vortage löscht man nicht versehentlich.</summary>
    public bool CanUndo => _log.Last is { } last && WorkCalendar.LocalDate(last.Time, Zone) == Today;

    public string UndoHelp => _log.Last is { } last && CanUndo
        ? Loc.HomeUndoHelp(last.AmountMl, TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(last.Time, Zone).DateTime))
        : string.Empty;

    public string NextReminderText => _reminders.Next is { } next
        ? Loc.HomeNextReminder(TimeZoneInfo.ConvertTime(next, Zone).DateTime, Now.DateTime)
        : Loc.HomeNoReminder;

    public string WorkTodayText => WorkCalendar.For(S, Today) is { Enabled: true } day
        ? Loc.HomeWorkToday(day.Start, day.End)
        : Loc.HomeNoWorkToday;

    public string IntervalText => Loc.HomeInterval(S.IntervalMin);

    [RelayCommand]
    private void AddGlass()
    {
        try
        {
            _actions.AddGlass();
            ErrorMessage = string.Empty;
            Announce?.Invoke(this, Loc.HomeAdded(TodayMl, S.DailyGoalMl));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Loc.ErrorSaveDrink(e.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        try
        {
            _actions.Undo();
            ErrorMessage = string.Empty;
            Announce?.Invoke(this, Loc.HomeRemoved(TodayMl, S.DailyGoalMl));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Loc.ErrorSaveDrink(e.Message);
        }
    }

    [RelayCommand]
    private void SendTest() => _sendTest();

    private static int Percent(int value, int goal) => DrinkStatistics.Percent(value, goal);

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    // Kommt vom Timer-Thread der Erinnerungen.
    private void OnNextChanged(object? sender, EventArgs e) => _dispatcher.TryEnqueue(Refresh);

    private void Refresh()
    {
        OnPropertyChanged(string.Empty);
        UndoCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _minuteTimer.Stop();
        Loc.LanguageChanged -= OnChanged;
        _settings.Changed -= OnChanged;
        _log.Changed -= OnChanged;
        _reminders.NextChanged -= OnNextChanged;
    }
}
