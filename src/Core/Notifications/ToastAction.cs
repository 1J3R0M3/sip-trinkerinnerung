namespace Trinkwasser.Core.Notifications;

/// <summary>Was der Nutzer in einer Erinnerung angeklickt hat.</summary>
public enum ToastAction
{
    /// <summary>Klick auf die Benachrichtigung selbst.</summary>
    Open,
    Drank,
    Snooze,
}

/// <summary>Übersetzt zwischen <see cref="ToastAction"/> und den Argumenten der Benachrichtigung.</summary>
public static class ToastArguments
{
    public const string ActionKey = "action";
    public const int SnoozeMinutes = 10;

    public static string ToArgument(ToastAction action) => action switch
    {
        ToastAction.Drank => "drank",
        ToastAction.Snooze => "snooze",
        _ => "open",
    };

    /// <summary>Unbekannte oder fehlende Argumente gelten als "Öffnen".</summary>
    public static ToastAction Parse(IDictionary<string, string> arguments) =>
        arguments.TryGetValue(ActionKey, out var value)
            ? value switch
            {
                "drank" => ToastAction.Drank,
                "snooze" => ToastAction.Snooze,
                _ => ToastAction.Open,
            }
            : ToastAction.Open;
}
