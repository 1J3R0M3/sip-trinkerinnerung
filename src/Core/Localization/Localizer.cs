using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Trinkwasser.Core.Localization;

/// <summary>
/// Liefert die Oberflächentexte aus den eingebetteten Sprachdateien (Locales/*.json).
/// x:Uid aus .resw aktualisiert sich nicht zur Laufzeit; deshalb bindet die Oberfläche
/// an die Eigenschaften dieser Klasse. Beim Sprachwechsel wird PropertyChanged mit
/// leerem Namen ausgelöst, damit sich alle Bindungen neu auswerten.
/// </summary>
public sealed partial class Localizer : ObservableObject
{
    public const string German = "de";
    public const string English = "en";
    public static IReadOnlyList<string> Languages { get; } = [German, English];

    private static readonly Dictionary<string, Dictionary<string, string>> Tables =
        Languages.ToDictionary(lang => lang, LoadTable);

    // Der App-Name wird nicht in den JSON-Dateien gepflegt, sondern zentral in den
    // Paketressourcen (Strings/<Sprache>/Resources.resw) und hier als {app} eingesetzt.
    private readonly Func<string, string> _appName;

    public Localizer(string language, Func<string, string> appName)
    {
        _appName = appName;
        Language = Normalize(language);
    }

    public string Language { get; private set; }

    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language == German ? "de-DE" : "en-US");

    public event EventHandler? LanguageChanged;

    /// <summary>"de-AT" → "de", alles Unbekannte → "en".</summary>
    public static string Normalize(string? language) =>
        language is not null && language.StartsWith(German, StringComparison.OrdinalIgnoreCase) ? German : English;

    public void SetLanguage(string language)
    {
        var lang = Normalize(language);
        if (lang == Language) return;
        Language = lang;
        OnPropertyChanged(string.Empty);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static IReadOnlyCollection<string> Keys(string language) => Tables[Normalize(language)].Keys;

    public string Get(string key)
    {
        if (!Tables[Language].TryGetValue(key, out var text) && !Tables[English].TryGetValue(key, out text))
            return key;
        return text.Replace("{app}", _appName(Language), StringComparison.Ordinal);
    }

    /// <summary>Ersetzt Platzhalter wie {amount}; Zahlen werden passend zur Sprache formatiert.</summary>
    public string Format(string key, params (string Name, object Value)[] args)
    {
        var text = Get(key);
        foreach (var (name, value) in args)
        {
            var formatted = value is IFormattable f ? f.ToString(value is int or long ? "N0" : null, Culture) : value.ToString();
            text = text.Replace("{" + name + "}", formatted, StringComparison.Ordinal);
        }
        return text;
    }

    private static Dictionary<string, string> LoadTable(string language)
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"Locales.{language}.json")
            ?? throw new InvalidOperationException($"Sprachdatei {language}.json fehlt.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Sprachdatei {language}.json ist leer.");
    }

    // Typisierte Eigenschaften für {x:Bind}. Texte mit Platzhaltern sind Methoden.
    public string AppName => _appName(Language);

    public string TrayTooltip => Get("tray.tooltip");
    public string TrayOpen => Get("tray.open");
    public string TraySettings => Get("tray.settings");
    public string TrayTestNotification => Get("tray.testNotification");
    public string TrayQuit => Get("tray.quit");

    public string ToastTitle => Get("toast.title");
    public string ToastBody(int amountMl) => Format("toast.body", ("amount", amountMl));
    public string ToastDrank => Get("toast.drank");
    public string ToastSnooze(int minutes) => Format("toast.snooze", ("minutes", minutes));
    public string ToastGoalTitle => Get("toast.goalTitle");
    public string ToastGoalBody(int amountMl) => Format("toast.goalBody", ("amount", amountMl));

    public string SettingsCategories => Get("settings.categories");
    public string SettingsInfo => Get("settings.info");
    public string InfoVersion(string version) => Format("info.version", ("version", version));
    public string InfoDevBuild => Get("info.devBuild");
    public string InfoSourceCode => Get("info.sourceCode");
    public string InfoPrivacy => Get("info.privacy");
    public string InfoLicenses => Get("info.licenses");
    public string InfoLicensesText => Get("info.licensesText");
    public string InfoLibraries => Get("info.libraries");
    public string InfoSounds => Get("info.sounds");
    public string InfoCredit(string license, string author) => Format("info.credit", ("license", LicenseName(license)), ("author", author));
    public string InfoOpenLink(string name) => Format("info.openLink", ("name", name));
    public string LicenseName(string id) => Get("license." + id);

    public string HomeAutostartOffTitle => Get("home.autostartOffTitle");
    public string HomeAutostartOffText => Get("home.autostartOffText");
    public string HomeAutostartEnable => Get("home.autostartEnable");

    public string SettingsSounds => Get("settings.sounds");
    public string SettingsSoundsText => Get("settings.soundsText");
    public string SoundPlay => Get("sound.play");
    public string SoundName(string id) => Get("sound." + id);

    public string SoundOccasionName(Sounds.SoundOccasion occasion) => occasion switch
    {
        Sounds.SoundOccasion.Snooze => Format("sound.occasion.snooze", ("minutes", Notifications.ToastArguments.SnoozeMinutes)),
        Sounds.SoundOccasion.GoalReached => Get("sound.occasion.goal"),
        _ => Get("sound.occasion.reminder"),
    };

    public string SoundPlayFor(Sounds.SoundOccasion occasion) => Format("sound.playFor", ("occasion", SoundOccasionName(occasion)));

    public string HomeTestButton => Get("home.testButton");
    public string HomeHint => Get("home.hint");
    public string HomeTodayHeading => Get("home.today.heading");
    public string HomeWeekHeading => Get("home.week.heading");
    public string HomeRemindersHeading => Get("home.reminders.heading");
    public string HomeGoalReached => Get("home.goalReached");
    public string HomeUndo => Get("home.undo");
    public string HomeNoReminder => Get("home.noReminder");
    public string HomeNoWorkToday => Get("home.noWorkToday");
    public string HomeProgress(int valueMl, int goalMl) => Format("home.progress", ("value", valueMl), ("goal", goalMl));
    public string HomePercent(int percent) => Format("home.percent", ("percent", percent));
    public string HomeAddGlass(int amountMl) => Format("home.addGlass", ("amount", amountMl));
    public string HomeUndoHelp(int amountMl, TimeOnly time) => Format("home.undoHelp", ("amount", amountMl), ("time", Time(time)));
    public string HomeAdded(int valueMl, int goalMl) => Format("home.added", ("value", valueMl), ("goal", goalMl));
    public string HomeRemoved(int valueMl, int goalMl) => Format("home.removed", ("value", valueMl), ("goal", goalMl));
    public string HomeInterval(int minutes) => Format("home.interval", ("interval", Interval(minutes)));
    public string HomeWorkToday(TimeOnly from, TimeOnly to) => Format("home.today", ("from", Time(from)), ("to", Time(to)));

    /// <summary>"Nächste Erinnerung: 14:00", "…: morgen, 09:00" oder "…: Montag, 09:00".</summary>
    public string HomeNextReminder(DateTime next, DateTime now)
    {
        var time = Time(TimeOnly.FromDateTime(next));
        var days = DateOnly.FromDateTime(next).DayNumber - DateOnly.FromDateTime(now).DayNumber;
        var text = days switch
        {
            0 => time,
            1 => $"{Get("home.tomorrow")}, {time}",
            _ => $"{WeekdayName(Settings.WorkCalendar.DayIndex(next.DayOfWeek))}, {time}",
        };
        return Format("home.nextReminder", ("time", text));
    }

    public string NavHome => Get("nav.home");
    public string NavStatistics => Get("nav.statistics");

    public string StatsTitle => Get("stats.title");
    public string StatsPrevWeek => Get("stats.prevWeek");
    public string StatsNextWeek => Get("stats.nextWeek");
    public string StatsThisWeek => Get("stats.thisWeek");
    public string StatsChart => Get("stats.chart");
    public string StatsLegend => Get("stats.legend");
    public string StatsTotal => Get("stats.total");
    public string StatsAverage => Get("stats.average");
    public string StatsBestHour => Get("stats.bestHour");
    public string StatsStreak => Get("stats.streak");
    public string StatsStreakHint => Get("stats.streakHint");
    public string StatsGoalRate => Get("stats.goalRate");
    public string StatsNoData => Get("stats.noData");
    public string StatsWeekRange(DateOnly from, DateOnly to) =>
        Format("stats.weekRange", ("from", from.ToString("d", Culture)), ("to", to.ToString("d", Culture)));
    public string StatsBar(int dayIndex, int valueMl, int goalMl, bool reached) =>
        Format(reached ? "stats.barReached" : "stats.bar", ("day", WeekdayName(dayIndex)), ("value", valueMl), ("goal", goalMl));
    public string StatsBestHourValue(int hour) =>
        Format("stats.bestHourValue", ("from", Time(new TimeOnly(hour, 0))), ("to", Time(new TimeOnly((hour + 1) % 24, 0))));
    public string StatsStreakValue(int days) => days == 1 ? Get("stats.streakDay") : Format("stats.streakDays", ("count", days));
    public string StatsGoalRateValue(int percent) => Format("stats.goalRateValue", ("percent", percent));
    public string StatsDaysReached(int reached, int total) => Format("stats.daysReached", ("reached", reached), ("total", total));
    public string Ml(int valueMl) => $"{valueMl.ToString("N0", Culture)} {Get("unit.ml")}";

    public string MoodText(Drinks.Mood mood) => mood switch
    {
        Drinks.Mood.GoalReached => Get("mood.goalReached"),
        Drinks.Mood.OnTrack => Get("mood.onTrack"),
        Drinks.Mood.Okay => Get("mood.okay"),
        Drinks.Mood.Behind => Get("mood.behind"),
        _ => Get("mood.thirsty"),
    };

    public string CountdownHeading => Get("countdown.heading");
    public string CountdownNone => Get("countdown.none");
    public string CountdownA11y(TimeSpan remaining) =>
        Format("countdown.a11y", ("minutes", (int)Math.Ceiling(remaining.TotalMinutes)));

    public string FactsHeading => Get("facts.heading");
    public string FactsNext => Get("facts.next");

    /// <summary>Anzahl der Wasser-Fakten (Schlüssel fact.1 … fact.N).</summary>
    public static int FactCount { get; } = Tables[English].Keys.Count(k => k.StartsWith("fact.", StringComparison.Ordinal));

    /// <summary>Fakt 0 … <see cref="FactCount"/> − 1.</summary>
    public string Fact(int index) => Get($"fact.{index % FactCount + 1}");

    public string FieldTheme => Get("field.theme");
    public string ThemeName(Settings.AppTheme theme) => Get("theme." + Settings.SettingsSanitizer.FormatTheme(theme));

    public string TrayAddGlass => Get("tray.addGlass");
    public string ErrorSaveDrink(string reason) => Format("error.saveDrink", ("reason", reason));
    public string DrinksBrokenFile(string? backupPath) => string.IsNullOrEmpty(backupPath)
        ? Get("error.drinksBrokenNoBackup")
        : Format("error.drinksBroken", ("file", Path.GetFileName(backupPath)));

    public string CommonBack => Get("common.back");
    public string CommonNext => Get("common.next");
    public string CommonFinish => Get("common.finish");
    public string CommonOn => Get("common.on");
    public string CommonOff => Get("common.off");

    public string SetupWelcome => Get("setup.welcome");
    public string SetupStep(int n, int total) => Format("setup.step", ("n", n), ("total", total));
    public string SetupLanguageHeading => Get("setup.language.heading");
    public string SetupLanguageText => Get("setup.language.text");
    public string SetupGoalsHeading => Get("setup.goals.heading");
    public string SetupGoalsText => Get("setup.goals.text");
    public string SetupRemindersHeading => Get("setup.reminders.heading");
    public string SetupRemindersText => Get("setup.reminders.text");

    public string FieldLanguage => Get("field.language");
    public string FieldDailyGoal => WithUnit("field.dailyGoal");
    public string FieldWeeklyGoal => WithUnit("field.weeklyGoal");
    public string FieldGlass => WithUnit("field.glass");
    public string FieldInterval => Get("field.interval");
    public string FieldWorkDays => Get("field.workDays");
    public string FieldWorkTime => Get("field.workTime");
    public string FieldFrom => Get("field.from");
    public string FieldTo => Get("field.to");
    public string FieldAutostart => Get("field.autostart");
    public string FieldAutostartHint => Get("field.autostartHint");
    public string FieldOpenStartupSettings => Get("field.openStartupSettings");
    public string FieldDayStart(int dayIndex) => Format("field.dayStart", ("day", WeekdayName(dayIndex)));
    public string FieldDayEnd(int dayIndex) => Format("field.dayEnd", ("day", WeekdayName(dayIndex)));

    public string WeeklyGoalSuggestion(int valueMl, int days) =>
        Format("field.weeklyGoalSuggest", ("value", valueMl), ("days", days));

    public string ErrorTimeRange => Get("error.timeRange");
    public string ErrorNoWorkDay => Get("error.noWorkDay");
    public string ErrorRange(int min, int max) => Format("error.range", ("min", min), ("max", max));
    public string ErrorSave(string reason) => Format("error.save", ("reason", reason));

    public string SettingsTitle => Get("settings.title");
    public string SettingsGeneral => Get("settings.general");
    public string SettingsGoals => Get("settings.goals");
    public string SettingsReminders => Get("settings.reminders");
    public string SettingsWorkHours => Get("settings.workHours");
    public string SettingsWorkHoursText => Get("settings.workHoursText");
    public string SettingsCopyFirst => Get("settings.copyFirst");
    public string SettingsAutoSave => Get("settings.autoSave");
    public string SettingsBrokenFile(string? backupPath) => string.IsNullOrEmpty(backupPath)
        ? Get("settings.brokenFileNoBackup")
        : Format("settings.brokenFile", ("file", Path.GetFileName(backupPath)));

    public string AutostartStatus(AutostartState state) => state switch
    {
        AutostartState.DisabledByUser => Get("autostart.disabledByUser"),
        AutostartState.DisabledByPolicy => Get("autostart.disabledByPolicy"),
        AutostartState.EnabledByPolicy => Get("autostart.enabledByPolicy"),
        AutostartState.Unavailable => Get("autostart.unavailable"),
        _ => string.Empty,
    };

    /// <summary>Name der Sprache in ihr selbst ("Deutsch", "English").</summary>
    public string LanguageName(string language) => Get("language." + Normalize(language));

    /// <summary>Wochentag in der App-Sprache; 0 = Montag … 6 = Sonntag.</summary>
    public string WeekdayName(int index, bool abbreviated = false)
    {
        var day = (DayOfWeek)((index + 1) % 7);
        var format = Culture.DateTimeFormat;
        return abbreviated ? format.GetAbbreviatedDayName(day) : format.GetDayName(day);
    }

    /// <summary>15 → "15 Min.", 60 → "1 Std.", 90 → "1,5 Std." (wie im Prototyp).</summary>
    public string Interval(int minutes) => minutes switch
    {
        _ when minutes % 60 == 0 => Format("unit.hours", ("hours", minutes / 60)),
        > 60 => Format("unit.hours", ("hours", (minutes / 60.0).ToString("N1", Culture))),
        _ => Format("unit.minutes", ("minutes", minutes)),
    };

    /// <summary>2000 → "= 2,0 l", 250 → "= 0,25 l".</summary>
    public string Liters(double ml) => double.IsFinite(ml) && ml > 0
        ? Format("unit.liters", ("value", (ml / 1000).ToString("0.0#", Culture)))
        : string.Empty;

    /// <summary>Uhrzeit kurz in der App-Sprache (de: 17:00, en: 5:00 PM).</summary>
    public string Time(TimeOnly time) => time.ToString("t", Culture);

    private string WithUnit(string key) => $"{Get(key)} ({Get("unit.ml")})";
}

/// <summary>Zustand des Autostarts (StartupTask) aus Sicht der Oberfläche.</summary>
public enum AutostartState
{
    Disabled,
    Enabled,
    /// <summary>Vom Nutzer in den Windows-Einstellungen ausgeschaltet; nur dort wieder einschaltbar.</summary>
    DisabledByUser,
    DisabledByPolicy,
    EnabledByPolicy,
    Unavailable,
}
