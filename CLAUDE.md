# Sip – Windows-11-App (WinUI 3)

Trinkwasser-Erinnerungs-App „Sip“ für Windows 11 in C# mit WinUI 3 (Windows App SDK), verteilt über den
Microsoft Store als MSIX. Kostenlos, ohne Telemetrie, Daten nur lokal. Ziel: eine App, die viele
Menschen im Alltag nutzen – also sorgfältig, barrierearm und robust bauen.

Nachfolger des Electron-Prototyps https://github.com/1J3R0M3/trinkwasser-app (lokal unter
`E:\Projekte\trinkwasser-app`, nur als Referenz – Electron-Code nicht übernehmen).

## Stand

| Schritt | Inhalt | Status |
|---|---|---|
| 1 | Projektgerüst, Single-Instance, Tray, Test-Toast mit beiden Buttons inkl. Aktivierung | ✅ fertig, vom Nutzer getestet |
| 2 | Einrichtungsassistent, Einstellungen, Sprachwechsel, Autostart | ✅ gebaut, Test durch den Nutzer läuft |
| 3 | Erinnerungslogik, Fortschritt, Statistiken inkl. Tests | ✅ gebaut, wartet auf Test durch den Nutzer |
| 4 | Store-Paket und README (optional GitHub-Actions-Workflow) | ✅ gebaut, Workflow noch nie auf GitHub gelaufen |
| 5 | Design: Smiley, Countdown-Kreis, Wasser-Fakten, Hell/Dunkel/System | ✅ gebaut, wartet auf Feedback |
| 6 | Benachrichtigungstöne, Einstellungen in Bereichen, Info mit Version und Lizenzen (Version 1.2.0) | ✅ gebaut und getestet, in main |
| 7 | Erfolge mit Badges, Erfolgsseite, Benachrichtigung und Ton | ✅ gebaut und getestet, Branch `feature/erfolge`, wartet auf Feedback |

Store: 1.0.0 veröffentlicht, 1.0.1 (Erinnerungs-Fix) eingereicht, 1.2.0 (Töne, neue Einstellungen) gebaut.

Versionen (vom Nutzer festgelegt): dreistellig **MAJOR.MINOR.PATCH** – PATCH (rechts) für Bugfixes,
MINOR (Mitte) für Features, MAJOR (links) für große Änderungen. Im Manifest vierstellig mit `.0`
(Store-Pflicht), angezeigt dreistellig. Bauen mit `.\tools\Build-StorePackage.ps1 -Version x.y.z`.

Entscheidungen aus Schritt 7 (Erfolge, Branch `feature/erfolge`):
- 17 Erfolge in `Core/Achievements/AchievementCatalog` (Ids wie die Badge-Dateien): Erste Schritte
  (erstes Glas, erstes Tagesziel, erstes Wochenziel), Menge (10/50/100/250/500/1000 l), Durchhalten
  (Streak 3/7/14/30/100, gezählt wie in der Statistik), Gewohnheit (Perfekte Woche = an allen
  Arbeitstagen einer Woche Mo–So das Tagesziel; Aufs Wort = 10× „Getrunken“ direkt in der Erinnerung;
  Früher Vogel = an 5 Arbeitstagen erstes Glas spätestens 1 h nach Arbeitsbeginn). Kein Erfolg belohnt
  deutlich mehr als das Tagesziel.
- Gezählt wird „ab jetzt“ (vom Nutzer so gewählt): `achievements.json` hält `since` (erster Start mit
  Erfolgen), `onCue` und `unlocked` (Id → Zeitpunkt). Gläser vor `since` zählen nirgends mit. Kaputte
  Datei → gesichert, Hinweis im Fenster, Zählung beginnt neu.
- Freigeschaltet bleibt freigeschaltet (auch nach „Rückgängig“). Geprüft wird nur nach einem Glas
  (`DrinkActions.AddGlass`), nicht bei geänderten Einstellungen; gewertet mit den aktuellen Zielen.
- Benachrichtigung „Erfolg freigeschaltet“ mit Badge (`SetAppLogoOverride`), mehrere Erfolge in einer;
  Klick öffnet die Erfolgsseite (`ToastAction.Achievements`). Eigener Ton als 4. `SoundOccasion`
  (JSON `sounds.achievement`, Standard `crystal`). Kommt gleichzeitig „Tagesziel erreicht“, folgt der
  Erfolg 6 s später mit eigenem Ton (`DrinkActions.SecondNoticeDelay`; gleichzeitig klangen zwei Töne
  schief, und Windows legte die zweite Meldung nur ins Info-Center). Hält jemand die Maus über die
  erste Meldung, kann die zweite trotzdem direkt im Info-Center landen.
