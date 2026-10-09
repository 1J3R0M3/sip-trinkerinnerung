# Sip – Trinkerinnerung für Windows 11

Sip läuft im Infobereich und erinnert während der Arbeitszeit per
Windows-Benachrichtigung ans Wassertrinken. „Getrunken“ zählt ein Glas,
„In 10 Min. erinnern“ verschiebt die Erinnerung.

- Erinnerungen im Wunschintervall, nur während der Arbeitszeit (pro Wochentag)
- Tages- und Wochenziel mit Fortschritt, „+1 Glas“ und „Rückgängig“
- Statistik: Wochenverlauf, Durchschnitt, beste Tageszeit, Serie, Zielerreichung
- Deutsch und Englisch, umschaltbar ohne Neustart
- Optionaler Autostart, dann nur im Infobereich
- Kostenlos, ohne Werbung, ohne Telemetrie und ohne Netzwerkzugriff – alle Daten
  bleiben lokal ([Datenschutz](PRIVACY.md))

Gebaut mit C#, WinUI 3 und dem Windows App SDK, verteilt über den Microsoft
Store als MSIX. Nachfolger des Electron-Prototyps
[trinkwasser-app](https://github.com/1J3R0M3/trinkwasser-app).

## Voraussetzungen

- Windows 11 (ab 10.0.22000), x64 oder arm64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (siehe `global.json`)
- **Entwicklermodus** an (*Einstellungen → System → Für Entwickler*), damit sich
  das Paket direkt aus dem Build-Ordner registrieren lässt
- Optional: Visual Studio 2026 mit der Workload „WinUI-Anwendungsentwicklung“
  (F5 mit Plattform x64 baut, registriert und startet die App)

## Bauen, testen, ausführen

```powershell
dotnet test tests/Core.Tests
dotnet build src/App/App.csproj -p:Platform=x64
Add-AppxPackage -Register src\App\bin\x64\Debug\net10.0-windows10.0.26100.0\AppxManifest.xml -ForceUpdateFromAnyVersion
Start-Process "shell:AppsFolder\PLACEHOLDER.Trinkwasser_7pn0cst8w4gvm!App"
```

- Vor einem Neubau die laufende App beenden (`Stop-Process -Name Trinkwasser`),
  sonst ist die EXE gesperrt.
- Ändert sich das Manifest, scheitert die Registrierung mit 0x80073CFB. Dann
  erst `Get-AppxPackage PLACEHOLDER.Trinkwasser | Remove-AppxPackage` – das
  löscht auch die App-Daten.
- Icons neu erzeugen: `dotnet run tools/gen-icons.cs` (aus dem Wurzelverzeichnis).

## Store-Paket und Einreichung

```powershell
.\tools\Build-StorePackage.ps1
```

erzeugt `artifacts\AppPackages\*.msixupload` (x64 + arm64, unsigniert; der Store
signiert selbst). Die Version steht in `src/App/Package.appxmanifest` und muss
vor jeder Einreichung steigen. Konto, App-Name, Produktidentität, Einreichung
und Store-Texte: [docs/STORE.md](docs/STORE.md).

GitHub Actions (`.github/workflows/build.yml`) führt bei jedem Push die Tests aus
und hängt das `.msixupload` als Artefakt an den Workflow-Lauf.

## Aufbau

| Ordner             | Inhalt |
| ------------------ | ------ |
| `src/Core`         | `net10.0` ohne WinUI: Einstellungen mit Prüfung, Arbeitszeiten, Erinnerungsplanung, Trink-Protokoll, Statistik, Texte (de/en). Uhr über `TimeProvider`, daher gut testbar. |
| `src/App`          | WinUI 3: Fenster, Seiten, ViewModels (CommunityToolkit.Mvvm), Tray (H.NotifyIcon), Benachrichtigungen, Autostart, Single-Instance. |
| `tests/Core.Tests` | xUnit v3, u. a. Arbeitszeitgrenzen, Wochenwechsel, Serie über Wochenenden, Zeitumstellung, beste Tageszeit. |
| `tools`            | Icon-Generator, Store-Build-Skript. |

Der angezeigte App-Name steht nur in `src/App/Strings/*/Resources.resw`
(`AppDisplayName`); alle anderen Stellen lesen ihn von dort.

## Daten

Einstellungen (`settings.json`) und Trink-Protokoll (`drinks.json`) liegen im
App-Datenordner `%LOCALAPPDATA%\Packages\<Paketname>\LocalState`. Geschrieben
wird atomar; eine unlesbare Datei wird als `*.broken-<Zeitstempel>.json`
gesichert statt überschrieben, die App weist darauf hin. Unerwartete Fehler
landen in `error.log` im selben Ordner. Beim Deinstallieren löscht Windows den
Ordner.

## Lizenz

[MIT](LICENSE)
