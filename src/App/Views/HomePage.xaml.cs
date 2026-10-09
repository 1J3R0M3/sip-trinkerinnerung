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
        InitializeComponent();
        ViewModel.Announce += (_, text) => Announcer.Announce(this, text);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.ErrorMessage)) Announcer.Announce(this, ViewModel.ErrorMessage);
        };
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    public HomeViewModel ViewModel { get; }
}