- „Getrunken“ in der Test-Benachrichtigung (`ToastAction.TestDrank`) zählt das Glas, aber nicht für
  „Aufs Wort“ – sonst ließe sich der Erfolg per Test-Button erschleichen.
- Beim Testen: Steht der Mauszeiger über einem Popup, blendet Windows es nicht aus.
- Badges: `tools/gen-badges.cs` zeichnet SVG (Form = Kategorie, Farbe = Stufe, gesperrt grau mit
  Schloss) und rendert per Svg.Skia (nur Werkzeug, nicht in der App) 256-px-PNGs nach
  `src/App/Assets/Achievements/<id>.png` und `<id>.locked.png`. Eigene Arbeit, daher nicht in Credits.
  Ein Test prüft, dass jeder Erfolg beide Dateien hat.
- Erfolgsseite: eigener Navigationseintrag (Stern `E734`), Karten in `Controls/CardGridPanel`
  (gleich breite Spalten, Zeilenhöhe = höchste Karte; UniformGridLayout schnitt Texte ab).
  Farben nur per `ThemeResource` in XAML – `Application.Current.Resources` im Code liefert die Pinsel
  des Windows-Farbschemas, nicht des in Sip gewählten.

Entscheidungen aus Schritt 6 (Töne):
- Drei Anlässe (`SoundOccasion`): Erinnerung, erneute Erinnerung nach Snooze (`ReminderKind.Snooze`
  aus `ReminderEngine.Due`), „Tagesziel erreicht“ (neue Benachrichtigung ohne Buttons).
- Je Anlass ein Ton aus `SoundCatalog`: 13 Sip-Töne, `windows` (Standard, kein audio-Element) oder
  `silent` (`MuteAudio`). JSON `sounds: { reminder, snooze, goal }`; fehlt es (Dateien vor 1.2) oder
  ist ein Ton unbekannt → Standard (drop / quick-pour / glass-ping).
- Ton hängt an der Benachrichtigung (`SetAudioUri("ms-appx:///Assets/Sounds/<id>.wav")`); Probehören
  in den Einstellungen per `MediaPlayer` (`SoundPreview`), „Windows-Standard“ liest den Ton aus
  `HKCU\AppEvents\Schemes\Apps\.Default\Notification.Default\.Current`.
- „Tagesziel erreicht“ (`DailyGoalNotice`): nur wenn genau dieses Glas das Ziel überschreitet, höchstens
  einmal am Tag. Der Tag steht seit 1.3 in `achievements.json` (`goalNoticeOn`), gilt also auch nach einem Neustart.
- Töne: CC0-Hörproben von Freesound (`-hq.mp3`) in `artifacts/sounds-raw`, aufbereitet mit
  `tools/process-sounds.cs` (NAudio; Mono, Stille weg, max. 3 s, Lautheit angeglichen, 16-Bit-WAV).
  Herkunft in `src/App/Assets/Sounds/LICENSES.md`.
- Einstellungen in sechs Bereichen links (ListView, Panels per Sichtbarkeit): Allgemein, Ziele,
  Erinnerungen, Arbeitszeiten, Töne, Info. Info zeigt Version (`AppInfo`, Paketversion; „Entwicklerversion“
  bei `.Dev`), Links zu GitHub/Datenschutz und Lizenzen aus `Core/About/Credits` (nur was im Paket
  landet; neue Pakete/Töne dort ergänzen, Test prüft Töne). Windows App SDK: Microsoft-Lizenz, nicht MIT.
- Fenster 1100 × 720, Mindestbreite 1040; Hauptnavigation klappt erst ab 1400 px auf
  (`ExpandedModeThresholdWidth`), sonst passen die Arbeitszeiten neben die Bereichsliste nicht.
- Startseite: Hinweis „Autostart ist aus“ (eigenes `AutostartViewModel`, `ShowOffHint`) mit „Autostart
  einschalten“ bzw. Link zu den Windows-Einstellungen, wenn dort ausgeschaltet.

