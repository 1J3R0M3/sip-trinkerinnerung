using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Trinkwasser.App.Services;
using Trinkwasser.App.ViewModels;

namespace Trinkwasser.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = new SettingsViewModel(App.Current.Loc, App.Current.Settings, App.Current.Autostart, App.Current.Sounds);
        InitializeComponent();
        _panels = [GeneralPanel, GoalsPanel, RemindersPanel, WorkHoursPanel, SoundsPanel, InfoPanel];
        Categories.SelectedIndex = 0;
        ViewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // Reihenfolge wie die Einträge in der Liste links.
    private readonly StackPanel[] _panels;

    public SettingsViewModel ViewModel { get; }

    /// <summary>Zeigt nur den gewählten Bereich und springt an dessen Anfang.</summary>
    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        // Beim Aufbau feuert das Ereignis, bevor _panels gesetzt ist.
        if (_panels is null || Categories.SelectedIndex < 0) return;
        for (var i = 0; i < _panels.Length; i++)
            _panels[i].Visibility = i == Categories.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
        ContentScroller.ChangeView(null, 0, null, disableAnimation: true);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Current.WindowActivated += OnWindowActivated;
        await ViewModel.Autostart.RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Current.WindowActivated -= OnWindowActivated;
        ViewModel.Dispose();
    }

    // Zurück aus den Windows-Einstellungen: der Autostart kann sich dort geändert haben.
    private async void OnWindowActivated(object? sender, EventArgs e) => await ViewModel.Autostart.RefreshAsync();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ErrorMessage))
            Announcer.Announce(this, ViewModel.ErrorMessage);
    }
}
