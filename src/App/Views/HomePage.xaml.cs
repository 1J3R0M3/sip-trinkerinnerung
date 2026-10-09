using Microsoft.UI.Xaml.Controls;
using Trinkwasser.App.ViewModels;
using Trinkwasser.Core.Localization;

namespace Trinkwasser.App.Views;

public sealed partial class HomePage : Page
{
    public HomePage() => InitializeComponent();

    public Localizer Loc => App.Current.Loc;
    public MainViewModel ViewModel => App.Current.Main;
}