Entscheidungen aus Schritt 5:
- Smiley (`HydrationMood`): vergleicht mit dem Soll für die Uhrzeit (linear über die Arbeitszeit,
  an freien Tagen 8–20 Uhr), nicht nur mit dem Tagesziel. Gezeichnet als Vektor (`Controls/MoodFace`),
  Farben nur aus Theme-Ressourcen, Bedeutung immer auch als Text. Hüpfen nur bei
  eingeschalteten Windows-Animationseffekten.
- Countdown-Kreis: ProgressRing, Kreis = ein ganzes Intervall (Snooze: 10 min), `ReminderEngine.Snapshot`.
  Liegt die nächste Erinnerung nicht mehr heute, bleibt er leer.
- Fakten: `fact.1` … `fact.N` in den Sprachdateien (nur gut belegte Aussagen), Start zufällig.
- Farbschema `AppSettings.Theme` (JSON `theme`: system/light/dark), wirkt sofort auf Fensterinhalt
  und Titelleiste; Tray-Menü und Toasts folgen immer Windows.

**Nach jedem Schritt anhalten und auf Feedback des Nutzers warten.** Bei Unklarheiten nachfragen.

Entscheidungen aus Schritt 3:
- Erinnerungen: `ReminderScheduler` (reine Berechnung) + `ReminderEngine` (Takt alle 20 s auf dem
  Thread-Pool). Ein Abstand > 2 min zwischen zwei Takten gilt als Schlaf/verstellte Uhr → neu planen,
  nicht nachholen (verpasster Termin → jetzt + Intervall). Termin genau zum Arbeitsende entfällt.
  Snooze außerhalb der Arbeitszeit entfällt. Letzte Erinnerung und Snooze nur im Speicher.
- Ist der eigentliche Termin schon vorbei (App-Start mitten am Tag ohne Glas, geänderte Einstellungen,
  Aufwachen), zählt das Intervall ab einem festen Zeitpunkt `ReminderState.Since`, nicht ab „jetzt“.
  Sonst wandert der Termin bei jedem Takt mit und kommt nie (Fehler in 1.0.0). `Reschedule()` setzt
  `Since`, der Takt (`Poll`) plant, ohne es zu ändern.
- Protokoll `drinks.json` mit lokaler Zeit inkl. Offset; Tage werden in lokaler Zeit gezählt.
- Woche Mo–So. Durchschnitt/Zielerreichung zählen Tage bis heute, davon Arbeitstage und Tage mit
  Einträgen. Beste Tageszeit = Stunde mit den meisten Gläsern der angezeigten Woche (Gleichstand → früher).
  Streak ab heute rückwärts; heute bricht nicht, freie Tage brechen nicht, zählen aber, wenn erreicht.
  Gewertet wird mit den aktuellen Zielen/Arbeitstagen (keine Historie der Einstellungen).
- „Rückgängig“ auf der Startseite nur für Einträge von heute. Tray hat „+1 Glas“.

Entscheidungen aus Schritt 4 (Store-Paket):
- Gebaut wird mit `dotnet build` (kein VS-MSBuild nötig), Bundle x64 + arm64, unsigniert.
- Release ist self-contained (.NET-Laufzeit im Paket, für .NET gibt es kein Store-Framework-Paket);
  dafür setzt das csproj im Release die RuntimeIdentifier pro Plattform. Debug bleibt framework-dependent.
- Windows ML (onnxruntime, DirectML) kommt über das Metapaket (auch via H.NotifyIcon) mit und wird
  per Target aus Copy-Local- und Publish-Liste entfernt. Paket je Architektur ~46 MB.
- Sprachen stehen explizit im Manifest (x-generate trug nur en-US ein) und werden nicht in
  Ressourcenpakete ausgelagert, weil die App zur Laufzeit beide braucht.
- Release-Paket lokal testen: x64-msix aus dem Bundle entpacken und das Layout registrieren
  (vorher LocalState sichern, da das Debug-Paket entfernt werden muss).
- GitHub-Repo: https://github.com/1J3R0M3/sip-trinkerinnerung (öffentlich). Lokaler Ordner heißt weiter `trinkwasser-winui`.

