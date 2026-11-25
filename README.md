# The PSS Oric Collection

Five Personal Software Services arcade games from the Oric-1/Atmos, recreated in MonoGame with
modern graphics, synthesised sound, a front-page game menu, four skill levels per game and high
score tables that survive closing the app.

| Game          | Oric original | Recreation |
|---------------|---------------|------------|
| Invaders      | 1983          | Space Invaders-style wave shooter: shields, UFO bonus, diving formations |
| Centipede     | 1983          | Mushroom field, splitting centipede, spider, flea |
| Hopper        | 1983          | Frogger: four river lanes of logs and diving turtles, a patrolling snake, three traffic lanes, five home bays, one TIME budget per sheet |
| Elektro Storm | 1983          | Missile Command: six cities, three batteries, a crosshair, counter-missile bursts |
| Light Cycle   | 1983          | Tron light cycles: trails, rival cycles, rounds that speed up |

The Ultra and OricMon are deliberately **not** included.

## Identifiers

| Platform | Identifier |
|----------|------------|
| Android / iOS / macOS | `uk.co.allthejohnsons.psscollection` |
| Windows (MSIX) | `49556nodoid.ThePSSOricCollection`, `CN=A6EAEB04-6634-41C1-BDFE-695819ECE444`, `PublisherDisplayName = all-the-johnsons` |

## Playing

* Arrows or a gamepad move; **Fire** shoots or starts; **P** pauses; **Esc** goes back.
* On touch devices there is an on-screen d-pad and fire button; in Elektro Storm a tap aims and
  fires the crosshair directly.
* Every game offers **Novice, Skilled, Veteran and Ace**. Higher skills mean faster enemies, more
  of them and a bigger score multiplier (x1, x1.25, x1.5, x2).
* High scores are kept per game (top ten, with skill, level reached and date) in
  `highscores.json` under the platform's application-data folder:
  * macOS: `~/Library/Application Support/PSSCollection/highscores.json`
  * iOS: the app's Documents folder
  * Android: the app's private files folder
  * Windows: `%LOCALAPPDATA%\PSSCollection\highscores.json`

## Projects

| Project | Target | Contents |
|---------|--------|----------|
| `PSSCollection.Engine` | net10.0 | All game rules, skill levels, entity list, high-score model and JSON store. No graphics, fully unit tested. |
| `PSSCollection.Core` | net10.0 | Shared MonoGame layer: virtual 1280x720 canvas, procedural pixel font, procedural shapes, procedural audio, screens. |
| `PSSCollection.DesktopGL` | net10.0 | macOS build (also runs on Linux/Windows GL). |
| `PSSCollection.iOS` | net11.0-ios | iPhone/iPad, iOS 18+, UIScene lifecycle. |
| `PSSCollection.Android` | net10.0-android | Android API 32+. |
| `PSSCollection.WindowsDX` | net10.0-windows | Windows and Xbox (DX12), MSIX manifest included. |
| `PSSCollection.Tests` | net11.0 | xunit v3 on Microsoft.Testing.Platform. |

There is no content pipeline: every graphic is drawn from primitives at runtime and every sound is
synthesised, so the games build from source with no asset steps.

## Documentation

* [`docs/architecture.md`](docs/architecture.md) - the virtual canvas, the screen model, input, platform
  services, persistence and audio, and where to add a game.
* [`docs/games.md`](docs/games.md) - the five games as implemented, with their board sizes, scoring and
  the skill table that tunes all of them.
* [`docs/building.md`](docs/building.md) - building each head, the release pipeline, Mac App Store
  signing and profiles, and how the store screenshots are produced.

## Building

```bash
dotnet build PSSCollection.slnx            # all libraries and heads
dotnet test PSSCollection.Tests            # unit tests
tools/build_macos.sh                       # -> build/macos/PSS Collection.app

dotnet build PSSCollection.iOS -c Release -f net11.0-ios -p:RuntimeIdentifier=ios-arm64
dotnet build PSSCollection.Android -c Release -f net10.0-android
dotnet publish PSSCollection.WindowsDX -c Release -r win-x64 --self-contained true \
    -p:EnableWindowsTargeting=true -o build/windows
```

`tools/capture.sh <scenario>` drives the macOS build with synthetic input and writes window
screenshots, which is how the layouts were checked.

## Releases and stores

```bash
tools/build_release.sh                     # everything, into Releases/<version>/
tools/build_release.sh macos ios android   # or pick targets
tools/build_windows.sh Releases/1.0.0      # MSIX x64 + arm64, packed in the Windows VM
python3 tools/make_store_artwork.py        # store artwork and framed screenshots in Stores/
```

`Releases/` holds the macOS Mac App Store `.pkg`, the iOS `.ipa`, the signed Android `.aab`
and `.apk`, and one MSIX per Windows architecture. `Stores/` holds the screenshots, icons
and artwork for each store together with the submission copy. Neither folder is committed.

## Tests

`dotnet test PSSCollection.Tests` runs the engine suite: scoring and extra lives, skill tuning,
high-score ordering, persistence round-trips and corrupt-file recovery, plus rule tests for each
game (marching and diving in Invaders, splitting and spiders in Centipede, drowning, riding and
home bays in Hopper, bursts and city loss in Elektro Storm, trails, turns and crashes in Light
Cycle).

## Notes and credits

The games are original PSS releases, written in 1983 for the Oric-1:

* Invaders, Centipede, Hopper, Elektro Storm and Light Cycle were published by Personal Software
  Services (Coventry).
* Hopper and Light Cycle were written by A.J. Clarke; Elektro Storm by Tony Stoddart.
* The rules implemented here follow the original games, checked against the authors' published
  source and rule documentation for Hopper and Light Cycle (the `Hopper-Oric` and
  `Light-Cycle-Oric` repositories) and against the published PSS catalogue descriptions. The
  original tapes were not run under Oricutron for this build because no emulator binary was
  installed on this machine; the tape images themselves were obtained, not used.

All code in this repository is new; no original PSS code or artwork is included.

## Licence

The DILLIGAF licence (Do I Look Like I Give A F***): do what you like with it - copy, change,
sell, port it - with no attribution and no warranty. The full text is in `LICENSE`. It is not an
OSI-approved licence, which will surprise nobody.
