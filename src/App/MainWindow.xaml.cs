using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Trinkwasser.App.ViewModels;
using Trinkwasser.Core.Localization;

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
        AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 680));
        UpdateTitle();
        Loc.LanguageChanged += (_, _) => UpdateTitle();

        // Schließen versteckt das Fenster nur; die App läuft im Tray weiter.
        AppWindow.Closing += (_, e) =>
        {
            if (_exiting) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
    }

    public Localizer Loc => App.Current.Loc;
    public MainViewModel ViewModel => App.Current.Main;

    public void ShowAndActivate()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        AppWindow.Show();
        Activate();
    }

    public void CloseForExit()
    {
        _exiting = true;
        Close();
    }

    private void UpdateTitle() => Title = Loc.AppName;
}