Entscheidungen aus Schritt 2:
- Einstellungen speichern sofort (wie die Windows-Einstellungen), ohne Speichern-Button. Ungültige
  Eingaben werden angezeigt und nicht gespeichert. Der Assistent speichert erst bei „Fertig“,
  die Sprache schaltet dort aber schon als Vorschau um.
- `AppSettings.Autostart` ist nur der Wunsch aus dem Assistenten; maßgeblich ist die StartupTask.
- Unbehandelte Fehler landen lokal in `LocalState\error.log` (nichts wird versendet).

## Anforderungen

### Aus dem Electron-Repo übernehmen
- Texte aus `src/shared/locales/de.json` und `en.json` → bereits in `src/Core/Localization/Locales/`
  (App-Name dort durch `{app}` ersetzt)
- `PRIVACY.md` und Store-Texte aus `docs/STORE.md` (gibt es erst ab Commit cf096ae, nicht im Tag v0.2.0)
- Einstellungslogik inkl. Prüfung und Grenzwerten aus `src/main/settings.js`:
  - Tagesziel 250–10000 ml (Standard 2000), Wochenziel 250–70000 ml (Standard 10000),
    Glas 50–2000 ml (Standard 250), Intervall 5–240 min (Standard 60), Autostart Standard aus
  - Arbeitszeiten: Index 0 = Montag … 6 = Sonntag, Standard Mo–Fr 08:00–17:00 an, Sa/So aus
  - Zeiten im Format `HH:mm`; Ende muss nach Beginn liegen (keine Schichten über Mitternacht),
    sonst Rückfall auf die vorherigen Zeiten des Tages, nur `enabled` wird übernommen
  - Zahlen werden gerundet und auf die Grenzen geklemmt; ungültige Werte → vorheriger Wert
  - Sprache: Standard nach Windows-Sprache (de* → de, sonst en)
- Tropfen-Icon → bereits portiert nach `tools/gen-icons.cs`

### App-Name
Offiziell (vom Nutzer festgelegt, so in Partner Center zu reservieren): **„Sip-Trinkerinnerung“** (de)
und **„Sip-Waterreminder“** (en), in `src/App/Strings/<Sprache>/Resources.resw` als `AppDisplayName`
→ Manifest, Startmenü, StartupTask, Absender der Toasts. In der App selbst (Titelleiste, Tray,
Texte mit `{app}`) steht der Kurzname `AppShortName` = „Sip“ (`AppResources.AppName`).
Code-Namespace `Trinkwasser.*`, `Trinkwasser.exe` und der Repo-Name sind intern und bleiben.

### Funktionen
- System-Tray (H.NotifyIcon.WinUI). Fenster schließen = in den Tray; „Beenden“ im Tray-Menü.
- Nur eine Instanz: eigene `Main` mit `AppInstance.FindOrRegisterForKey` und `RedirectActivationToAsync`.
- Toasts über `Microsoft.Windows.AppNotifications` mit Buttons „Getrunken“ und „In 10 Min. erinnern“;
  Aktivierung bei laufender und bei beendeter App (Klick im Info-Center). AUMID aus dem MSIX-Paket.
- Einrichtungsassistent beim ersten Start: Sprache, Tagesziel, Wochenziel, Menge pro Glas,
  Erinnerungsintervall, Arbeitszeiten (eine gemeinsame Zeit + Wochentage zum Anklicken), Autostart.
  Wochenziel vorbelegt mit Tagesziel × Anzahl Arbeitstage und nachgeführt, bis der Nutzer es selbst ändert.
- Einstellungen: alles änderbar, Arbeitszeiten pro Wochentag einzeln (an/aus, von, bis), Button
  „Montag auf alle aktiven Tage übertragen“. Außerhalb der Arbeitszeit keine Erinnerungen.
  Schichten über Mitternacht nicht unterstützt (Ende > Beginn).
- Autostart: StartupTask im Manifest; Schalter über `Windows.ApplicationModel.StartupTask`
  (`RequestEnableAsync`/`Disable`). Status „vom Nutzer in Windows deaktiviert“ korrekt anzeigen.
  Bei Aktivierung über die StartupTask nur im Tray starten, ohne Fenster.
- Erinnerungslogik:
  - Intervall startet nach jedem „Getrunken“ neu. Snooze: 10 Minuten.
  - Nach Ende der Arbeitszeit keine Erinnerung mehr; am nächsten Arbeitstag zur Startzeit + Intervall.
  - Schlaf, Ruhezustand, Zeitumstellung: Termine nach dem Aufwachen neu berechnen, verpasste
    Erinnerungen nicht nachholen.
