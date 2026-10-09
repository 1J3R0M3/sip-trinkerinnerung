using Microsoft.Extensions.Time.Testing;
using Trinkwasser.Core.Settings;
using Trinkwasser.Core.Storage;

namespace Trinkwasser.Core.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "trinkwasser-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 9, 14, 30, 0, TimeSpan.Zero));

    public SettingsServiceTests() => _clock.SetLocalTimeZone(TimeZoneInfo.Utc);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private JsonFileStore Store => new(_folder, _clock);
    private SettingsService Create() => new(Store, "de-DE");
    private string SettingsFile => Path.Combine(_folder, SettingsService.FileName);

    [Fact]
    public void MissingFileGivesDefaultsWithoutWriting()
    {
        var service = Create();
        Assert.Equal(AppSettings.Defaults("de"), service.Load());
        Assert.Null(service.BrokenFileBackup);
        Assert.False(File.Exists(SettingsFile));
    }

    [Fact]
    public void UpdateSavesAndSurvivesReload()
    {
        var service = Create();
        service.Load();
        service.Update(s => s with { SetupDone = true, DailyGoalMl = 2500, Language = "en" });

        var reloaded = Create().Load();
        Assert.True(reloaded.SetupDone);
        Assert.Equal(2500, reloaded.DailyGoalMl);
        Assert.Equal("en", reloaded.Language);
        Assert.False(File.Exists(SettingsFile + ".tmp"));
    }

    [Fact]
    public void UpdateSanitizesAndRaisesChangedOnlyOnRealChanges()
    {
        var service = Create();
        service.Load();
        var events = new List<SettingsChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        var result = service.Update(s => s with { GlassMl = 99999 });
        service.Update(s => s with { GlassMl = 2000 });
        service.Update(s => s with { WorkHours = [.. s.WorkHours] }); // gleicher Inhalt, neue Liste

        Assert.Equal(2000, result.GlassMl);
        var e = Assert.Single(events);
        Assert.Equal(250, e.Previous.GlassMl);
        Assert.Equal(2000, e.Current.GlassMl);
    }

    [Fact]
    public void BrokenFileIsBackedUpAndNotOverwritten()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, "{ das ist kein JSON");

        var service = Create();
        Assert.Equal(AppSettings.Defaults("de"), service.Load());

        var backup = Path.Combine(_folder, "settings.broken-20261009-143000.json");
        Assert.Equal(backup, service.BrokenFileBackup);
        Assert.Equal("{ das ist kein JSON", File.ReadAllText(backup));
        Assert.False(File.Exists(SettingsFile));

        service.Update(s => s with { SetupDone = true });
        Assert.Equal("{ das ist kein JSON", File.ReadAllText(backup));
    }

    [Fact]
    public void SecondBackupInTheSameSecondGetsANewName()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, "kaputt 1");
        Create().Load();
        File.WriteAllText(SettingsFile, "kaputt 2");
        var service = Create();
        service.Load();

        Assert.EndsWith("settings.broken-20261009-143000-2.json", service.BrokenFileBackup);
        Assert.Equal("kaputt 2", File.ReadAllText(service.BrokenFileBackup!));
    }

    [Fact]
    public void FileWithSingleInvalidValuesIsNotTreatedAsBroken()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, """{"setupDone":true,"glassMl":"abc"}""");
        var service = Create();
        var s = service.Load();
        Assert.True(s.SetupDone);
        Assert.Equal(250, s.GlassMl);
        Assert.Null(service.BrokenFileBackup);
    }

    [Fact]
    public void WriteReplacesExistingFileAndLeavesNoTempFile()
    {
        var store = Store;
        store.Write("a.json", "eins");
        store.Write("a.json", "zwei");
        Assert.Equal("zwei", store.Read("a.json"));
        Assert.Equal(["a.json"], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    [Fact]
    public void FailedSaveKeepsCurrentSettings()
    {
        var service = Create();
        service.Load();
        // Ein Ordner mit dem Namen der Temp-Datei lässt das Schreiben scheitern.
        Directory.CreateDirectory(SettingsFile + ".tmp");

        var error = Record.Exception(() => service.Update(s => s with { GlassMl = 500 }));
        Assert.True(error is IOException or UnauthorizedAccessException, error?.GetType().Name);
        Assert.Equal(250, service.Current.GlassMl);
    }
}
