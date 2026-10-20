using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Trinkwasser.App.ViewModels;

namespace Trinkwasser.App.Views;

public sealed partial class AchievementsPage : Page
{
    public AchievementsPage()
    {
        var app = App.Current;
        ViewModel = new AchievementsViewModel(app.Loc, app.Settings, app.Drinks, app.Achievements);
        InitializeComponent();
        // "Heute …" und "Diese Woche …" stimmen auch, wenn die Seite über Mitternacht offen war.
        Loaded += (_, _) => ViewModel.Refresh();
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    public AchievementsViewModel ViewModel { get; }

    // Hilfsfunktionen für x:Bind in der Vorlage.

    /// <summary>Badges sind 256 px groß; passend zur Anzeigegröße dekodieren, sonst wirken sie unscharf.</summary>
    public static ImageSource BadgeImage(Uri uri) =>
        new BitmapImage(uri) { DecodePixelType = DecodePixelType.Logical, DecodePixelWidth = 72 };

    public static Visibility Locked(bool unlocked) => unlocked ? Visibility.Collapsed : Visibility.Visible;

    public static double Percent(double? percent) => percent ?? 0;

    public static Visibility HasProgress(double? percent) => percent is null ? Visibility.Collapsed : Visibility.Visible;
}
