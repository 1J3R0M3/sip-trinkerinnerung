using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.App.ViewModels;

/// <summary>Eintrag der Intervall-Auswahl; der Text wechselt mit der Sprache.</summary>
public sealed partial class IntervalOption(int minutes, string label) : ObservableObject
{
    public int Minutes { get; } = minutes;

    [ObservableProperty]
    public partial string Label { get; set; } = label;
}

/// <summary>
/// Gemeinsame Felder von Einrichtungsassistent und Einstellungen: Sprache, Ziele mit
/// Wochenziel-Vorschlag, Glasgröße und Intervall. Zahlenfelder sind <see cref="double"/>,
/// weil die NumberBox ein leeres Feld als <see cref="double.NaN"/> meldet.
/// </summary>
public abstract partial class FormViewModelBase : ObservableObject, IDisposable
{
    protected FormViewModelBase(Localizer loc, AppSettings settings)
    {
        Loc = loc;
        LanguageNames = [.. Localizer.Languages.Select(loc.LanguageName)];
        SelectedLanguageIndex = Math.Max(0, Localizer.Languages.ToList().IndexOf(settings.Language));
        DailyGoal = settings.DailyGoalMl;
        WeeklyGoal = settings.WeeklyGoalMl;
        Glass = settings.GlassMl;
        IntervalOptions = [.. SettingsLimits.IntervalChoicesWith(settings.IntervalMin)
            .Select(m => new IntervalOption(m, loc.Interval(m)))];
        SelectedIntervalIndex = IntervalOptions.ToList().FindIndex(o => o.Minutes == settings.IntervalMin);
        Loc.LanguageChanged += OnLanguageChanged;
        IsReady = true;
    }

    /// <summary>False, solange der Konstruktor die Startwerte setzt (keine Änderung durch den Nutzer).</summary>
    protected bool IsReady { get; private set; }

    public Localizer Loc { get; }

    /// <summary>Sprachnamen in der jeweiligen Sprache ("Deutsch", "English").</summary>
    public IReadOnlyList<string> LanguageNames { get; }

    public IReadOnlyList<IntervalOption> IntervalOptions { get; }

    public double DailyMin => SettingsLimits.DailyGoalMl.Min;
    public double DailyMax => SettingsLimits.DailyGoalMl.Max;
    public double WeeklyMin => SettingsLimits.WeeklyGoalMl.Min;
    public double WeeklyMax => SettingsLimits.WeeklyGoalMl.Max;
    public double GlassMin => SettingsLimits.GlassMl.Min;
    public double GlassMax => SettingsLimits.GlassMl.Max;

    [ObservableProperty]
    public partial int SelectedLanguageIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DailyLiters))]
    public partial double DailyGoal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeeklyLiters))]
    public partial double WeeklyGoal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GlassLiters))]
    public partial double Glass { get; set; }

    [ObservableProperty]
    public partial int SelectedIntervalIndex { get; set; }

    public string SelectedLanguage => Localizer.Languages[Math.Clamp(SelectedLanguageIndex, 0, Localizer.Languages.Count - 1)];
    public int SelectedIntervalMinutes => IntervalOptions[Math.Max(0, SelectedIntervalIndex)].Minutes;

    public string DailyLiters => Loc.Liters(DailyGoal);
    public string WeeklyLiters => Loc.Liters(WeeklyGoal);
    public string GlassLiters => Loc.Liters(Glass);

    /// <summary>Anzahl aktiver Arbeitstage für den Wochenziel-Vorschlag.</summary>
    protected abstract int WorkDayCount { get; }

    protected int? Suggestion => SettingsLimits.SuggestWeeklyGoal(DailyGoal, WorkDayCount);

    public bool ShowSuggestion => Suggestion is { } s && s != WeeklyGoal;

    public string SuggestionText => Suggestion is { } s ? Loc.WeeklyGoalSuggestion(s, WorkDayCount) : string.Empty;

    [RelayCommand]
    private void ApplySuggestion()
    {
        if (Suggestion is { } s) WeeklyGoal = s;
    }

    /// <summary>Muss aufgerufen werden, wenn sich Tagesziel, Wochenziel oder Arbeitstage ändern.</summary>
    protected virtual void RefreshSuggestion()
    {
        OnPropertyChanged(nameof(ShowSuggestion));
        OnPropertyChanged(nameof(SuggestionText));
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (IsReady) OnFieldChanged(nameof(SelectedLanguageIndex));
    }

    partial void OnDailyGoalChanged(double value)
    {
        if (!IsReady) return;
        RefreshSuggestion();
        OnFieldChanged(nameof(DailyGoal));
    }

    partial void OnWeeklyGoalChanged(double value)
    {
        if (!IsReady) return;
        RefreshSuggestion();
        OnFieldChanged(nameof(WeeklyGoal));
    }

    partial void OnGlassChanged(double value)
    {
        if (IsReady) OnFieldChanged(nameof(Glass));
    }

    partial void OnSelectedIntervalIndexChanged(int value)
    {
        if (IsReady) OnFieldChanged(nameof(SelectedIntervalIndex));
    }

    /// <summary>Ein Feld wurde nach dem Laden geändert (durch den Nutzer oder das ViewModel selbst).</summary>
    protected abstract void OnFieldChanged(string name);

    /// <summary>
    /// Erstes leeres Zahlenfeld als Fehlermeldung (oder null). Werte außerhalb der Grenzen
    /// kommen nicht vor, weil die NumberBox auf Minimum/Maximum klemmt.
    /// </summary>
    protected string? NumberError()
    {
        if (double.IsNaN(DailyGoal)) return RangeError(SettingsLimits.DailyGoalMl);
        if (double.IsNaN(WeeklyGoal)) return RangeError(SettingsLimits.WeeklyGoalMl);
        if (double.IsNaN(Glass)) return RangeError(SettingsLimits.GlassMl);
        return null;
    }

    protected string RangeError(Limit limit) => Loc.ErrorRange(limit.Min, limit.Max);

    protected virtual void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var option in IntervalOptions) option.Label = Loc.Interval(option.Minutes);
        // Alle abgeleiteten Texte (Liter, Vorschlag, Wochentage …) neu auswerten.
        OnPropertyChanged(string.Empty);
    }

    public virtual void Dispose()
    {
        Loc.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
