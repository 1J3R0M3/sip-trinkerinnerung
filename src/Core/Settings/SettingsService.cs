using System.Text.Json;
using Trinkwasser.Core.Storage;

namespace Trinkwasser.Core.Settings;

public sealed class SettingsChangedEventArgs(AppSettings previous, AppSettings current) : EventArgs
{
    public AppSettings Previous { get; } = previous;
    public AppSettings Current { get; } = current;
}

/// <summary>Lädt, prüft und speichert die Einstellungen (<c>settings.json</c>).</summary>
public sealed class SettingsService(JsonFileStore store, string? windowsLanguage)
{
    public const string FileName = "settings.json";

    private readonly AppSettings _defaults = AppSettings.Defaults(windowsLanguage);

    public AppSettings Current { get; private set; } = AppSettings.Defaults(windowsLanguage);

    /// <summary>
    /// Gesetzt, wenn die Datei beim Laden unlesbar war und gesichert wurde (Pfad der Sicherung).
    /// Leerer Text: unlesbar, aber das Sichern ist fehlgeschlagen.
    /// </summary>
    public string? BrokenFileBackup { get; private set; }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public AppSettings Load()
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

        Current = _defaults;
        if (json is not null)
        {
            try
            {
                Current = SettingsSanitizer.FromJson(json, _defaults);
            }
            catch (JsonException)
            {
                BrokenFileBackup = store.Backup(FileName) ?? string.Empty;
            }
        }
        return Current;
    }

    /// <summary>
    /// Übernimmt eine Änderung: prüfen, atomar speichern, dann <see cref="Changed"/> auslösen.
    /// Schlägt das Speichern fehl (<see cref="IOException"/> oder <see cref="UnauthorizedAccessException"/>),
    /// bleibt <see cref="Current"/> unverändert.
    /// </summary>
    public AppSettings Update(Func<AppSettings, AppSettings> change)
    {
        var previous = Current;
        var next = SettingsSanitizer.Sanitize(change(previous), previous);
        if (next.Equals(previous)) return previous;

        store.Write(FileName, SettingsSanitizer.ToJson(next));
        Current = next;
        Changed?.Invoke(this, new SettingsChangedEventArgs(previous, next));
        return next;
    }
}
