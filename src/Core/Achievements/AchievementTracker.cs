using System.Text.Json;
using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Storage;

namespace Trinkwasser.Core.Achievements;

/// <summary>
/// Hält den Stand der Erfolge und speichert ihn in <c>achievements.json</c>. Gibt es die Datei
/// noch nicht (erster Start mit Erfolgen), beginnt die Zählung jetzt – ältere Gläser zählen nicht.
/// Speicherfehler (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>) gehen an den Aufrufer;
/// der Stand im Speicher bleibt dann unverändert.
/// </summary>
public sealed class AchievementTracker(JsonFileStore store, TimeProvider clock)
{
    public AchievementState State { get; private set; } = AchievementState.StartingAt(clock.GetLocalNow());

    /// <summary>Wie bei den Einstellungen: Pfad der Sicherung einer unlesbaren Datei, leer = Sichern fehlgeschlagen.</summary>
    public string? BrokenFileBackup { get; private set; }

    public event EventHandler? Changed;

    public void Load()
    {
        string? json;
        try
        {
            json = store.Read(AchievementState.FileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            json = null;
            BrokenFileBackup = store.Backup(AchievementState.FileName) ?? string.Empty;
        }

        if (json is not null)
        {
            try
            {
                State = AchievementState.Parse(json);
                return;
            }
            catch (JsonException)
            {
                BrokenFileBackup = store.Backup(AchievementState.FileName) ?? string.Empty;
            }
        }

        // Neu anfangen und den Startzeitpunkt gleich festhalten, sonst wandert er bei jedem Start mit.
        State = AchievementState.StartingAt(clock.GetLocalNow());
        try
        {
            store.Write(AchievementState.FileName, State.ToJson());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Dann beim nächsten Speichern; bis dahin gilt der Startzeitpunkt im Speicher.
        }
    }

    public IReadOnlyList<AchievementProgress> Progress(IReadOnlyList<DrinkEntry> entries, AppSettings settings) =>
        AchievementEvaluator.Evaluate(entries, State, settings, clock.GetUtcNow(), clock.LocalTimeZone);

    /// <summary>
    /// Schaltet Erfolge frei, deren Ziel jetzt erreicht ist, und liefert sie (leer, wenn keiner neu ist).
    /// </summary>
    public IReadOnlyList<Achievement> Check(IReadOnlyList<DrinkEntry> entries, AppSettings settings)
    {
        var reached = AchievementEvaluator.NewlyReached(Progress(entries, settings));
        if (reached.Count == 0) return reached;

        var now = clock.GetLocalNow();
        Save(State with { Unlocked = State.Unlocked.AddRange(reached.Select(a => KeyValuePair.Create(a.Id, now))) });
        return reached;
    }

    /// <summary>"Getrunken" wurde direkt in einer Erinnerung angeklickt (zählt für "Aufs Wort").</summary>
    public void RecordOnCue() => Save(State with { OnCueCount = State.OnCueCount + 1 });

    private void Save(AchievementState next)
    {
        store.Write(AchievementState.FileName, next.ToJson());
        State = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
