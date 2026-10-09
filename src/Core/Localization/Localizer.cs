using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Trinkwasser.Core.Localization;

/// <summary>
/// Liefert die Oberflächentexte aus den eingebetteten Sprachdateien (Locales/*.json).
/// x:Uid aus .resw aktualisiert sich nicht zur Laufzeit; deshalb bindet die Oberfläche
/// an die Eigenschaften dieser Klasse. Beim Sprachwechsel wird PropertyChanged mit
/// leerem Namen ausgelöst, damit sich alle Bindungen neu auswerten.
/// </summary>
public sealed partial class Localizer : ObservableObject
{
    public const string German = "de";
    public const string English = "en";
    public static IReadOnlyList<string> Languages { get; } = [German, English];

    private static readonly Dictionary<string, Dictionary<string, string>> Tables =
        Languages.ToDictionary(lang => lang, LoadTable);

    // Der App-Name wird nicht in den JSON-Dateien gepflegt, sondern zentral in den
    // Paketressourcen (Strings/<Sprache>/Resources.resw) und hier als {app} eingesetzt.
    private readonly Func<string, string> _appName;

    public Localizer(string language, Func<string, string> appName)
    {
        _appName = appName;
        Language = Normalize(language);
    }

    public string Language { get; private set; }

    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language == German ? "de-DE" : "en-US");

    public event EventHandler? LanguageChanged;

    /// <summary>"de-AT" → "de", alles Unbekannte → "en".</summary>
    public static string Normalize(string? language) =>
        language is not null && language.StartsWith(German, StringComparison.OrdinalIgnoreCase) ? German : English;

    public void SetLanguage(string language)
    {
        var lang = Normalize(language);
        if (lang == Language) return;
        Language = lang;
        OnPropertyChanged(string.Empty);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static IReadOnlyCollection<string> Keys(string language) => Tables[Normalize(language)].Keys;

    public string Get(string key)
    {
        if (!Tables[Language].TryGetValue(key, out var text) && !Tables[English].TryGetValue(key, out text))
            return key;
        return text.Replace("{app}", _appName(Language), StringComparison.Ordinal);
    }

    /// <summary>Ersetzt Platzhalter wie {amount}; Zahlen werden passend zur Sprache formatiert.</summary>
    public string Format(string key, params (string Name, object Value)[] args)
    {
        var text = Get(key);
        foreach (var (name, value) in args)
        {
            var formatted = value is IFormattable f ? f.ToString(value is int or long ? "N0" : null, Culture) : value.ToString();
            text = text.Replace("{" + name + "}", formatted, StringComparison.Ordinal);
        }
        return text;
    }

    private static Dictionary<string, string> LoadTable(string language)
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"Locales.{language}.json")
            ?? throw new InvalidOperationException($"Sprachdatei {language}.json fehlt.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Sprachdatei {language}.json ist leer.");
    }

    // Typisierte Eigenschaften für {x:Bind}. Texte mit Platzhaltern sind Methoden.
    public string AppName => _appName(Language);

    public string TrayTooltip => Get("tray.tooltip");
    public string TrayOpen => Get("tray.open");
    public string TraySettings => Get("tray.settings");
    public string TrayTestNotification => Get("tray.testNotification");
    public string TrayQuit => Get("tray.quit");

    public string ToastTitle => Get("toast.title");
    public string ToastBody(int amountMl) => Format("toast.body", ("amount", amountMl));
    public string ToastDrank => Get("toast.drank");
    public string ToastSnooze(int minutes) => Format("toast.snooze", ("minutes", minutes));

    public string HomeTestButton => Get("home.testButton");
    public string HomeHint => Get("home.hint");
    public string HomeActionsHeading => Get("home.actionsHeading");
    public string HomeActionsEmpty => Get("home.actionsEmpty");
}
