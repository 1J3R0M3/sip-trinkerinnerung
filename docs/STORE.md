# Veröffentlichung im Microsoft Store

Der Store signiert das Paket beim Einreichen selbst. Ein eigenes
Code-Signing-Zertifikat ist nicht nötig.

## 1. Einmalig: Konto, App-Name und Produktidentität

1. Entwicklerkonto in [Partner Center](https://partner.microsoft.com/dashboard)
   anlegen (Einzelperson).
2. **Apps und Spiele → Neues Produkt → MSIX- oder PWA-App** und den App-Namen
   **„Sip-Trinkerinnerung“** reservieren, dann unter **Produktverwaltung →
   App-Namen verwalten** zusätzlich **„Sip-Waterreminder“**. Jeder Name im Paket
   muss in Partner Center reserviert sein, und zwar genau so geschrieben.
3. Die Namen stehen zentral in `src/App/Strings/de-DE/Resources.resw` und
   `src/App/Strings/en-US/Resources.resw`:
   - `AppDisplayName`: offizieller Name für Store, Startmenü, Autostart-Eintrag
     und als Absender der Benachrichtigungen
   - `AppShortName`: „Sip“ in der App selbst (Titelleiste, Tray, Texte)

   Ist ein Name schon vergeben, nur `AppDisplayName` anpassen.
4. Unter **Produktverwaltung → Produktidentität** stehen die Werte, die die
   Platzhalter ersetzen:

   | Partner Center                          | Wo im Projekt                                              |
   | --------------------------------------- | ---------------------------------------------------------- |
   | Package/Identity/Name                   | `Package.appxmanifest` → `Identity Name`                   |
   | Package/Identity/Publisher (`CN=…`)     | `Package.appxmanifest` → `Identity Publisher`              |
   | Package/Properties/PublisherDisplayName | beide `Resources.resw` → `PublisherDisplayName`            |

   Danach ändert sich der Paketname. Für lokale Tests den Start-Befehl im README
   anpassen (`shell:AppsFolder\<Name>_<Hash>!App`, den Namen mit Hash zeigt
   `Get-AppxPackage <Name>`).

## 2. Paket bauen

```powershell
.\tools\Build-StorePackage.ps1
```

Ergebnis: `artifacts\AppPackages\App_<version>_x64_arm64_bundle.msixupload`.
Das Bündel enthält x64 und arm64 samt .NET-Laufzeit; der Store liefert jedem
Gerät nur seine Architektur aus (rund 45 MB). Die Windows App SDK Runtime
installiert der Store als Abhängigkeit selbst.

GitHub Actions baut dasselbe Paket bei jedem Push auf `main` und hängt es als
Artefakt an den Workflow-Lauf (`.github/workflows/build.yml`).

**Version:** Vor jeder Einreichung `Version` in `src/App/Package.appxmanifest`
erhöhen. Format `Major.Minor.Build.0` – die letzte Stelle muss 0 sein.

## 3. Einreichung

- **Pakete:** die `.msixupload`-Datei hochladen.
- **Gerätefamilie:** nur Desktop (Windows 11, ab 10.0.22000).
- **Datenschutzrichtlinie:**
  `https://github.com/1J3R0M3/sip-trinkerinnerung/blob/main/PRIVACY.md`
- **Eingeschränkte Funktion `runFullTrust`.** Begründung, die Partner Center
  abfragt:

  > The app is a packaged WinUI 3 desktop application (Windows App SDK). Every
  > packaged desktop (Win32) app declares runFullTrust. The app uses it only to
  > show a notification-area icon, local Windows notifications and an optional
  > startup task. It makes no network connections and stores all data locally.

- **Altersfreigabe:** Fragebogen ausfüllen. Keine Inhalte mit
  Altersbeschränkung, keine Nutzerinteraktion, keine Käufe.
- **Screenshots:** mindestens einer, 1366 × 768 oder größer. Sinnvoll:
  Startseite, Statistik, Einstellungen, eine Erinnerung.
- **Preis/Verfügbarkeit:** kostenlos. Zum Testen zuerst „Privates Publikum“ oder
  „Ausgeblendet, nur per Direktlink“ wählen.

## 4. Store-Eintrag (Texte)

### Deutsch

**Kurzbeschreibung**
Erinnert dich während der Arbeit ans Wassertrinken – mit Tages- und Wochenziel.

**Beschreibung**
Sip läuft unauffällig im Infobereich und erinnert dich in deinem
Wunschintervall per Windows-Benachrichtigung an ein Glas Wasser. Mit einem Klick
auf „Getrunken“ wird es gezählt, mit „In 10 Min. erinnern“ verschoben.

- Erinnerungen nur während deiner Arbeitszeiten, für jeden Wochentag einstellbar
- Tages- und Wochenziel mit Fortschrittsanzeige
- Statistik: Wochenverlauf, Tagesdurchschnitt, beste Tageszeit, Serie
- Deutsch und Englisch, umschaltbar ohne Neustart
- Auf Wunsch Start mit Windows, ohne Fenster im Infobereich
- Keine Werbung, kein Konto, keine Internetverbindung – alle Daten bleiben auf
  deinem Gerät

**Suchbegriffe:** Wasser, trinken, Erinnerung, Gesundheit, Hydration

### English

**Short description**
Reminds you to drink water while you work – with daily and weekly goals.

**Description**
Sip sits quietly in the notification area and reminds you to have a
glass of water at your chosen interval. Click "Done" to log it or "Remind me in
10 min" to snooze.

- Reminders only during your working hours, configurable per weekday
- Daily and weekly goals with progress
- Statistics: weekly history, daily average, best time of day, streak
- German and English, switchable without restarting
- Optionally starts with Windows, without a window, in the notification area
- No ads, no account, no internet connection – all data stays on your device

**Keywords:** water, drink, reminder, health, hydration

## Besonderheiten der Store-Version

- **Autostart:** StartupTask im Manifest, standardmäßig aus. Eingeschaltet wird
  er im Einrichtungsassistenten oder in den Einstellungen der App. Hat der
  Nutzer ihn in den Windows-Einstellungen ausgeschaltet, kann die App ihn nicht
  wieder einschalten und zeigt einen Link dorthin.
- **Start im Tray:** Beim Start über die StartupTask öffnet die App kein Fenster.
- **Benachrichtigungen:** AppUserModelID und COM-Aktivierung (Klick bei
  beendeter App) kommen aus dem Manifest.
- **Daten** liegen im privaten Paketordner (`LocalState`) und werden beim
  Deinstallieren gelöscht.
