using CommunityToolkit.Mvvm.ComponentModel;
using Trinkwasser.App.Services;
using Trinkwasser.Core.Achievements;
using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.App.ViewModels;

/// <summary>Ein Erfolg auf der Erfolgsseite.</summary>
/// <param name="Status">"Freigeschaltet am …", Fortschritt ("37,2 von 50 l") oder "Noch gesperrt".</param>
/// <param name="ProgressPercent">Für den Balken; null = kein Balken (freigeschaltet oder ohne Zwischenstand).</param>
public sealed record AchievementItem(
    string Name,
    string Description,
    string Status,
    double? ProgressPercent,
    bool IsUnlocked,
    Uri Badge,
    string AutomationName);

/// <summary>Eine Kategorie mit ihren Erfolgen.</summary>
public sealed record AchievementGroup(string Title, IReadOnlyList<AchievementItem> Items);

/// <summary>Alle Erfolge nach Kategorien, mit Fortschritt; aktualisiert sich bei jedem Glas.</summary>
public sealed partial class AchievementsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settings;
    private readonly DrinkLog _log;
    private readonly AchievementTracker _tracker;

    public AchievementsViewModel(Localizer loc, SettingsService settings, DrinkLog log, AchievementTracker tracker)
    {
        Loc = loc;
        _settings = settings;
        _log = log;
        _tracker = tracker;
        Loc.LanguageChanged += OnChanged;
        _settings.Changed += OnChanged;
        _log.Changed += OnChanged;
        _tracker.Changed += OnChanged;
        Refresh();
    }

    public Localizer Loc { get; }

    [ObservableProperty]
    public partial IReadOnlyList<AchievementGroup> Groups { get; private set; } = [];

    [ObservableProperty]
    public partial string Summary { get; private set; } = string.Empty;

    public string SinceText => Loc.AchSince(_tracker.State.Since);

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    public void Refresh()
    {
        var progress = _tracker.Progress(_log.Entries, _settings.Current);
        Summary = Loc.AchSummary(progress.Count(p => p.IsUnlocked), progress.Count);
        Groups = [.. progress.GroupBy(p => p.Achievement.Category)
            .Select(g => new AchievementGroup(Loc.AchCategory(g.Key), [.. g.Select(ToItem)]))];
        OnPropertyChanged(nameof(SinceText));
    }

    private AchievementItem ToItem(AchievementProgress p)
    {
        var id = p.Achievement.Id;
        var progressText = Loc.AchProgress(p);
        var status = p.UnlockedAt is { } at ? Loc.AchUnlockedOn(at)
            : progressText.Length > 0 ? progressText
            : Loc.AchLocked;
        double? percent = p.IsUnlocked || progressText.Length == 0 || p.Target <= 0 ? null : 100.0 * p.Current / p.Target;
        return new AchievementItem(Loc.AchName(id), Loc.AchDescription(id), status, percent, p.IsUnlocked,
            NotificationService.BadgeUri(id, p.IsUnlocked), Loc.AchAutomationName(p));
    }

    public void Dispose()
    {
        Loc.LanguageChanged -= OnChanged;
        _settings.Changed -= OnChanged;
        _log.Changed -= OnChanged;
        _tracker.Changed -= OnChanged;
    }
}
