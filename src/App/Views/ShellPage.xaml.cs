using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Trinkwasser.Core.Localization;

namespace Trinkwasser.App.Views;

/// <summary>Hauptansicht nach der Einrichtung: Navigation zwischen Übersicht, Statistik, Erfolgen und Einstellungen.</summary>
public sealed partial class ShellPage : Page
{
    public ShellPage()
    {
        InitializeComponent();
        Nav.SelectedItem = HomeItem;
    }

    public Localizer Loc => App.Current.Loc;

    public void ShowSettings() => Nav.SelectedItem = SettingsItem;

    public void ShowAchievements() => Nav.SelectedItem = AchievementsItem;

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var page = ((args.SelectedItem as NavigationViewItem)?.Tag as string) switch
        {
            "settings" => typeof(SettingsPage),
            "statistics" => typeof(StatisticsPage),
            "achievements" => typeof(AchievementsPage),
            _ => typeof(HomePage),
        };
        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page, null, new SuppressNavigationTransitionInfo());
    }
}
