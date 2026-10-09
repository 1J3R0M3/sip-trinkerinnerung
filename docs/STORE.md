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
4. Die Produktidentität aus **Produktverwaltung → Produktidentität** ist
   eingetragen und darf nicht mehr geändert werden:

   | Partner Center                          | Wert                                     | Wo im Projekt                                   |
   | --------------------------------------- | ---------------------------------------- | ----------------------------------------------- |
   | Package/Identity/Name                   | `1J3R0M3.Sip-Trinkerinnerung`            | `Package.appxmanifest` → `Identity Name`        |
   | Package/Identity/Publisher              | `CN=DA341E14-0D55-4F79-AE19-763C3E8DD1B5` | `Package.appxmanifest` → `Identity Publisher`   |
   | Package/Properties/PublisherDisplayName | `1J3R0M3`                                | `Package.appxmanifest` → `PublisherDisplayName` (wörtlich, kein `ms-resource:`) |

   Paketfamilie lokal: `1J3R0M3.Sip-Trinkerinnerung_984vmrdge06s6`.

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

**Vorab prüfen (optional):** Windows App Certification Kit (Teil des Windows
SDK, Feature `OptionId.WindowsSoftwareLogoToolkit`). Version 1.0.0.0 hat alle
Pflichttests bestanden. Der optionale Test „Blockierte ausführbare Dateien“
schlägt an, weil in .NET-Laufzeit-DLLs Zeichenfolgen wie „cmd“ oder Aufrufe
von `CreateProcess` vorkommen; das ist bei self-contained .NET-Apps normal und
für den Store kein Hindernis.

**Version:** Für ein Update die neue Version mitgeben:

```powershell
.\tools\Build-StorePackage.ps1 -Version 1.0.1
```

Das Skript schreibt `1.0.1.0` nach `src/App/Package.appxmanifest` und bricht ab,
wenn die Version nicht höher ist als die bisherige. Ohne `-Version` wird die
aktuelle Version neu gebaut (z. B. nach einem abgelehnten Upload, der noch
nicht eingereicht war). Die Änderung am Manifest danach committen.

## Updates

1. Änderungen testen, dann `.\tools\Build-StorePackage.ps1 -Version <neu>`
   (Fehlerbehebung: 1.0.0 → 1.0.1, neue Funktionen: 1.0.1 → 1.1.0).
2. Partner Center: **Apps und Spiele → Sip-Trinkerinnerung → Update**. Die
   neue Übermittlung übernimmt alle bisherigen Angaben.
3. Unter **Pakete** das alte Paket entfernen, das neue hochladen.
4. Optional in den Store-Einträgen „Neuerungen in dieser Version“ ausfüllen und
   Screenshots tauschen.
5. Übermitteln. Der Store verteilt das Update nach der Zertifizierung
   automatisch; Einstellungen und Trinkprotokoll der Nutzer bleiben erhalten.

Texte, Screenshots, Preis und Sichtbarkeit lassen sich auch ohne neues Paket
ändern (Update ohne Paketwechsel, keine neue Version nötig).

## 3. Einreichung Schritt für Schritt

In Partner Center: **Apps und Spiele → Sip-Trinkerinnerung → Übermittlung
starten**. Die Abschnitte lassen sich in beliebiger Reihenfolge ausfüllen und
zwischenspeichern; „An den Store übermitteln“ wird erst aktiv, wenn alle
vollständig sind.

### 3.1 Preise und Verfügbarkeit
- **Märkte:** alle Märkte (Standard).
- **Sichtbarkeit:** für die erste Version **„Privates Publikum“** (nur
  eingeladene Microsoft-Konten finden die App) oder **„Öffentlich, aber nicht
  auffindbar“** (nur per Direktlink). Später auf „Öffentlich“ umstellen; das
  geht ohne neues Paket.
- **Zeitplan:** „So bald wie möglich“.
- **Preis:** **Kostenlos**, keine Testversion.

### 3.2 Eigenschaften
- **Kategorie:** Gesundheit und Fitness (keine Unterkategorie nötig).
- **Datenschutzrichtlinie (URL):**
  `https://github.com/1J3R0M3/sip-trinkerinnerung/blob/main/PRIVACY.md`
- **Website:** `https://github.com/1J3R0M3/sip-trinkerinnerung`
- **Supportkontakt:** E-Mail-Adresse oder
  `https://github.com/1J3R0M3/sip-trinkerinnerung/issues`
- **Produkterklärungen:** „Diese App wurde getestet, um die Richtlinien zur
  Barrierefreiheit zu erfüllen“ darf angehakt werden (Tastatur, Screenreader,
  Kontrast hell/dunkel geprüft). Alles andere frei lassen.
