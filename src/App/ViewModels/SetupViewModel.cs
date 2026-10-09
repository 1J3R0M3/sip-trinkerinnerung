using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trinkwasser.App.Services;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.App.ViewModels;

/// <summary>Wochentag zum Anklicken im Assistenten (0 = Montag).</summary>
public sealed partial class WeekdayToggle(Localizer loc, int index, bool isOn) : ObservableObject
{
    public int Index { get; } = index;

    [ObservableProperty]
    public partial bool IsOn { get; set; } = isOn;

    public string ShortName => loc.WeekdayName(Index, abbreviated: true);
    public string Name => loc.WeekdayName(Index);

    public void RefreshTexts() => OnPropertyChanged(string.Empty);
}

/// <summary>
/// Einrichtungsassistent beim ersten Start, drei Schritte wie im Prototyp:
/// Sprache · Ziele · Erinnerungen (Intervall, eine gemeinsame Arbeitszeit, Wochentage, Autostart).
/// Gespeichert wird erst bei "Fertig".
/// </summary>
public sealed partial class SetupViewModel : FormViewModelBase
{
    public const int StepCount = 3;

    private readonly SettingsService _settings;
    private bool _weeklyTouched;
    private bool _followingSuggestion;

    public SetupViewModel(Localizer loc, SettingsService settings, AutostartService autostart)
        : base(loc, settings.Current)
    {
        _settings = settings;
        var s = settings.Current;
        // Gemeinsame Zeit: vom ersten aktiven Tag (wie im Prototyp).
        var first = s.WorkHours.FirstOrDefault(d => d.Enabled) ?? s.WorkHours[0];
        Start = first.Start.ToTimeSpan();
        End = first.End.ToTimeSpan();
        Days = [.. s.WorkHours.Select((d, i) => new WeekdayToggle(loc, i, d.Enabled))];
        foreach (var day in Days)
            day.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WeekdayToggle.IsOn)) OnWorkHoursChanged();
            };
        Autostart = new AutostartViewModel(loc, autostart, applyImmediately: false, wish: s.Autostart);
        FollowSuggestion();
    }

    public IReadOnlyList<WeekdayToggle> Days { get; }

    public AutostartViewModel Autostart { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepText), nameof(IsFirstStep), nameof(NextText), nameof(StepHeading), nameof(StepDescription))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    public partial int Step { get; private set; }

    [ObservableProperty]
    public partial TimeSpan? Start { get; set; }

    [ObservableProperty]
    public partial TimeSpan? End { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(BackCommand))]
    public partial bool IsSaving { get; private set; }

    public bool HasError => ErrorMessage.Length > 0;
    public bool IsFirstStep => Step == 0;

    // Namen für Screenreader: "von" allein wäre ohne Zusammenhang.
    public string StartLabel => $"{Loc.FieldWorkTime}, {Loc.FieldFrom}";
    public string EndLabel => $"{Loc.FieldWorkTime}, {Loc.FieldTo}";
    public string StepText => Loc.SetupStep(Step + 1, StepCount);
    public string NextText => Step == StepCount - 1 ? Loc.CommonFinish : Loc.CommonNext;

    public string StepHeading => Step switch
    {
        0 => Loc.SetupLanguageHeading,
        1 => Loc.SetupGoalsHeading,
        _ => Loc.SetupRemindersHeading,
    };

    public string StepDescription => Step switch
    {
        0 => Loc.SetupLanguageText,
        1 => Loc.SetupGoalsText,
        _ => Loc.SetupRemindersText,
    };

    /// <summary>Feld, das nach einem Fehler den Fokus bekommen soll.</summary>
    public event EventHandler<string>? FocusRequested;

    protected override int WorkDayCount => Days?.Count(d => d.IsOn) ?? 0;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        ErrorMessage = string.Empty;
        Step--;
    }

    private bool CanGoBack => Step > 0 && !IsSaving;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        ErrorMessage = Validate() ?? string.Empty;
        if (HasError) return;
        if (Step < StepCount - 1)
        {
            Step++;
            return;
        }
        await FinishAsync();
    }

    private bool CanGoNext => !IsSaving;

    private string? Validate()
    {
        switch (Step)
        {
            case 1:
                var error = NumberError();
                if (error is not null) FocusRequested?.Invoke(this, FirstEmptyField());
                return error;
            case 2:
                return WorkHoursValidator.Validate(BuildWorkHours()) switch
                {
                    WorkHoursError.NoWorkDay => Loc.ErrorNoWorkDay,
                    WorkHoursError.TimeRange => Loc.ErrorTimeRange,
                    _ => null,
                };
            default:
                return null;
        }
    }

    private string FirstEmptyField() =>
        double.IsNaN(DailyGoal) ? nameof(DailyGoal) : double.IsNaN(WeeklyGoal) ? nameof(WeeklyGoal) : nameof(Glass);

    private IReadOnlyList<WorkDay> BuildWorkHours()
    {
        var start = TimeOnly.FromTimeSpan(Start ?? TimeSpan.Zero);
        var end = TimeOnly.FromTimeSpan(End ?? TimeSpan.Zero);
        return [.. Days.Select(d => new WorkDay(d.IsOn, start, end))];
    }

    private async Task FinishAsync()
    {
        IsSaving = true;
        try
        {
            await Autostart.ApplyWishAsync();
            // Löst über SettingsService.Changed den Wechsel zur Startseite aus.
            _settings.Update(s => s with
            {
                SetupDone = true,
                Language = SelectedLanguage,
                DailyGoalMl = SettingsLimits.DailyGoalMl.Clamp(DailyGoal, s.DailyGoalMl),
                WeeklyGoalMl = SettingsLimits.WeeklyGoalMl.Clamp(WeeklyGoal, s.WeeklyGoalMl),
                GlassMl = SettingsLimits.GlassMl.Clamp(Glass, s.GlassMl),
                IntervalMin = SelectedIntervalMinutes,
                Autostart = Autostart.IsOn,
                WorkHours = BuildWorkHours(),
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Loc.ErrorSave(e.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    protected override void OnFieldChanged(string name)
    {
        ErrorMessage = string.Empty;
        switch (name)
        {
            // Sprache als Vorschau sofort umschalten, gespeichert wird erst bei "Fertig".
            case nameof(SelectedLanguageIndex):
                Loc.SetLanguage(SelectedLanguage);
                break;
            case nameof(DailyGoal):
                FollowSuggestion();
                break;
            case nameof(WeeklyGoal) when !_followingSuggestion:
                // Ab jetzt nicht mehr nachführen: der Nutzer hat das Wochenziel selbst gesetzt.
                _weeklyTouched = true;
                break;
        }
    }

    partial void OnStartChanged(TimeSpan? value) => ErrorMessage = string.Empty;
    partial void OnEndChanged(TimeSpan? value) => ErrorMessage = string.Empty;

    private void OnWorkHoursChanged()
    {
        ErrorMessage = string.Empty;
        FollowSuggestion();
    }

    /// <summary>Wochenziel = Tagesziel × Arbeitstage, solange der Nutzer es nicht selbst ändert.</summary>
    private void FollowSuggestion()
    {
        if (!_weeklyTouched && Suggestion is { } s)
        {
            _followingSuggestion = true;
            WeeklyGoal = s;
            _followingSuggestion = false;
        }
        RefreshSuggestion();
    }

    protected override void OnLanguageChanged(object? sender, EventArgs e)
    {
        base.OnLanguageChanged(sender, e);
        foreach (var day in Days) day.RefreshTexts();
        Autostart.RefreshTexts();
    }
}
