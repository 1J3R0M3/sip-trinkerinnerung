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
        ViewModel = new SettingsViewModel(App.Current.Loc, App.Current.Settings, App.Current.Autostart);
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SettingsViewModel ViewModel { get; }

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