- **Systemanforderungen:** leer lassen.

### 3.3 Altersfreigaben
IARC-Fragebogen, Kategorie **„App (kein Spiel)“**, „Andere“ (keine Social-,
Browser- oder Messenger-App). Alle Fragen zu Gewalt, Sexualität, Drogen,
Glücksspiel, Sprache: **Nein**. Nutzer interagieren nicht miteinander, teilen
keinen Standort, keine Käufe, kein uneingeschränkter Internetzugang: **Nein**.
Ergebnis ist in der Regel „ab 3 Jahren“ / USK 0.

### 3.4 Pakete
- `artifacts\AppPackages\App_<version>_x64_arm64_bundle.msixupload` per
  Drag & Drop hochladen und die Prüfung abwarten (grüner Haken).
- **Gerätefamilien:** nur **Windows 10/11 Desktop** (ergibt sich aus dem
  Manifest, ab Windows 11 / 10.0.22000).

### 3.5 Store-Einträge (je Sprache: Deutsch und Englisch)
Die Sprachen übernimmt Partner Center aus dem Paket. Pro Sprache:
- **Beschreibung, Kurzbeschreibung, Suchbegriffe:** aus Abschnitt 4 unten.
- **Produktfunktionen:** die Aufzählungspunkte aus der Beschreibung, einzeln.
- **Screenshots (Desktop):** aus `artifacts\Screenshots\` (1600 × 900):
  `de-*.png` für Deutsch, `en-*.png` für Englisch. Reihenfolge wie die
  Dateinamen. Neu aufnehmen: siehe unten.
- **Store-Logos:** optional; ohne eigene Logos nimmt der Store die Logos aus
  dem Paket.
- **Copyright:** `© 2026 1J3R0M3`
- Alle übrigen Felder (Trailer, zusätzliche Lizenzbedingungen, Neuerungen in
  dieser Version) können bei der ersten Version leer bleiben.

### 3.6 Übermittlungsoptionen
- **Veröffentlichungsoptionen:** „Sobald die Zertifizierung abgeschlossen ist“
  oder „Manuell veröffentlichen“, wenn du den Zeitpunkt selbst bestimmen willst.
- **Hinweise zur Zertifizierung** (für die Prüfer, englisch):

  > No account or sign-in required. On first start a short setup wizard asks
  > for language, goals, reminder interval and working hours. Reminders only
  > appear during the configured working hours; to test a reminder immediately,
  > use "Send test notification" on the Overview page or "Test notification"
  > in the tray menu.
  > Closing the window keeps the app running in the notification area; use
  > "Quit" in the tray menu to exit. The app makes no network connections.

- **Eingeschränkte Funktion `runFullTrust`** – Begründung:

  > The app is a packaged WinUI 3 desktop application (Windows App SDK). Every
  > packaged desktop (Win32) app declares runFullTrust. The app uses it only to
  > show a notification-area icon, local Windows notifications and an optional
  > startup task. It makes no network connections and stores all data locally.

### 3.7 Übermitteln
**An den Store übermitteln** klicken. Die Zertifizierung dauert meist bis zu
drei Werktage; den Stand zeigt Partner Center unter der Übermittlung. Bei einer
Ablehnung steht im Bericht, welche Richtlinie betroffen ist – Paket korrigieren,
Version erhöhen, neu bauen und dieselbe Übermittlung erneut hochladen.

Nach der Freigabe steht der Store-Link unter **Produktverwaltung →
Produktidentität** (`https://apps.microsoft.com/detail/<Store-ID>`).

### Screenshots neu aufnehmen
Mit installierter App spielt ein Hilfsskript Demo-Daten ein, nimmt Übersicht,
Statistik und Einstellungen in beiden Sprachen hell und dunkel mit 1600 × 900
auf und stellt danach die eigenen Daten wieder her. Es liegt nicht im Repo;
bei Bedarf neu erstellen lassen.

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
- Ein Smiley zeigt auf einen Blick, ob du für die Uhrzeit genug getrunken hast
- Countdown-Kreis bis zur nächsten Erinnerung
- Statistik: Wochenverlauf, Tagesdurchschnitt, beste Tageszeit, Serie
- Wissenswertes rund ums Wasser auf der Startseite
- Hell, dunkel oder wie Windows
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
- A smiley shows at a glance whether you're on track for the time of day
- Countdown ring until the next reminder
- Statistics: weekly history, daily average, best time of day, streak
- Water facts on the home page
- Light, dark or system theme
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
