using System.Reflection;
using System.Text.RegularExpressions;
using Trinkwasser.Core.Localization;

namespace Trinkwasser.Core.Tests;

public class LocalizerTests
{
    private static Localizer Create(string lang) => new(lang, l => l == "de" ? "Trinkwasser" : "Drink Water");

    [Fact]
    public void BothLanguagesHaveTheSameKeys() =>
        Assert.Equal(Localizer.Keys("de").Order(), Localizer.Keys("en").Order());

    [Fact]
    public void BothLanguagesUseTheSamePlaceholders()
    {
        var de = Create("de");
        var en = Create("en");
        foreach (var key in Localizer.Keys("de"))
            Assert.True(Placeholders(de.Get(key)).SetEquals(Placeholders(en.Get(key))), key);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void EveryTypedPropertyResolvesToAText(string lang)
    {
        var loc = Create(lang);
        var props = typeof(Localizer).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.Name != nameof(Localizer.Language));
        foreach (var p in props)
        {
            var value = (string)p.GetValue(loc)!;
            // Fehlt ein Schlüssel, liefert Get() den Schlüssel selbst zurück, z. B. "home.title".
            Assert.False(Regex.IsMatch(value, @"^[a-z]+\.[A-Za-z.]+$"), $"{p.Name}: {value}");
        }
    }

    [Fact]
    public void InsertsAppNameAndFormatsNumbersPerLanguage()
    {
        var loc = Create("de");
        Assert.Equal("Trinkwasser – Erinnerung", loc.TrayTooltip);
        Assert.Contains("1.500 ml", loc.ToastBody(1500));
        loc.SetLanguage("en-US");
        Assert.Equal("Drink Water – reminder", loc.TrayTooltip);
        Assert.Contains("1,500 ml", loc.ToastBody(1500));
    }

    [Fact]
    public void LanguageChangeNotifiesAllBindingsOnce()
    {
        var loc = Create("de");
        var names = new List<string?>();
        loc.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        loc.SetLanguage("en");
        loc.SetLanguage("en");
        Assert.Equal([string.Empty], names);
    }

    [Theory]
    [InlineData("de-AT", "de")]
    [InlineData("DE", "de")]
    [InlineData("fr-FR", "en")]
    [InlineData(null, "en")]
    public void NormalizesLanguage(string? input, string expected) =>
        Assert.Equal(expected, Localizer.Normalize(input));

    private static HashSet<string> Placeholders(string text) =>
        [.. Regex.Matches(text, @"\{\w+\}").Select(m => m.Value)];
}
