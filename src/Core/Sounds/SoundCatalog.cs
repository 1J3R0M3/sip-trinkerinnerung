namespace Trinkwasser.Core.Sounds;

/// <summary>Anlass einer Benachrichtigung; jeder Anlass hat einen eigenen, einstellbaren Ton.</summary>
public enum SoundOccasion
{
    Reminder,
    Snooze,
    GoalReached,
}

/// <summary>
/// Die wählbaren Töne. Gespeichert wird die Id: ein mitgelieferter Sip-Ton
/// (<c>Assets/Sounds/&lt;id&gt;.wav</c>), <see cref="Windows"/> oder <see cref="Silent"/>.
/// </summary>
public static class SoundCatalog
{
    /// <summary>Standardton von Windows für Benachrichtigungen.</summary>
    public const string Windows = "windows";

    /// <summary>Ohne Ton; die Benachrichtigung erscheint trotzdem.</summary>
    public const string Silent = "silent";

    /// <summary>Mitgelieferte Töne in der Reihenfolge der Auswahlliste.</summary>
    public static IReadOnlyList<string> SipSounds { get; } =
    [
        // Tropfen
        "drop", "clear-drop", "soft-drop", "quick-drop", "plip",
        // Glas
        "wine-glass", "crystal", "glass-ping", "ice-cube",
        // Wasser
        "quick-pour", "pour", "plop", "bubbles",
    ];

    public static IReadOnlyList<string> All { get; } = [.. SipSounds, Windows, Silent];

    public static bool IsKnown(string? id) => id is not null && All.Contains(id);

    public static bool IsSipSound(string id) => SipSounds.Contains(id);

    public static string Default(SoundOccasion occasion) => occasion switch
    {
        SoundOccasion.Snooze => "quick-pour",
        SoundOccasion.GoalReached => "glass-ping",
        _ => "drop",
    };
}
