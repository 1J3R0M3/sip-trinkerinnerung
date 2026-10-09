using Microsoft.Windows.ApplicationModel.Resources;
using Trinkwasser.Core.Localization;

namespace Trinkwasser.App.Services;

/// <summary>Liest Werte aus den Paketressourcen (Strings/&lt;Sprache&gt;/Resources.resw).</summary>
public static class AppResources
{
    private static readonly ResourceManager Manager = new();
    private static readonly Dictionary<string, string> AppNames = [];

    /// <summary>App-Name in der gewünschten Sprache ("de" oder "en").</summary>
    public static string AppName(string language)
    {
        if (AppNames.TryGetValue(language, out var name)) return name;
        var context = Manager.CreateResourceContext();
        context.QualifierValues["Language"] = language == Localizer.German ? "de-DE" : "en-US";
        name = Manager.MainResourceMap.GetValue("Resources/AppDisplayName", context).ValueAsString;
        AppNames[language] = name;
        return name;
    }
}
