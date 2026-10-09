using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Trinkwasser.App.Views;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Settings;

namespace Trinkwasser.App;

public sealed partial class MainWindow : Window
{
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 720));
        if (AppWindow.Presenter is OverlappedPresenter overlapped)
        {
            // Darunter passen die Arbeitszeiten (zwei Uhrzeitfelder pro Zeile) nicht mehr nebeneinander.
            overlapped.PreferredMinimumWidth = 860;
            overlapped.PreferredMinimumHeight = 560;
        }
        UpdateTexts();
        BrokenFileBar.IsOpen = App.Current.Settings.BrokenFileBackup is not null;
        Loc.LanguageChanged += (_, _) => UpdateTexts();
        App.Current.Settings.Changed += OnSettingsChanged;
        ShowStartPage();

        // Schließen versteckt das Fenster nur; die App läuft im Tray weiter.
        AppWindow.Closing += (_, e) =>
        {
            if (_exiting) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
    }

    public Localizer Loc => App.Current.Loc;

    public void ShowAndActivate()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        AppWindow.Show();
        Activate();
    }

    /// <summary>Öffnet die Einstellungen; während der Einrichtung bleibt der Assistent stehen.</summary>
    public void ShowSettings()
    {
        if (RootFrame.Content is ShellPage shell) shell.ShowSettings();
    }

    public void CloseForExit()
    {
        _exiting = true;
        App.Current.Settings.Changed -= OnSettingsChanged;
        Close();
    }

    private void ShowStartPage()
    {
        var page = App.Current.Settings.Current.SetupDone ? typeof(ShellPage) : typeof(SetupPage);
        RootFrame.Navigate(page, null, new SuppressNavigationTransitionInfo());
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Previous.SetupDone != e.Current.SetupDone)
        {
            RootFrame.Navigate(e.Current.SetupDone ? typeof(ShellPage) : typeof(SetupPage), null,
                new DrillInNavigationTransitionInfo());
            RootFrame.BackStack.Clear();
        }
    }

    private void UpdateTexts()
    {
        Title = Loc.AppName;
        if (App.Current.Settings.BrokenFileBackup is { } backup)
            BrokenFileBar.Message = Loc.SettingsBrokenFile(backup);
    }
}
