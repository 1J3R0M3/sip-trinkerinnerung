using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trinkwasser.Core.Localization;
using Trinkwasser.Core.Sounds;

namespace Trinkwasser.Core.Settings;

/// <summary>
/// Prüft Einstellungen aus der Datei und aus der Oberfläche (Logik aus dem Electron-Prototyp):
/// Zahlen werden gerundet und geklemmt, ungültige Werte fallen auf den vorherigen Wert zurück.
/// Bei Arbeitszeiten, deren Ende nicht nach dem Beginn liegt, bleiben die vorherigen Zeiten
/// des Tages erhalten; übernommen wird dann nur <see cref="WorkDay.Enabled"/>.
/// </summary>
public static class SettingsSanitizer
{
    public const string TimeFormat = "HH:mm";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Prüft typisierte Werte, z. B. aus der Oberfläche.</summary>
    public static AppSettings Sanitize(AppSettings input, AppSettings previous) => previous with
    {
        Version = AppSettings.CurrentVersion,
        SetupDone = input.SetupDone,
        Language = Localizer.Languages.Contains(input.Language) ? input.Language : previous.Language,
        DailyGoalMl = SettingsLimits.DailyGoalMl.Clamp(input.DailyGoalMl, previous.DailyGoalMl),
        WeeklyGoalMl = SettingsLimits.WeeklyGoalMl.Clamp(input.WeeklyGoalMl, previous.WeeklyGoalMl),
        GlassMl = SettingsLimits.GlassMl.Clamp(input.GlassMl, previous.GlassMl),
        IntervalMin = SettingsLimits.IntervalMin.Clamp(input.IntervalMin, previous.IntervalMin),
        Autostart = input.Autostart,
        Theme = Enum.IsDefined(input.Theme) ? input.Theme : previous.Theme,
        ReminderSound = SoundCatalog.IsKnown(input.ReminderSound) ? input.ReminderSound : previous.ReminderSound,
        SnoozeSound = SoundCatalog.IsKnown(input.SnoozeSound) ? input.SnoozeSound : previous.SnoozeSound,
        GoalSound = SoundCatalog.IsKnown(input.GoalSound) ? input.GoalSound : previous.GoalSound,
        WorkHours = input.WorkHours is { Count: 7 } days
            ? [.. days.Select((d, i) => SanitizeDay(d.Enabled, d.Start, d.End, previous.WorkHours[i]))]
            : previous.WorkHours,
    };

    /// <summary>
    /// Liest die gespeicherte JSON-Datei. Einzelne ungültige Werte fallen auf
    /// <paramref name="defaults"/> zurück. Ist der Text gar kein JSON-Objekt, wird
    /// <see cref="JsonException"/> ausgelöst – die Datei gilt dann als kaputt.
    /// </summary>
    public static AppSettings FromJson(string json, AppSettings defaults)
    {
        if (JsonNode.Parse(json) is not JsonObject o)
            throw new JsonException("Die Einstellungsdatei enthält kein JSON-Objekt.");

        var result = defaults with
        {
            SetupDone = Bool(o["setupDone"]) ?? defaults.SetupDone,
            Language = Text(o["language"]) is { } lang && Localizer.Languages.Contains(lang) ? lang : defaults.Language,
            DailyGoalMl = SettingsLimits.DailyGoalMl.Clamp(Number(o["dailyGoalMl"]), defaults.DailyGoalMl),
            WeeklyGoalMl = SettingsLimits.WeeklyGoalMl.Clamp(Number(o["weeklyGoalMl"]), defaults.WeeklyGoalMl),
            GlassMl = SettingsLimits.GlassMl.Clamp(Number(o["glassMl"]), defaults.GlassMl),
            IntervalMin = SettingsLimits.IntervalMin.Clamp(Number(o["intervalMin"]), defaults.IntervalMin),
            Autostart = Bool(o["autostart"]) ?? defaults.Autostart,
            Theme = ParseTheme(Text(o["theme"])) ?? defaults.Theme,
        };

        // Fehlt "sounds" (Einstellungen vor 1.2) oder ist ein Ton unbekannt, gilt der Standard.
        if (o["sounds"] is JsonObject sounds)
        {
            result = result with
            {
                ReminderSound = Sound(sounds["reminder"]) ?? defaults.ReminderSound,
                SnoozeSound = Sound(sounds["snooze"]) ?? defaults.SnoozeSound,
                GoalSound = Sound(sounds["goal"]) ?? defaults.GoalSound,
            };
        }

        if (o["workHours"] is JsonArray { Count: 7 } days)
        {
            result = result with
            {
                WorkHours = [.. days.Select((node, i) =>
                {
                    var fallback = defaults.WorkHours[i];
                    if (node is not JsonObject day) return fallback;
                    return SanitizeDay(
                        Bool(day["enabled"]) ?? false,
                        ParseTime(Text(day["start"])) ?? fallback.Start,
                        ParseTime(Text(day["end"])) ?? fallback.End,
                        fallback);
                })],
            };
        }
        return result;
    }

    public static string ToJson(AppSettings s)
    {
        var o = new JsonObject
        {
            ["version"] = s.Version,
            ["setupDone"] = s.SetupDone,
            ["language"] = s.Language,
            ["dailyGoalMl"] = s.DailyGoalMl,
            ["weeklyGoalMl"] = s.WeeklyGoalMl,
            ["glassMl"] = s.GlassMl,
            ["intervalMin"] = s.IntervalMin,
            ["autostart"] = s.Autostart,
            ["theme"] = FormatTheme(s.Theme),
            ["sounds"] = new JsonObject
            {
                ["reminder"] = s.ReminderSound,
                ["snooze"] = s.SnoozeSound,
                ["goal"] = s.GoalSound,
            },
            ["workHours"] = new JsonArray([.. s.WorkHours.Select(d => (JsonNode)new JsonObject
            {
                ["enabled"] = d.Enabled,
                ["start"] = FormatTime(d.Start),
                ["end"] = FormatTime(d.End),
            })]),
        };
        return o.ToJsonString(WriteOptions);
    }

    /// <summary>"08:00" → 08:00; alles andere (auch "8:00" oder "24:00") → null.</summary>
    public static TimeOnly? ParseTime(string? text) =>
        TimeOnly.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

    public static string FormatTime(TimeOnly time) => time.ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static string FormatTheme(AppTheme theme) => theme switch
    {
        AppTheme.Light => "light",
        AppTheme.Dark => "dark",
        _ => "system",
    };

    /// <summary>"system", "light", "dark" → Wert; alles andere → null.</summary>
    public static AppTheme? ParseTheme(string? text) => text switch
    {
        "system" => AppTheme.System,
        "light" => AppTheme.Light,
        "dark" => AppTheme.Dark,
        _ => null,
    };

    private static WorkDay SanitizeDay(bool enabled, TimeOnly start, TimeOnly end, WorkDay fallback)
    {
        // Gespeichert wird minutengenau; Sekunden abschneiden, damit der Vergleich stimmt.
        start = new TimeOnly(start.Hour, start.Minute);
        end = new TimeOnly(end.Hour, end.Minute);
        return end > start ? new WorkDay(enabled, start, end) : fallback with { Enabled = enabled };
    }

    private static bool? Bool(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static string? Text(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string? Sound(JsonNode? node) => Text(node) is { } id && SoundCatalog.IsKnown(id) ? id : null;

    // Wie Number() im Prototyp: Zahlen und Zahlen als Text werden akzeptiert.
    private static double Number(JsonNode? node)
    {
        if (node is not JsonValue v) return double.NaN;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<string>(out var s)
            && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
        return double.NaN;
    }
}
