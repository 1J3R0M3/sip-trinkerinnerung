using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Trinkwasser.App.Services;
using Trinkwasser.Core.Achievements;
using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Notifications;
using Trinkwasser.Core.Reminders;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Sounds;
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

        var store = new JsonFileStore(ApplicationData.Current.LocalFolder.Path, Clock);
        Settings = new SettingsService(store, GlobalizationPreferences.Languages.FirstOrDefault());
        Settings.Load();
        Drinks = new DrinkLog(store, Clock);
        Drinks.Load();
        Achievements = new AchievementTracker(store, Clock);
        Achievements.Load();

        Loc = new Localizer(Settings.Current.Language, AppResources.AppName);
        // Sprachwechsel ohne Neustart: Fenster, Tray und neue Benachrichtigungen folgen dem Localizer.
        Settings.Changed += (_, e) => Loc.SetLanguage(e.Current.Language);

        Autostart = new AutostartService();
        Notifications = new NotificationService(Loc);
        Reminders = new ReminderEngine(Clock, () => Settings.Current, () => Drinks.Last?.Time);
        Settings.Changed += (_, _) => Reminders.Reschedule();
        DrinkActions = new DrinkActions(Drinks, Reminders, Notifications, Settings, new DailyGoalNotice(Clock, Achievements.State.GoalNoticeOn), Achievements, LogError);
        Sounds = new SoundPreview();
    }

    public static new App Current => (App)Application.Current;

    public TimeProvider Clock { get; } = TimeProvider.System;
    public SettingsService Settings { get; }
    public DrinkLog Drinks { get; }
    public AchievementTracker Achievements { get; }
    public Localizer Loc { get; }
    public AutostartService Autostart { get; }
    public NotificationService Notifications { get; }
    public ReminderEngine Reminders { get; }
    public DrinkActions DrinkActions { get; }
    public SoundPreview Sounds { get; }

    /// <summary>Das Hauptfenster wurde aktiviert (z. B. nach Rückkehr aus den Windows-Einstellungen).</summary>
    public event EventHandler? WindowActivated;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        Notifications.ActionInvoked += action => _dispatcher.TryEnqueue(() => HandleToastAction(action));
        Notifications.Register();

        // Der Takt läuft im Hintergrund; gezeigt wird die Erinnerung auf dem UI-Thread.
        Reminders.Due += (_, kind) => _dispatcher.TryEnqueue(() => ShowReminder(kind));
        Reminders.Start();

        _tray = new TrayService(Loc, ShowWindow, ShowSettings, AddGlassFromTray, SendTestNotification, Quit);

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
        switch (action)
        {
            case ToastAction.Drank:
                TryAddGlass(fromReminder: true);
                break;
            case ToastAction.TestDrank:
                TryAddGlass();
                break;
            case ToastAction.Snooze:
                Reminders.Snooze();
                break;
            case ToastAction.Achievements:
                ShowAchievements();
                break;
            default:
                ShowWindow();
                break;
        }
    }

    private void ShowReminder(ReminderKind kind)
    {
        // Während der Einrichtung noch nicht erinnern.
        var s = Settings.Current;
        if (s.SetupDone)
            Notifications.ShowReminder(s.GlassMl, s.Sound(kind == ReminderKind.Snooze ? SoundOccasion.Snooze : SoundOccasion.Reminder));
    }

    private void AddGlassFromTray()
    {
        if (!TryAddGlass()) ShowWindow();
    }

    /// <summary>"Getrunken" außerhalb der Startseite. Speicherfehler landen im error.log.</summary>
    private bool TryAddGlass(bool fromReminder = false)
    {
        try
        {
            DrinkActions.AddGlass(fromReminder);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LogError(e);
            return false;
        }
    }

    internal void SendTestNotification() =>
        Notifications.ShowReminder(Settings.Current.GlassMl, Settings.Current.Sound(SoundOccasion.Reminder), test: true);

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

    private void ShowAchievements()
    {
        ShowWindow();
        _window!.ShowAchievements();
    }

    /// <summary>
    /// Schreibt Fehler nach <c>error.log</c> im LocalFolder – nur lokal, nichts wird
    /// versendet. Hilft bei Fehlerberichten von Nutzern.
    /// </summary>
    internal static void LogError(Exception exception)
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
        Reminders.Dispose();
        _tray?.Dispose();
        Notifications.Unregister();
        _window?.CloseForExit();
        Exit();
    }
}
