using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Trinkwasser.App.Services;
using Trinkwasser.App.ViewModels;

namespace Trinkwasser.App.Views;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        var app = App.Current;
        ViewModel = new HomeViewModel(app.Loc, app.Settings, app.Drinks, app.Reminders, app.DrinkActions,
            app.Clock, app.SendTestNotification);
        // Hinweis "Autostart ist aus" mit Schalter; wirkt sofort wie in den Einstellungen.
        Autostart = new AutostartViewModel(app.Loc, app.Autostart, applyImmediately: true, wish: app.Settings.Current.Autostart);
        Autostart.Applied += (_, on) => SaveAutostartWish(on);
        InitializeComponent();
        ViewModel.Announce += (_, text) => Announcer.Announce(this, text);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.ErrorMessage)) Announcer.Announce(this, ViewModel.ErrorMessage);
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public HomeViewModel ViewModel { get; }

    public AutostartViewModel Autostart { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Current.WindowActivated += OnWindowActivated;
        await Autostart.RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Current.WindowActivated -= OnWindowActivated;
        ViewModel.Dispose();
    }

    // Zurück aus den Windows-Einstellungen: der Autostart kann sich dort geändert haben.
    private async void OnWindowActivated(object? sender, EventArgs e) => await Autostart.RefreshAsync();

    private static void SaveAutostartWish(bool on)
    {
        try
        {
            App.Current.Settings.Update(s => s with { Autostart = on });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nur der gemerkte Wunsch; maßgeblich ist ohnehin die StartupTask in Windows.
        }
    }
}
