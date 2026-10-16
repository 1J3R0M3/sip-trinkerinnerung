using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trinkwasser.App.Services;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Sounds;

namespace Trinkwasser.App.ViewModels;

/// <summary>Arbeitszeit eines Wochentags in den Einstellungen (0 = Montag).</summary>
public sealed partial class WorkDayRow(Localizer loc, int index, WorkDay day) : ObservableObject
{
    public int Index { get; } = index;

    [ObservableProperty]
    public partial bool Enabled { get; set; } = day.Enabled;

    [ObservableProperty]
    public partial TimeSpan? Start { get; set; } = day.Start.ToTimeSpan();

    [ObservableProperty]
    public partial TimeSpan? End { get; set; } = day.End.ToTimeSpan();

    public string Name => loc.WeekdayName(Index);
    public string StartLabel => loc.FieldDayStart(Index);
    public string EndLabel => loc.FieldDayEnd(Index);

    public WorkDay ToWorkDay(WorkDay previous) => new(
        Enabled,
        Start is { } s ? TimeOnly.FromTimeSpan(s) : previous.Start,
        End is { } e ? TimeOnly.FromTimeSpan(e) : previous.End);

    public void RefreshTexts() => OnPropertyChanged(string.Empty);
}

/// <summary>
/// Einstellungen. Jede Änderung wird sofort gespeichert (wie in den Windows-Einstellungen).
/// Ungültige Eingaben werden angezeigt und nicht gespeichert; die gespeicherten Werte bleiben
/// dann unverändert, bis die Eingabe korrigiert ist.
/// </summary>
public sealed partial class SettingsViewModel : FormViewModelBase
{
    private readonly SettingsService _settings;
    private bool _bulkChange;

    public SettingsViewModel(Localizer loc, SettingsService settings, AutostartService autostart, SoundPreview preview)
        : base(loc, settings.Current)
    {
        _settings = settings;
        Days = CreateRows(settings.Current.WorkHours);
        // Eine angezeigte Eingabe-Fehlermeldung bleibt beim Speichern eines Tons stehen.
        Sounds = [.. Enum.GetValues<SoundOccasion>().Select(o =>
            new SoundRow(loc, preview, o, settings.Current.Sound(o), (occasion, id) => Save(s => s.WithSound(occasion, id), ErrorMessage)))];
        Autostart = new AutostartViewModel(loc, autostart, applyImmediately: true, wish: settings.Current.Autostart);
        // Eine angezeigte Eingabe-Fehlermeldung bleibt dabei stehen.
        Autostart.Applied += (_, on) => Save(s => s with { Autostart = on }, ErrorMessage);
        SelectedThemeIndex = (int)settings.Current.Theme;
        RefreshSuggestion();
    }

    /// <summary>Wird beim Sprachwechsel neu erzeugt, siehe <see cref="OnLanguageChanged"/>.</summary>
    public IReadOnlyList<WorkDayRow> Days { get; private set; }

    public AutostartViewModel Autostart { get; }

    /// <summary>Ein Ton je Anlass (Erinnerung, erneute Erinnerung, Tagesziel).</summary>
    public IReadOnlyList<SoundRow> Sounds { get; }

    // Darstellung: Reihenfolge wie AppTheme (System, Hell, Dunkel). Wirkt sofort.
    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    public string ThemeSystemText => Loc.ThemeName(AppTheme.System);
    public string ThemeLightText => Loc.ThemeName(AppTheme.Light);
    public string ThemeDarkText => Loc.ThemeName(AppTheme.Dark);

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (value is >= 0 and <= 2) Save(s => s with { Theme = (AppTheme)value }, ErrorMessage);
    }

    private IReadOnlyList<WorkDayRow> CreateRows(IReadOnlyList<WorkDay> days)
    {
        var rows = days.Select((d, i) => new WorkDayRow(Loc, i, d)).ToList();
        foreach (var row in rows)
            row.PropertyChanged += (_, e) =>
            {
                if (!_bulkChange && e.PropertyName is nameof(WorkDayRow.Enabled) or nameof(WorkDayRow.Start) or nameof(WorkDayRow.End))
                    OnWorkHoursChanged();
            };
        return rows;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    public bool HasError => ErrorMessage.Length > 0;

    protected override int WorkDayCount => Days?.Count(d => d.Enabled) ?? 0;

    [RelayCommand]
    private void CopyMonday()
    {
        var copied = WorkHoursValidator.CopyMondayToActiveDays([.. Days.Select(r => r.ToWorkDay(_settings.Current.WorkHours[r.Index]))]);
        _bulkChange = true;
        foreach (var row in Days)
        {
            row.Start = copied[row.Index].Start.ToTimeSpan();
            row.End = copied[row.Index].End.ToTimeSpan();
        }
        _bulkChange = false;
        OnWorkHoursChanged();
    }

    protected override void OnFieldChanged(string name) => SaveForm();

    private void OnWorkHoursChanged()
    {
        RefreshSuggestion();
        SaveForm();
    }

    /// <summary>Speichert alle gültigen Eingaben; die erste ungültige wird als Fehler angezeigt.</summary>
    private void SaveForm()
    {
        var current = _settings.Current;
        var workHours = Days.Select(r => r.ToWorkDay(current.WorkHours[r.Index])).ToList();
        var workHoursError = WorkHoursValidator.Validate(workHours);

        var error = NumberError() ?? workHoursError switch
        {
            WorkHoursError.NoWorkDay => Loc.ErrorNoWorkDay,
            WorkHoursError.TimeRange => TimeRangeError(workHours),
            _ => null,
        };

        Save(s => s with
        {
            Language = SelectedLanguage,
            // Leere Felder (NaN) behalten den gespeicherten Wert.
            DailyGoalMl = SettingsLimits.DailyGoalMl.Clamp(DailyGoal, s.DailyGoalMl),
            WeeklyGoalMl = SettingsLimits.WeeklyGoalMl.Clamp(WeeklyGoal, s.WeeklyGoalMl),
            GlassMl = SettingsLimits.GlassMl.Clamp(Glass, s.GlassMl),
            IntervalMin = SelectedIntervalMinutes,
            WorkHours = workHoursError == WorkHoursError.None ? workHours : s.WorkHours,
        }, error);
    }

    private string TimeRangeError(IReadOnlyList<WorkDay> days)
    {
        var index = days.ToList().FindIndex(d => d.Enabled && d.End <= d.Start);
        return $"{Loc.WeekdayName(index)}: {Loc.ErrorTimeRange}";
    }

    private void Save(Func<AppSettings, AppSettings> change, string? validationError = null)
    {
        try
        {
            _settings.Update(change);
            ErrorMessage = validationError ?? string.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Loc.ErrorSave(e.Message);
        }
    }

    protected override void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Der TimePicker übernimmt seinen Namen für Screenreader nur beim Aufbau. Neue Zeilen
        // (mit den aktuellen, evtl. noch ungespeicherten Eingaben) erzwingen neue TimePicker.
        var current = _settings.Current.WorkHours;
        Days = CreateRows([.. Days.Select(r => r.ToWorkDay(current[r.Index]))]);
        base.OnLanguageChanged(sender, e);
        Autostart.RefreshTexts();
        foreach (var row in Sounds) row.RefreshTexts();
        // Fehlermeldung in der neuen Sprache.
        if (HasError) SaveForm();
    }
}
