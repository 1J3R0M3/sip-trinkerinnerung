using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Trinkwasser.App.Services;

/// <summary>
/// Lässt Screenreader (Sprachausgabe, NVDA …) einen Text vorlesen, z. B. eine
/// Fehlermeldung oder den neuen Schritt im Assistenten, ohne den Fokus zu verschieben.
/// </summary>
public static class Announcer
{
    public static void Announce(UIElement element, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var peer = FrameworkElementAutomationPeer.FromElement(element)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            AutomationNotificationProcessing.ImportantMostRecent,
            text,
            "Trinkwasser.Announcement");
    }
}
