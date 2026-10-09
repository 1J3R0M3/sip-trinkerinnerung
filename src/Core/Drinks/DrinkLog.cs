using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trinkwasser.Core.Storage;

namespace Trinkwasser.Core.Drinks;

/// <summary>Ein getrunkenes Glas.</summary>
public sealed record DrinkEntry(DateTimeOffset Time, int AmountMl);

/// <summary>
/// Protokoll der getrunkenen Gläser (<c>drinks.json</c>). <see cref="Entries"/> wird bei jeder
/// Änderung durch eine neue Liste ersetzt, damit andere Threads (Erinnerungs-Timer) sie
/// gefahrlos lesen können.
/// </summary>
public sealed class DrinkLog(JsonFileStore store, TimeProvider clock)
{
    public const string FileName = "drinks.json";
    public const int MaxAmountMl = 10000;
    private const int CurrentVersion = 1;

    private volatile DrinkEntry[] _entries = [];

    /// <summary>Alle Einträge, nach Zeit sortiert.</summary>
    public IReadOnlyList<DrinkEntry> Entries => _entries;

    public DrinkEntry? Last => _entries is [.., var last] ? last : null;

    /// <summary>Wie bei den Einstellungen: Pfad der Sicherung einer unlesbaren Datei, leer = Sichern fehlgeschlagen.</summary>
    public string? BrokenFileBackup { get; private set; }

    public event EventHandler? Changed;

    public void Load()
    {
        string? json;
        try
        {
            json = store.Read(FileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            json = null;
            BrokenFileBackup = store.Backup(FileName) ?? string.Empty;
        }

        _entries = [];
        if (json is null) return;
        try
        {
            _entries = Parse(json);
        }
        catch (JsonException)
        {
            BrokenFileBackup = store.Backup(FileName) ?? string.Empty;
        }
    }

    /// <summary>Trägt ein Glas mit der aktuellen Uhrzeit ein.</summary>
    public DrinkEntry Add(int amountMl)
    {
        var entry = new DrinkEntry(clock.GetLocalNow(), Math.Clamp(amountMl, 1, MaxAmountMl));
        Save([.. _entries.Append(entry).OrderBy(e => e.Time)]);
        return entry;
    }

    /// <summary>"Rückgängig": löscht den letzten Eintrag. Null, wenn es keinen gibt.</summary>
    public DrinkEntry? RemoveLast()
    {
        if (Last is not { } last) return null;
        Save(_entries[..^1]);
        return last;
    }

    /// <summary>Speichert erst, dann wird die Liste getauscht – schlägt das Schreiben fehl, bleibt alles beim Alten.</summary>
    private void Save(DrinkEntry[] entries)
    {
        store.Write(FileName, ToJson(entries));
        _entries = entries;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static string ToJson(IEnumerable<DrinkEntry> entries) => new JsonObject
    {
        ["version"] = CurrentVersion,
        ["entries"] = new JsonArray([.. entries.Select(e => (JsonNode)new JsonObject
        {
            ["time"] = e.Time.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            ["ml"] = e.AmountMl,
        })]),
    }.ToJsonString();

    /// <summary>
    /// Liest die Datei. Einzelne ungültige Einträge werden übersprungen; ist der Text gar kein
    /// passendes JSON-Objekt, wird <see cref="JsonException"/> ausgelöst.
    /// </summary>
    public static DrinkEntry[] Parse(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject { } root || root["entries"] is not JsonArray list)
            throw new JsonException("Die Trinkdaten enthalten keine Liste \"entries\".");

        var entries = new List<DrinkEntry>(list.Count);
        foreach (var node in list)
        {
            if (node is not JsonObject o) continue;
            if (o["time"] is not JsonValue t || !t.TryGetValue<string>(out var text)
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) continue;
            if (o["ml"] is not JsonValue m || !m.TryGetValue<int>(out var ml) || ml is < 1 or > MaxAmountMl) continue;
            entries.Add(new DrinkEntry(time, ml));
        }
        return [.. entries.OrderBy(e => e.Time)];
    }
}
