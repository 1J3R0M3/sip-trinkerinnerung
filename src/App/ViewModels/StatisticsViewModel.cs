using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.App.ViewModels;

/// <summary>Ein Balken im Wochenverlauf (Höhen in Pixeln, bezogen auf <see cref="StatisticsViewModel.ChartHeight"/>).</summary>
public sealed record BarItem(
    string DayShort,
    string ValueText,
    double BarHeight,
    double GoalOffset,
    bool GoalReached,
    bool IsToday,
    string AutomationName);

/// <summary>Statistik einer Woche, mit Blättern zu früheren Wochen.</summary>
public sealed partial class StatisticsViewModel : ObservableObject, IDisposable
{
    public const double ChartHeight = 180;

    private readonly SettingsService _settings;
    private readonly DrinkLog _log;
    private readonly TimeProvider _clock;
    private WeekStats _week = null!;

    public StatisticsViewModel(Localizer loc, SettingsService settings, DrinkLog log, TimeProvider clock)
    {
        Loc = loc;
        _settings = settings;
        _log = log;
        _clock = clock;
        WeekStart = WorkCalendar.WeekStart(Today);
        Loc.LanguageChanged += OnChanged;
        _settings.Changed += OnChanged;
        _log.Changed += OnChanged;
        Refresh();
    }

    public Localizer Loc { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextWeekCommand), nameof(ThisWeekCommand))]
    public partial DateOnly WeekStart { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<BarItem> Bars { get; private set; } = [];

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
    private bool IsCurrentWeek => WeekStart >= WorkCalendar.WeekStart(Today);

    public double ChartHeightValue => ChartHeight;

    public string WeekTitle => IsCurrentWeek ? Loc.StatsThisWeek : Loc.StatsWeekRange(WeekStart, WeekStart.AddDays(6));
    public string WeekRange => Loc.StatsWeekRange(WeekStart, WeekStart.AddDays(6));

    public string TotalText => Loc.Ml(_week.TotalMl);
    public string AverageText => _week.AverageMl is { } a ? Loc.Ml(a) : Loc.StatsNoData;
    public string BestHourText => _week.BestHour is { } h ? Loc.StatsBestHourValue(h) : Loc.StatsNoData;
    public string GoalRateText => Loc.StatsGoalRateValue(_week.GoalPercent);
    public string GoalRateDetail => Loc.HomeProgress(_week.TotalMl, _week.WeeklyGoalMl);
    public string DaysReachedText => _week.DaysCounted > 0 ? Loc.StatsDaysReached(_week.DaysReached, _week.DaysCounted) : string.Empty;

    /// <summary>Die Serie zählt immer ab heute, unabhängig von der angezeigten Woche.</summary>
    public string StreakText => Loc.StatsStreakValue(DrinkStatistics.Streak(_log.Entries, _settings.Current, Today, _clock.LocalTimeZone));

    [RelayCommand]
    private void PreviousWeek() => WeekStart = WeekStart.AddDays(-7);

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void NextWeek() => WeekStart = WeekStart.AddDays(7);

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void ThisWeek() => WeekStart = WorkCalendar.WeekStart(Today);

    private bool CanGoForward => !IsCurrentWeek;

    partial void OnWeekStartChanged(DateOnly value) => Refresh();

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var s = _settings.Current;
        var today = Today;
        _week = DrinkStatistics.ForWeek(_log.Entries, s, WeekStart, today, _clock.LocalTimeZone);

        // Maßstab: höchster Tag oder Tagesziel, damit die Ziellinie immer sichtbar bleibt.
        var max = Math.Max(s.DailyGoalMl, _week.Days.Max(d => d.TotalMl));
        Bars = [.. _week.Days.Select((d, i) => new BarItem(
            Loc.WeekdayName(i, abbreviated: true),
            d.TotalMl > 0 ? Loc.Liters(d.TotalMl).TrimStart('=', ' ') : "–",
            ChartHeight * d.TotalMl / max,
            Math.Min(ChartHeight - 2, ChartHeight * s.DailyGoalMl / max),
            d.GoalReached,
            d.Date == today,
            Loc.StatsBar(i, d.TotalMl, s.DailyGoalMl, d.GoalReached)))];

        OnPropertyChanged(string.Empty);
        NextWeekCommand.NotifyCanExecuteChanged();
        ThisWeekCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        Loc.LanguageChanged -= OnChanged;
        _settings.Changed -= OnChanged;
        _log.Changed -= OnChanged;
    }
}
