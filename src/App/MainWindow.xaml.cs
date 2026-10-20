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
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 720));
        if (AppWindow.Presenter is OverlappedPresenter overlapped)
        {
            // Darunter passen die Arbeitszeiten (zwei Uhrzeitfelder pro Zeile) neben der
            // Bereichsliste der Einstellungen nicht mehr nebeneinander.
            overlapped.PreferredMinimumWidth = 1040;
            overlapped.PreferredMinimumHeight = 560;
        }
        UpdateTexts();
        BrokenFileBar.IsOpen = BrokenFileBar.Message.Length > 0;
        Loc.LanguageChanged += (_, _) => UpdateTexts();
        App.Current.Settings.Changed += OnSettingsChanged;
        ApplyTheme(App.Current.Settings.Current.Theme);
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

    /// <summary>Öffnet die Erfolge (Klick auf "Erfolg freigeschaltet"); während der Einrichtung bleibt der Assistent stehen.</summary>
    public void ShowAchievements()
    {
        if (RootFrame.Content is ShellPage shell) shell.ShowAchievements();
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

    /// <summary>
    /// Hell, dunkel oder wie Windows. Gilt für den Fensterinhalt (inkl. Mica) und die
    /// Titelleisten-Buttons; das Tray-Menü und Benachrichtigungen folgen immer Windows.
    /// </summary>
    private void ApplyTheme(AppTheme theme)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            AppTheme.Light => TitleBarTheme.Light,
            AppTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Previous.Theme != e.Current.Theme) ApplyTheme(e.Current.Theme);
        if (e.Previous.SetupDone != e.Current.SetupDone)
        {
            RootFrame.Navigate(e.Current.SetupDone ? typeof(ShellPage) : typeof(SetupPage), null,
                new DrillInNavigationTransitionInfo());
            RootFrame.BackStack.Clear();
        }
    }

    private void UpdateTexts()
    {
        // Fenstertitel (Taskleiste, Alt+Tab) und Titelleiste: offizieller Name in der App-Sprache.
        Title = TitleText.Text = Services.AppResources.AppDisplayName(Loc.Language);
        var messages = new List<string>();
        if (App.Current.Settings.BrokenFileBackup is { } settingsBackup) messages.Add(Loc.SettingsBrokenFile(settingsBackup));
        if (App.Current.Drinks.BrokenFileBackup is { } drinksBackup) messages.Add(Loc.DrinksBrokenFile(drinksBackup));
        if (App.Current.Achievements.BrokenFileBackup is { } achievementsBackup) messages.Add(Loc.AchBrokenFile(achievementsBackup));
        BrokenFileBar.Message = string.Join(Environment.NewLine, messages);
    }
}
