using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Trinkwasser.App.ViewModels;
using Windows.UI.Text;

namespace Trinkwasser.App.Views;

public sealed partial class StatisticsPage : Page
{
    public StatisticsPage()
    {
        var app = App.Current;
        ViewModel = new StatisticsViewModel(app.Loc, app.Settings, app.Drinks, app.Clock);
        InitializeComponent();
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    public StatisticsViewModel ViewModel { get; }

    // Hilfsfunktionen für x:Bind in der Balken-Vorlage.
    public static Visibility NotReached(bool reached) => reached ? Visibility.Collapsed : Visibility.Visible;

    public static Thickness GoalMargin(double offset) => new(0, 0, 0, offset);

    public static FontWeight DayWeight(bool isToday) => isToday ? FontWeights.Bold : FontWeights.Normal;
}
