namespace Trinkwasser.Core.About;

/// <summary>Ein Eintrag der Lizenzliste unter Einstellungen → Info.</summary>
/// <param name="License">Kennung der Lizenz; der angezeigte Name kommt aus den Sprachdateien (<c>license.&lt;id&gt;</c>).</param>
public sealed record Credit(string Name, string Author, string License, string Url);

/// <summary>
/// Was Sip verwendet und unter welcher Lizenz: die App selbst, die mitgelieferten Bibliotheken
/// (nur was im Paket landet, keine Werkzeuge wie NAudio) und die Töne. Bei neuen Paketen oder
/// Tönen hier ergänzen; Tests prüfen, dass jeder Ton einen Eintrag hat.
/// </summary>
public static class Credits
{
    public const string Mit = "mit";
    public const string Cc0 = "cc0";
    public const string MicrosoftTerms = "microsoft";

    public const string RepositoryUrl = "https://github.com/1J3R0M3/sip-trinkerinnerung";
    public const string PrivacyUrl = RepositoryUrl + "/blob/main/PRIVACY.md";

    public static Credit App { get; } = new("Sip", "J. B. Dietrich", Mit, RepositoryUrl);

    public static IReadOnlyList<Credit> Libraries { get; } =
    [
        new(".NET", "Microsoft, .NET Foundation", Mit, "https://github.com/dotnet/runtime"),
        new("Windows App SDK / WinUI 3", "Microsoft", MicrosoftTerms, "https://github.com/microsoft/windowsappsdk"),
        new("CommunityToolkit.Mvvm", ".NET Foundation", Mit, "https://github.com/CommunityToolkit/dotnet"),
        new("H.NotifyIcon", "HavenDV", Mit, "https://github.com/HavenDV/H.NotifyIcon"),
    ];

    /// <summary>Herkunft der Töne nach Id (siehe auch src/App/Assets/Sounds/LICENSES.md).</summary>
    public static IReadOnlyDictionary<string, Credit> Sounds { get; } = new Dictionary<string, Credit>
    {
        ["drop"] = new("Drop - Water", "mattfinarelli", Cc0, "https://freesound.org/s/533146/"),
        ["clear-drop"] = new("Water_drop_9.wav", "Freesound", Cc0, "https://freesound.org/s/166325/"),
        ["soft-drop"] = new("Fine Water Drop", "qubodup", Cc0, "https://freesound.org/s/792927/"),
        ["quick-drop"] = new("Quick Water Droplet", "qubodup", Cc0, "https://freesound.org/s/792931/"),
        ["plip"] = new("Drip 5 [dry] [plip]", "magnuswaker", Cc0, "https://freesound.org/s/524222/"),
        ["wine-glass"] = new("wine glass clink", "fleurescence", Cc0, "https://freesound.org/s/573160/"),
        ["crystal"] = new("crystal-bell-08", "arseniiv", Cc0, "https://freesound.org/s/614832/"),
        ["glass-ping"] = new("Glass Ping", "tix99", Cc0, "https://freesound.org/s/745014/"),
        ["ice-cube"] = new("Ice cube falling in glass 5", "Valerie-Vivegnis", Cc0, "https://freesound.org/s/837694/"),
        ["quick-pour"] = new("Quick Pour", "elliotlp", Cc0, "https://freesound.org/s/235779/"),
        ["pour"] = new("Water is Poured into a Glass", "Mish7913", Cc0, "https://freesound.org/s/741348/"),
        ["plop"] = new("Water + pebble", "giddster", Cc0, "https://freesound.org/s/386429/"),
        ["bubbles"] = new("raw_bubblingdrink5", "swordofkings128", Cc0, "https://freesound.org/s/398029/"),
    };
}
