using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trinkwasser.App.Services;
using Trinkwasser.Core.Localization;

namespace Trinkwasser.App.ViewModels;

/// <summary>
/// Schalter "Beim Anmelden an Windows starten". In den Einstellungen wirkt er sofort,
/// im Einrichtungsassistenten wird nur der Wunsch gemerkt und beim Fertigstellen angewendet.
/// </summary>
public sealed partial class AutostartViewModel : ObservableObject
{
    private readonly AutostartService _service;
    private readonly bool _applyImmediately;
    private bool _updating;

    public AutostartViewModel(Localizer loc, AutostartService service, bool applyImmediately, bool wish)
    {
        Loc = loc;
        _service = service;
        _applyImmediately = applyImmediately;
        _updating = true;
        IsOn = wish;
        _updating = false;
    }

    public Localizer Loc { get; }

    /// <summary>Nach einer Änderung durch den Nutzer: ist der Autostart jetzt an?</summary>
    public event EventHandler<bool>? Applied;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggle), nameof(StatusText), nameof(HasStatus), nameof(ShowOpenSettings), nameof(ShowOffHint), nameof(CanEnableHere))]
    public partial AutostartState State { get; private set; } = AutostartState.Disabled;

    /// <summary>Erst nach dem ersten <see cref="RefreshAsync"/> ist der Zustand aus Windows bekannt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOffHint))]
    public partial bool IsKnown { get; private set; }

    /// <summary>
    /// Hinweis auf der Startseite: Ohne Autostart läuft nach dem Windows-Start nichts, bis die App
    /// geöffnet wird. Nicht bei Richtlinien oder unverpackt – dort kann der Nutzer nichts tun.
    /// </summary>
    public bool ShowOffHint => IsKnown && State is AutostartState.Disabled or AutostartState.DisabledByUser;

    /// <summary>Direkt einschaltbar; nach "in Windows ausgeschaltet" geht es nur über die Windows-Einstellungen.</summary>
    public bool CanEnableHere => State == AutostartState.Disabled;

    [RelayCommand]
    private void Enable() => IsOn = true;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggle))]
    public partial bool IsBusy { get; private set; }

    public bool CanToggle => !IsBusy && State is AutostartState.Enabled or AutostartState.Disabled;

    public string StatusText => Loc.AutostartStatus(State);

    public bool HasStatus => StatusText.Length > 0;

    /// <summary>Nur der Nutzer selbst kann einen in Windows ausgeschalteten Autostart wieder einschalten.</summary>
    public bool ShowOpenSettings => State is AutostartState.DisabledByUser or AutostartState.EnabledByPolicy or AutostartState.DisabledByPolicy;

    /// <summary>Liest den aktuellen Zustand aus Windows (z. B. nach Rückkehr aus den Windows-Einstellungen).</summary>
    public async Task RefreshAsync()
    {
        State = await _service.GetStateAsync();
        IsKnown = true;
        // Im Assistenten bleibt der Wunsch stehen, solange Windows das Umschalten erlaubt.
        if (_applyImmediately || !CanToggle) SetIsOnSilently(IsActive(State));
    }

    /// <summary>Wendet den Wunsch aus dem Assistenten an.</summary>
    public async Task ApplyWishAsync()
    {
        if (!CanToggle || IsOn == IsActive(State)) return;
        State = await _service.SetEnabledAsync(IsOn);
    }

    public void RefreshTexts() => OnPropertyChanged(string.Empty);

    [RelayCommand]
    private Task OpenWindowsSettings() => AutostartService.OpenWindowsSettingsAsync();

    partial void OnIsOnChanged(bool value)
    {
        if (_updating || !_applyImmediately) return;
        _ = ToggleAsync(value);
    }

    private async Task ToggleAsync(bool enable)
    {
        IsBusy = true;
        try
        {
            State = await _service.SetEnabledAsync(enable);
        }
        finally
        {
            IsBusy = false;
        }
        SetIsOnSilently(IsActive(State));
        Applied?.Invoke(this, IsOn);
    }

    private void SetIsOnSilently(bool value)
    {
        _updating = true;
        IsOn = value;
        _updating = false;
    }

    private static bool IsActive(AutostartState state) => state is AutostartState.Enabled or AutostartState.EnabledByPolicy;
}
