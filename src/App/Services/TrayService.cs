using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Trinkwasser.Core.Localization;

namespace Trinkwasser.App.Services;

/// <summary>Symbol im Infobereich mit Kontextmenü. Linksklick öffnet das Fenster.</summary>
public sealed class TrayService : IDisposable
{
    private readonly Localizer _loc;
    private readonly TaskbarIcon _icon;
    private readonly MenuFlyoutItem _open, _test, _quit;

    public TrayService(Localizer loc, Action open, Action sendTest, Action quit)
    {
        _loc = loc;
        _open = new MenuFlyoutItem { Command = new RelayCommand(open) };
        _test = new MenuFlyoutItem { Command = new RelayCommand(sendTest) };
        _quit = new MenuFlyoutItem { Command = new RelayCommand(quit) };

        var menu = new MenuFlyout();
        menu.Items.Add(_open);
        menu.Items.Add(_test);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_quit);

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico")),
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = menu,
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(open),
        };
        UpdateTexts();
        _loc.LanguageChanged += OnLanguageChanged;

        // Ohne Effizienzmodus: die App soll im Hintergrund pünktlich erinnern.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateTexts();

    private void UpdateTexts()
    {
        _icon.ToolTipText = _loc.TrayTooltip;
        _open.Text = _loc.TrayOpen;
        _test.Text = _loc.TrayTestNotification;
        _quit.Text = _loc.TrayQuit;
    }

    public void Dispose()
    {
        _loc.LanguageChanged -= OnLanguageChanged;
        _icon.Dispose();
    }
}
