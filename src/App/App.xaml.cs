using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Trinkwasser.App.Services;
using Trinkwasser.App.ViewModels;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Notifications;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Storage;
using Windows.Storage;
using Windows.System.UserProfile;

namespace Trinkwasser.App;

public partial class App : Application
{
    private readonly AppActivationArguments _startupActivation;
    private DispatcherQueue _dispatcher = null!;
    private TrayService? _tray;
    private MainWindow? _window;

    public App(AppActivationArguments startupActivation)
    {
        _startupActivation = startupActivation;
        InitializeComponent();
        // Die App lebt im Tray weiter, auch wenn kein Fenster offen ist.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        UnhandledException += (_, e) => LogError(e.Exception);

        var store = new JsonFileStore(ApplicationData.Current.LocalFolder.Path, TimeProvider.System);
        Settings = new SettingsService(store, GlobalizationPreferences.Languages.FirstOrDefault());
        Settings.Load();

        Loc = new Localizer(Settings.Current.Language, AppResources.AppName);
        // Sprachwechsel ohne Neustart: Fenster, Tray und neue Benachrichtigungen folgen dem Localizer.
        Settings.Changed += (_, e) => Loc.SetLanguage(e.Current.Language);

        Autostart = new AutostartService();
        Notifications = new NotificationService(Loc);
        Main = new MainViewModel(Loc, SendTestNotification);
    }

    public static new App Current => (App)Application.Current;

    public SettingsService Settings { get; }
    public Localizer Loc { get; }
    public AutostartService Autostart { get; }
    public NotificationService Notifications { get; }
    public MainViewModel Main { get; }

    /// <summary>Das Hauptfenster wurde aktiviert (z. B. nach Rückkehr aus den Windows-Einstellungen).</summary>
    public event EventHandler? WindowActivated;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        Notifications.ActionInvoked += action => _dispatcher.TryEnqueue(() => HandleToastAction(action));
        Notifications.Register();

        _tray = new TrayService(Loc, ShowWindow, ShowSettings, SendTestNotification, Quit);

        // Spätere Starts (zweite Instanz, Startmenü, Benachrichtigung) landen hier.
        AppInstance.GetCurrent().Activated += (_, e) => _dispatcher.TryEnqueue(() => HandleActivation(e));
        HandleActivation(_startupActivation);
    }

    private void HandleActivation(AppActivationArguments activation)
    {
        switch (activation.Kind)
        {
            case ExtendedActivationKind.AppNotification:
                // App war beendet und wurde per Klick auf eine Benachrichtigung gestartet.
                var data = (AppNotificationActivatedEventArgs)activation.Data;
                HandleToastAction(ToastArguments.Parse(data.Arguments));
                break;
            case ExtendedActivationKind.StartupTask:
                // Autostart beim Anmelden: nur im Tray, ohne Fenster. Ohne abgeschlossene
                // Einrichtung (z. B. Datei kaputt) trotzdem den Assistenten zeigen.
                if (!Settings.Current.SetupDone) ShowWindow();
                break;
            default:
                ShowWindow();
                break;
        }
    }

    private void HandleToastAction(ToastAction action)
    {
        Main.AddReceived(action, DateTime.Now);
        if (action == ToastAction.Open) ShowWindow();
    }

    private void SendTestNotification() => Notifications.ShowReminder(Settings.Current.GlassMl);

    private void ShowWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow();
            _window.Activated += (_, e) =>
            {
                if (e.WindowActivationState != WindowActivationState.Deactivated)
                    WindowActivated?.Invoke(this, EventArgs.Empty);
            };
        }
        _window.ShowAndActivate();
    }

    private void ShowSettings()
    {
        ShowWindow();
        _window!.ShowSettings();
    }

    /// <summary>
    /// Schreibt unbehandelte Fehler nach <c>error.log</c> im LocalFolder – nur lokal, nichts wird
    /// versendet. Hilft bei Fehlerberichten von Nutzern.
    /// </summary>
    private static void LogError(Exception exception)
    {
        try
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "error.log");
            File.AppendAllText(path, $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Protokollieren darf selbst nie zum Absturz führen.
        }
    }

    private void Quit()
    {
        _tray?.Dispose();
        Notifications.Unregister();
        _window?.CloseForExit();
        Exit();
    }
}
