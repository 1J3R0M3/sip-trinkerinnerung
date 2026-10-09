using Microsoft.Windows.ApplicationModel.Resources;
using Trinkwasser.Core.Localization;

namespace Trinkwasser.App.Services;

/// <summary>Liest Werte aus den Paketressourcen (Strings/&lt;Sprache&gt;/Resources.resw).</summary>
public static class AppResources
{
    private static readonly ResourceManager Manager = new();
    private static readonly Dictionary<string, string> AppNames = [];

    /// <summary>Kurzname der App für Texte und Tray ("Sip") in der gewünschten Sprache ("de" oder "en").</summary>
    public static string AppName(string language) => Get("AppShortName", language);

    /// <summary>Offizieller Name ("Sip-Trinkerinnerung" / "Sip-Waterreminder") – Startmenü, Store, Fenstertitel.</summary>
    public static string AppDisplayName(string language) => Get("AppDisplayName", language);

    private static string Get(string key, string language)
    {
        var cacheKey = $"{key}|{language}";
        if (AppNames.TryGetValue(cacheKey, out var value)) return value;
        var context = Manager.CreateResourceContext();
        context.QualifierValues["Language"] = language == Localizer.German ? "de-DE" : "en-US";
        value = Manager.MainResourceMap.GetValue("Resources/" + key, context).ValueAsString;
        AppNames[cacheKey] = value;
        return value;
    }
}
