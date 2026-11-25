# Architecture

The collection is one game framework with five rule sets and five platform heads. The rule sets
do not know that MonoGame exists, and the heads do not know which games they are running.

```
                    PSSCollection.Engine          net10.0, no graphics
                    rule sets, entities, scores
                             ^
                             | project reference
                             |
                    PSSCollection.Core            net10.0, MonoGame
                    PssGame, screens, canvas, input, audio, persistence
                             ^
        +--------------------+--------------------+--------------------+
        |                    |                    |                    |
  DesktopGL             iOS (net11.0-ios)   Android (net10)      WindowsDX
  macOS / Linux         UIScene lifecycle   MainActivity         DX12 + MSIX
        |
  PSSCollection.Tests        xunit v3 over the engine only
```

## The frame

`PssGame` (in `Core/PssGame.cs`) is the whole shared application; each head is a `Program.cs` that
constructs it with its own `IPlatformServices` and calls `Run()`.

* **Virtual canvas.** Everything is drawn into a 1280x720 `RenderTarget2D` at
  `ScreenCanvas.VirtualWidth`/`VirtualHeight`. That target is then blitted to the back buffer with
  a destination rectangle computed in `ComputeDestination()`: the largest 16:9 rectangle that fits,
  centred, so a wider or taller window gets black bars rather than stretched pixels. Screens and
  the engine deal only in virtual coordinates; nothing else needs to know the window size.
* **Fixed step.** `IsFixedTimeStep` with a 1/60s `TargetElapsedTime`, and the update delta is
  clamped to 0.1s so a stalled frame cannot teleport a cycle across the grid.
* **No content pipeline.** Content.RootDirectory is set but unused: the pixel font, the shapes, the
  palette and every sound are produced at runtime (`Core/Graphics`, `Core/Audio`).

## Screens

`Screen` (in `Core/Screens/Screen.cs`) is a full page with `Enter`, `Leave`, `Update(delta, input)`
and `Draw(time)`. `PssGame` holds exactly one current screen; `Navigate(screen)` leaves the old one
and enters the new one, so there is no stack and no back history to keep in sync. Screens are:

| Screen | Reached from | Leaves to |
|--------|--------------|-----------|
| `TitleScreen` | start-up | `MenuScreen` on tap/fire |
| `MenuScreen` | title, and back from a game | `SkillScreen`, `HighScoresScreen`, or quit |
| `SkillScreen` | a chosen game | `PlayScreen` with that skill |
| `PlayScreen` | skill choice | `NameEntryScreen` at game over, `HighScoresScreen`, `MenuScreen` |
| `NameEntryScreen` | game over with a qualifying score | back to `MenuScreen` after saving |
| `HighScoresScreen` | menu, or a finished run | back to wherever it came from |

`PlayScreen` is the adapter between the framework and the engine: it owns the current `IGame`,
feeds it `InputState` built from `GameInput`, drains `IGame.DrainSounds()` into the `SoundBank`,
draws the HUD, and hands finished runs to `ScoreService`.

## The engine

* `IGame` is what screens and the renderer need: score, lives, level, entities, `PlayfieldSize`,
  messages, the sound queue, `Start(skill, rng)` and `Update(delta, input)`.
* `GameBase` implements the shared plumbing: skill-scaled `Award(points)` (rounded away from zero,
  minimum 1 point), an extra life every 10,000 points, `CompleteWave(bonus)`, `EndGame()`, the
  message timer and pause handling. Games implement `UpdateCore` and `OnStart`.
* `Entity` is a small struct (kind, position, size, colour, phase) filled in by each game each
  frame and read by `EntityRenderer` in Core. The engine never touches a texture.
* `IRandomSource` / `XorShiftRandom` make runs reproducible in tests.
* `Catalogue` is the single source of truth for titles, taglines, blurbs and control hints, and it
  lists the titles that must never appear (`THE ULTRA`, `ORICMON`).

## Input

`InputManager` (Core/Input) samples keyboard, gamepad, mouse and touch once per frame and produces
one immutable `GameInput`: discrete directions, `Confirm`, `Back`, `Pause`, `HighScores`, `Clear`,
pointer position and clicks, an `InputState` for the running game, and the current touch contacts.

Bindings: arrows/WASD move; Space, Enter, Z, X, gamepad A or Start confirm; Escape, Backspace,
gamepad B or Back go back; P or gamepad Y pauses; H opens high scores from the menu; C clears a
table.

Touch is the awkward one, so it has its own seam. A platform that can report touches supplies an
`ITouchSource` (iOS does: `IosTouchFeed` in `PSSCollection.iOS/Program.cs`, fed by a transparent
`UIView` overlay) and reports the coordinate space in points through `IPlatformServices.TouchSpace`.
Everything else uses MonoGame's `TouchPanel`. `InputManager` also drives the on-screen pad and fire
button on mobile, and converts raw positions into virtual pixels through the same destination
rectangle the renderer uses, so a touch lands where the player sees it hit.

## Platform services

`IPlatformServices` is the only thing a head must provide:

| Member | Purpose |
|--------|---------|
| `IsMobile` | hides the mouse pointer, enables the on-screen pad, stops the window resizing |
| `DataDirectory` | writable folder for `highscores.json` |
| `PlatformName` | label on the title screen ("MACOS", "IPHONE / IPAD", ...) |
| `TouchSpace` | size of the space the platform reports touches in, or zero to use TouchPanel |
| `TouchSource` | platform touch feed, when the platform has a better one than TouchPanel |
| `Tilt`, `TiltY` | -1..1 device roll and pitch for Hopper and Light Cycle |
| `Quit`, `SetOrientation` | leave the app where allowed; lock landscape |

`DesktopPlatformServices` is the default implementation; iOS subclasses it to add CoreMotion tilt
(calibrated against the pose the phone is first held in) and the touch overlay.

## Persistence

`ScoreService` wraps `HighScoreBook` (Engine/HighScores) with an `IScoreStore`: `JsonScoreStore`
writes `highscores.json` in `DataDirectory`, `InMemoryScoreStore` is the fallback when the folder
is not writable. The file is written after every change. A file that will not parse is set aside
rather than discarded, and the reason surfaces as `Warning` for the high-score screen to show.

## Audio

`Synth` generates waveforms (squares, noise, envelopes) and `SoundBank` owns the resulting MonoGame
`SoundEffect`s keyed by `SoundId`. Games queue sound ids; they never touch the audio device, which
is what lets the engine be tested with no graphics or audio at all.

## Adding a game

1. Write `MyGame : GameBase` in `Engine/Games`, implementing `OnStart`, `UpdateCore`,
   `PlayfieldSize`, `Entities`.
2. Add a `GameId` and a `GameInfo` to `Catalogue`.
3. Teach `EntityRenderer` any new `EntityKind`, and `SoundBank` any new `SoundId`.
4. Add rule tests to `PSSCollection.Tests` — the existing per-game test files show the pattern of
   driving `Update` with a scripted `InputState` and a seeded `XorShiftRandom`.