- Startseite: Fortschritt Tages- und Wochenziel, „+1 Glas“, „Rückgängig“ (letzten Eintrag löschen),
  Test-Benachrichtigung.
- Statistiken: Wochenverlauf als Balken (eigene XAML-Balken, keine Chart-Bibliothek),
  Durchschnitt pro Tag, beste Tageszeit (Stunde mit den meisten Gläsern), Streak (Tage mit erreichtem
  Tagesziel; Tage ohne Arbeitszeit unterbrechen den Streak nicht), Zielerreichung in Prozent.
- Sprachen Deutsch und Englisch, umschaltbar ohne Neustart. `x:Uid` aktualisiert sich nicht zur
  Laufzeit → eigener `Localizer` (Core) mit `{x:Bind Loc.Xyz, Mode=OneWay}`; Sprachwechsel löst
  `PropertyChanged(string.Empty)` aus. Tray-Menü und Toasts wechseln mit (`LanguageChanged`).
- Daten nur lokal als JSON (System.Text.Json) in `ApplicationData.Current.LocalFolder`, atomar
  schreiben (Temp-Datei + Ersetzen). Kaputte Dateien nicht stillschweigend überschreiben, sondern sichern.
- Keine Netzwerkzugriffe: keine Telemetrie, keine Updates aus dem Netz, keine `internetClient`-Capability.

### Technik
- .NET 10 (LTS), Windows App SDK 2.5.1, WinUI 3, verpackt (MSIX), MVVM mit CommunityToolkit.Mvvm.
- Projektmappe `Trinkwasser.slnx`:
  - `src/App` – WinUI 3, UI und Windows-Anbindung
  - `src/Core` – `net10.0` ohne WinUI: Einstellungen, Prüfung, Arbeitszeiten, Erinnerungsplanung,
    Statistiken; injizierbare Uhr (`TimeProvider`)
  - `tests/Core.Tests` – xUnit v3 (bewusst 3.2.2) + `Microsoft.Extensions.TimeProvider.Testing`.
    Pflicht-Tests: Arbeitszeitgrenzen, Wochenwechsel, Streak über Wochenenden, Zeitumstellung,
    beste Tageszeit.
- Paketversionen zentral in `Directory.Packages.props`.
- Optik: Windows-11-Stil (Mica, NavigationView oder schlichte Seitenstruktur), hell und dunkel.
- Store: `Package.appxmanifest` mit Identity Name/Publisher aus Partner Center (eingetragen, siehe docs/STORE.md),
  Logos in allen Skalierungen inkl. `targetsize`/`altform-unplated`, Sprachen de-DE und en-US,
  Architekturen x64 und arm64.
- README mit Voraussetzungen, Build-, Test- und Einreichungsanleitung; optional GitHub-Actions-Workflow,
  der baut, testet und `.msixupload` erzeugt.

## Bauen, testen, ausführen

```powershell
dotnet test tests/Core.Tests
dotnet build src/App/App.csproj -p:Platform=x64
Add-AppxPackage -Register src\App\bin\x64\Debug\net10.0-windows10.0.26100.0\AppxManifest.xml -ForceUpdateFromAnyVersion
Start-Process "shell:AppsFolder\1J3R0M3.Sip-Trinkerinnerung.Dev_984vmrdge06s6!App"   # Debug = "Sip (Dev)"
dotnet run tools/gen-icons.cs   # Icons neu erzeugen (aus dem Repo-Wurzelverzeichnis)
dotnet run tools/gen-badges.cs  # Erfolgs-Badges neu erzeugen (--svg <Ordner> schreibt zusätzlich die SVGs)
.\tools\Build-StorePackage.ps1   # Store-Paket → artifacts\AppPackages\*.msixupload
.\tools\Build-StorePackage.ps1 -Version 1.0.1   # Store-Update: Version im Manifest erhöhen + bauen
```

Vor einem Neubau die laufende Dev-Version beenden, sonst ist die EXE gesperrt:
`Get-Process Trinkwasser | Where-Object Path -like "*\bin\*" | Stop-Process`. **Nie** `Stop-Process -Name Trinkwasser` –
das beendet auch die Store-Version, die der Nutzer benutzt.
Alternativ in Visual Studio 2026 mit F5 (Plattform x64).

