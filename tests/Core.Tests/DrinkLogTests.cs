using System.Text.Json;
using Trinkwasser.Core.Drinks;
using Trinkwasser.Core.Storage;
using static Trinkwasser.Core.Tests.TestTime;

namespace Trinkwasser.Core.Tests;

public sealed class DrinkLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "trinkwasser-tests", Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Clock(At(2026, 10, 5, "09:15"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private DrinkLog Create()
    {
        var log = new DrinkLog(new JsonFileStore(_folder, _clock), _clock);
        log.Load();
        return log;
    }

    [Fact]
    public void AddSavesWithLocalTimeAndSurvivesReload()
    {
        var log = Create();
        log.Add(250);
        _clock.Advance(TimeSpan.FromMinutes(30));
        log.Add(300);

        var reloaded = Create();
        Assert.Equal([250, 300], reloaded.Entries.Select(e => e.AmountMl));
        Assert.Equal(At(2026, 10, 5, "09:15"), reloaded.Entries[0].Time);
        Assert.Equal(TimeSpan.FromHours(2), reloaded.Entries[0].Time.Offset);
    }

    [Fact]
    public void RemoveLastDeletesOnlyTheNewestEntry()
    {
        var log = Create();
        log.Add(250);
        _clock.Advance(TimeSpan.FromMinutes(5));
        log.Add(500);

        Assert.Equal(500, log.RemoveLast()?.AmountMl);
        Assert.Equal([250], Create().Entries.Select(e => e.AmountMl));
        Assert.Equal(250, log.RemoveLast()?.AmountMl);
        Assert.Null(log.RemoveLast());
    }

    [Fact]
    public void ChangedIsRaisedForAddAndUndo()
    {
        var log = Create();
        var count = 0;
        log.Changed += (_, _) => count++;
        log.Add(250);
        log.RemoveLast();
        log.RemoveLast();
        Assert.Equal(2, count);
    }

    [Fact]
    public void InvalidEntriesAreSkipped()
    {
        var entries = DrinkLog.Parse("""
            {"version":1,"entries":[
              {"time":"2026-10-05T10:00:00+02:00","ml":250},
              {"time":"kaputt","ml":250},
              {"time":"2026-10-05T11:00:00+02:00","ml":0},
              {"time":"2026-10-05T12:00:00+02:00","ml":"250"},
              null,
              {"time":"2026-10-05T09:00:00+02:00","ml":300}
            ]}
            """);
        Assert.Equal([300, 250], entries.Select(e => e.AmountMl));
    }

    [Theory]
    [InlineData("kein JSON")]
    [InlineData("[]")]
    [InlineData("{\"entries\":5}")]
    public void BrokenFileThrows(string json) => Assert.ThrowsAny<JsonException>(() => DrinkLog.Parse(json));

    [Fact]
    public void BrokenFileIsBackedUpAndLogStartsEmpty()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, DrinkLog.FileName), "{ kaputt");
        var log = Create();

        Assert.Empty(log.Entries);
        Assert.EndsWith("drinks.broken-20261005-091500.json", log.BrokenFileBackup);
        log.Add(250);
        Assert.Equal("{ kaputt", File.ReadAllText(log.BrokenFileBackup!));
    }

    [Fact]
    public void FailedSaveKeepsEntries()
    {
        var log = Create();
        log.Add(250);
        Directory.CreateDirectory(Path.Combine(_folder, DrinkLog.FileName + ".tmp"));

        Assert.NotNull(Record.Exception(() => log.Add(300)));
        Assert.NotNull(Record.Exception(() => log.RemoveLast()));
        Assert.Equal([250], log.Entries.Select(e => e.AmountMl));
    }
}
