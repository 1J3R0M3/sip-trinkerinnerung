# Benachrichtigungstöne – Herkunft und Lizenz

Alle Töne stammen von [Freesound](https://freesound.org) und stehen unter
**[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)** (gemeinfrei, keine Namensnennung nötig).
Die Urheber sind trotzdem hier genannt. Verwendet wurden die Hörproben in hoher Qualität (MP3),
aufbereitet mit `tools/process-sounds.cs` (Mono, Stille abgeschnitten, höchstens 3 s,
Lautstärke angeglichen, ein- und ausgeblendet).

| Datei | Name in Sip | Original | Urheber |
|---|---|---|---|
| `drop.wav` | Wassertropfen | [Drop - Water](https://freesound.org/s/533146/) | mattfinarelli |
| `clear-drop.wav` | Klarer Tropfen | [Water_drop_9.wav](https://freesound.org/s/166325/) | (gelöschtes Konto) |
| `soft-drop.wav` | Sanfter Tropfen | [Fine Water Drop](https://freesound.org/s/792927/) | qubodup |
| `quick-drop.wav` | Schneller Tropfen | [Quick Water Droplet](https://freesound.org/s/792931/) | qubodup |
| `plip.wav` | Plip | [Drip 5 [dry] [plip]](https://freesound.org/s/524222/) | magnuswaker |
| `wine-glass.wav` | Weinglas | [wine glass clink](https://freesound.org/s/573160/) | fleurescence |
| `crystal.wav` | Kristallglocke | [2022-01-14-crystal-bell-08.flac](https://freesound.org/s/614832/) | arseniiv |
| `glass-ping.wav` | Glas-Ping | [Glass Ping](https://freesound.org/s/745014/) | tix99 |
| `ice-cube.wav` | Eiswürfel | [Ice cube falling in glass 5](https://freesound.org/s/837694/) | Valerie-Vivegnis |
| `quick-pour.wav` | Kurz eingießen | [Quick Pour](https://freesound.org/s/235779/) | elliotlp |
| `pour.wav` | Wasser ins Glas | [Water is Poured into a Glass](https://freesound.org/s/741348/) | Mish7913 |
| `plop.wav` | Plopp | [Water + pebble](https://freesound.org/s/386429/) | giddster |
| `bubbles.wav` | Blubbern | [raw_bubblingdrink5.ogg](https://freesound.org/s/398029/) | swordofkings128 |

Neu erzeugen: MP3-Hörproben nach `artifacts/sounds-raw/<id>.mp3` legen
(`https://cdn.freesound.org/previews/<…>-hq.mp3`), dann im Repo-Wurzelverzeichnis
`dotnet run tools/process-sounds.cs`.
