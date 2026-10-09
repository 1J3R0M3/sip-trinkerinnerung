using System.Globalization;
using System.Text;

namespace Trinkwasser.Core.Storage;

/// <summary>
/// Kleine Textdateien in einem Ordner (in der App: <c>ApplicationData.Current.LocalFolder</c>).
/// Geschrieben wird atomar: erst in eine Temp-Datei, dann ersetzen. So bleibt bei einem
/// Absturz oder Stromausfall mitten im Schreiben die alte Datei intakt.
/// </summary>
public sealed class JsonFileStore(string folder, TimeProvider clock)
{
    public string Folder { get; } = folder;

    public string PathOf(string fileName) => Path.Combine(Folder, fileName);

    /// <summary>Inhalt der Datei oder null, wenn es sie (noch) nicht gibt.</summary>
    public string? Read(string fileName)
    {
        try
        {
            return File.ReadAllText(PathOf(fileName), Encoding.UTF8);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void Write(string fileName, string content)
    {
        Directory.CreateDirectory(Folder);
        var path = PathOf(fileName);
        var tmp = path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            stream.Write(bytes);
            // Erst auf die Platte, dann umbenennen – sonst kann nach einem Absturz eine leere Datei übrig bleiben.
            stream.Flush(flushToDisk: true);
        }
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Sichert eine unlesbare Datei unter neuem Namen (z. B. <c>settings.broken-20261009-143000.json</c>),
    /// damit sie beim nächsten Speichern nicht stillschweigend überschrieben wird.
    /// Liefert den Pfad der Sicherung oder null, wenn das Sichern nicht möglich war.
    /// </summary>
    public string? Backup(string fileName)
    {
        var path = PathOf(fileName);
        var stamp = clock.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var stem = $"{Path.GetFileNameWithoutExtension(fileName)}.broken-{stamp}";
        var ext = Path.GetExtension(fileName);
        var target = PathOf(stem + ext);
        for (var n = 2; File.Exists(target); n++) target = PathOf($"{stem}-{n}{ext}");

        try
        {
            File.Move(path, target);
            return target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Datei gesperrt o. Ä.: wenigstens eine Kopie anlegen.
            try
            {
                File.Copy(path, target);
                return target;
            }
            catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
