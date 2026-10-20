using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trinkwasser.Core.Achievements;

/// <summary>
/// Gespeicherter Stand der Erfolge (<c>achievements.json</c>).
/// </summary>
/// <param name="Since">Ab hier zählen Gläser für Erfolge (erster Start mit Erfolgen); ältere Einträge nicht.</param>
/// <param name="OnCueCount">Wie oft direkt in einer Erinnerung auf "Getrunken" geklickt wurde.</param>
/// <param name="Unlocked">Freigeschaltete Erfolge mit Zeitpunkt. Einmal freigeschaltet, bleibt es dabei.</param>
/// <param name="GoalNoticeOn">Tag, an dem zuletzt "Tagesziel erreicht" gemeldet wurde (siehe <see cref="Drinks.DailyGoalNotice"/>).
/// Steht hier, damit die Meldung auch nach einem Neustart nur einmal am Tag kommt.</param>
public sealed record AchievementState(DateTimeOffset Since, int OnCueCount, ImmutableDictionary<string, DateTimeOffset> Unlocked,
    DateOnly? GoalNoticeOn = null)
{
    public const string FileName = "achievements.json";
    private const int CurrentVersion = 1;
    private const string DateFormat = "yyyy-MM-dd";

    public static AchievementState StartingAt(DateTimeOffset since) => new(since, 0, ImmutableDictionary<string, DateTimeOffset>.Empty);

    public bool IsUnlocked(string id) => Unlocked.ContainsKey(id);

    public string ToJson() => new JsonObject
    {
        ["version"] = CurrentVersion,
        ["since"] = Format(Since),
        ["onCue"] = OnCueCount,
        ["goalNoticeOn"] = GoalNoticeOn?.ToString(DateFormat, CultureInfo.InvariantCulture),
        // Nach Zeitpunkt sortiert, damit die Datei lesbar bleibt und sich nicht grundlos ändert.
        ["unlocked"] = new JsonObject(Unlocked.OrderBy(u => u.Value).ThenBy(u => u.Key, StringComparer.Ordinal)
            .Select(u => KeyValuePair.Create(u.Key, (JsonNode?)Format(u.Value)))),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>
    /// Liest die Datei. Unbekannte Erfolge und ungültige Zeitpunkte werden übersprungen; ohne
    /// gültiges <c>since</c> oder wenn der Text kein JSON-Objekt ist, wird <see cref="JsonException"/> ausgelöst.
    /// </summary>
    public static AchievementState Parse(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root || Time(root["since"]) is not { } since)
            throw new JsonException("Die Erfolgsdatei enthält keinen gültigen Startzeitpunkt \"since\".");

        var onCue = root["onCue"] is JsonValue v && v.TryGetValue<int>(out var n) && n >= 0 ? n : 0;
        var unlocked = ImmutableDictionary.CreateBuilder<string, DateTimeOffset>();
        if (root["unlocked"] is JsonObject list)
        {
            foreach (var (id, node) in list)
            {
                if (AchievementCatalog.Find(id) is not null && Time(node) is { } at) unlocked[id] = at;
            }
        }
        var goalNoticeOn = root["goalNoticeOn"] is JsonValue g && g.TryGetValue<string>(out var day)
            && DateOnly.TryParseExact(day, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : (DateOnly?)null;
        return new AchievementState(since, onCue, unlocked.ToImmutable(), goalNoticeOn);
    }

    private static string Format(DateTimeOffset time) => time.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static DateTimeOffset? Time(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var text)
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;
}