## Umgebung und Eigenheiten
- Visual Studio Community 2026 mit .NET-Desktop-Workload und WinUI-Komponente, .NET SDK 10.0.40x, Entwicklermodus an.
  Installiert per winget; die WinUI-Komponente heißt in VS 2026 `Microsoft.VisualStudio.Component.WindowsAppSdkSupport.CSharp`.
- Kein Python auf dem Rechner.
- **Die Store-Version von Sip ist auf diesem Rechner installiert und wird benutzt.** Nie in ihren Datenordner
  (`…\Packages\1J3R0M3.Sip-Trinkerinnerung_984vmrdge06s6\LocalState`) schreiben und das Paket nie entfernen.
  Debug-Builds heißen „Sip (Dev)“ mit eigener Kennung `1J3R0M3.Sip-Trinkerinnerung.Dev` und eigener
  Toast-CLSID (Target `_UseDevPackageIdentity` im App.csproj); Testdaten nur in deren LocalState.
- Dateibasierte C#-Skripte: zentrale Paketversionen gelten auch dort (`#:package Name` ohne Version,
  Version in `Directory.Packages.props`); für COM (z. B. NAudio/Media Foundation)
  `#:property PublishAot=false` und `#:property BuiltInComInteropSupport=true`.
- Smart App Control ist auf diesem Rechner **aus** (blockierte unsignierte Builds). Hilfsskripte
  trotzdem ohne eigene EXE ausführen (`#:property UseAppHost=false`).
- `dotnet` ist in Bash-Sitzungen evtl. nicht im PATH: `export PATH="$PATH:/c/Program Files/dotnet"`.
- Windows PowerShell 5.1 zerlegt Argumente mit Anführungszeichen: Commit-Nachrichten per Datei
  übergeben (`git commit -F <datei>`).
- Assets werden ins Ausgabeverzeichnis kopiert (`CopyToOutputDirectory`), damit die Registrierung
  direkt aus `bin` funktioniert.
- Ändert sich das Manifest (z. B. neue Extension), scheitert `Add-AppxPackage -Register` mit
  0x80073CFB. Dann erst `Get-AppxPackage 1J3R0M3.Sip-Trinkerinnerung.Dev | Remove-AppxPackage` (löscht LocalState der Dev-Version;
  nie das Store-Paket ohne `.Dev` entfernen!).
- `StartupTask.GetAsync` nie direkt auf dem UI-Thread im Loaded-Ereignis aufrufen: WinUI stürzt
  dann nativ mit 0xC000027B ab (Wiedereintritt). `AutostartService` holt die Task per `Task.Run`.
- WinUI-Abstürze mit 0xC000027B zeigen sich ohne Debugger nur im Ereignisprotokoll
  (Anwendung, „Application Error“); zum Eingrenzen helfen Ablaufmarken in `error.log`.
- TimePicker übernimmt `AutomationProperties.Name` nur beim Aufbau. Nach einem Sprachwechsel
  werden die Zeilen in den Einstellungen deshalb neu erzeugt, im Assistenten lädt Schritt 3 per `x:Load`.
- Bei gesperrtem Bildschirm liefern Screenshots nur den Sperrbildschirm; UI Automation
  (PowerShell, `System.Windows.Automation`) funktioniert trotzdem und zeigt die Screenreader-Namen.
- `PublisherDisplayName` im Manifest muss wörtlich `1J3R0M3` sein: Partner Center lehnt
  `ms-resource:` dort ab (DisplayName per `ms-resource:` ist erlaubt).
- Toast-Aktivierung: COM-Server mit CLSID `5260F15E-66CD-45D6-9EC1-5FC32E5E2913` im Manifest;
  `NotificationInvoked` wird vor `Register()` angehängt; Kaltstart per Toast kommt als
  `ExtendedActivationKind.AppNotification` über `GetActivatedEventArgs()`.

## Git
- Commits mit der E-Mail `180740383+1J3R0M3@users.noreply.github.com` (im Repo konfiguriert).
- **Pushen nur auf Nachfrage.** Remote `origin` = github.com/1J3R0M3/sip-trinkerinnerung.
- Commit-Nachrichten auf Deutsch, wie im Electron-Repo.
- Texte und Kommentare im Code auf Deutsch.
