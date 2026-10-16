using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trinkwasser.App.Services;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Sounds;

namespace Trinkwasser.App.ViewModels;

/// <summary>Eintrag der Tonauswahl; der Text wechselt mit der Sprache.</summary>
public sealed partial class SoundOption(string id, string label) : ObservableObject
{
    public string Id { get; } = id;

    [ObservableProperty]
    public partial string Label { get; set; } = label;
}

/// <summary>Ton für einen Anlass in den Einstellungen: Auswahl und Probehören.</summary>
public sealed partial class SoundRow : ObservableObject
{
    private readonly Localizer _loc;
    private readonly SoundPreview _preview;
    private readonly Action<SoundOccasion, string> _changed;

    public SoundRow(Localizer loc, SoundPreview preview, SoundOccasion occasion, string selected, Action<SoundOccasion, string> changed)
    {
        _loc = loc;
        _preview = preview;
        Occasion = occasion;
        Options = [.. SoundCatalog.All.Select(id => new SoundOption(id, loc.SoundName(id)))];
        SelectedIndex = Math.Max(0, SoundCatalog.All.ToList().IndexOf(selected));
        // Erst danach: der Startwert ist keine Änderung durch den Nutzer.
        _changed = changed;
    }

    public SoundOccasion Occasion { get; }

    public IReadOnlyList<SoundOption> Options { get; }

    public string Name => _loc.SoundOccasionName(Occasion);
    public string PlayLabel => _loc.SoundPlay;
    public string PlayName => _loc.SoundPlayFor(Occasion);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    public partial int SelectedIndex { get; set; }

    private string SelectedId => Options[Math.Clamp(SelectedIndex, 0, Options.Count - 1)].Id;

    partial void OnSelectedIndexChanged(int value)
    {
        // Beim Aufbau ist _changed noch nicht gesetzt; die ComboBox meldet beim Leeren -1.
        if (value >= 0) _changed?.Invoke(Occasion, SelectedId);
    }

    private bool CanPlay() => SelectedId != SoundCatalog.Silent;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play() => _preview.Play(SelectedId);

    public void RefreshTexts()
    {
        foreach (var option in Options) option.Label = _loc.SoundName(option.Id);
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayName));
    }
}
