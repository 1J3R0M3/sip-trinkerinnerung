using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Trinkwasser.App.Services;
using Trinkwasser.App.ViewModels;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Notifications;
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

        Loc = new Localizer(GlobalizationPreferences.Languages.FirstOrDefault() ?? "en", AppResources.AppName);
        Notifications = new NotificationService(Loc);
        Main = new MainViewModel(Loc, SendTestNotification);
    }

    public static new App Current => (App)Application.Current;

    public Localizer Loc { get; }
    public NotificationService Notifications { get; }
    public MainViewModel Main { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        Notifications.ActionInvoked += action => _dispatcher.TryEnqueue(() => HandleToastAction(action));
        Notifications.Register();

        _tray = new TrayService(Loc, ShowWindow, SendTestNotification, Quit);

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

    private void SendTestNotification() => Notifications.ShowReminder(250);

    private void ShowWindow()
    {
        _window ??= new MainWindow();
        _window.ShowAndActivate();
    }

    private void Quit()
    {
        _tray?.Dispose();
        Notifications.Unregister();
        _window?.CloseForExit();
        Exit();
    }
}
