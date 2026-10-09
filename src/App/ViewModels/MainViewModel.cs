using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Notifications;

namespace Trinkwasser.App.ViewModels;

public sealed partial class MainViewModel(Localizer loc, Action sendTest) : ObservableObject
{
    // Vorläufig für Schritt 1: zeigt, welche Klicks aus Benachrichtigungen angekommen sind.
    public ObservableCollection<string> ReceivedActions { get; } = [];

    public bool HasNoActions => ReceivedActions.Count == 0;

    [RelayCommand]
    private void SendTest() => sendTest();

    public void AddReceived(ToastAction action, DateTime time)
    {
        var t = time.ToString("t", loc.Culture);
        ReceivedActions.Insert(0, action switch
        {
            ToastAction.Drank => loc.Format("action.drank", ("time", t)),
            ToastAction.Snooze => loc.Format("action.snooze", ("time", t), ("minutes", ToastArguments.SnoozeMinutes)),
            _ => loc.Format("action.open", ("time", t)),
        });
        OnPropertyChanged(nameof(HasNoActions));
    }
}
