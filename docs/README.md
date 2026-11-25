# Documentation

The [README](../README.md) is the tour. These pages go deeper.

| Page | What it covers |
|------|----------------|
| [architecture.md](architecture.md) | How the projects fit together: the 1280x720 virtual canvas, the screen model, the engine's game loop, input, platform services, persistence and audio. Where to add things. |
| [games.md](games.md) | The five games as implemented: board sizes, scoring, entity behaviour, and the skill table that tunes all of them. |
| [building.md](building.md) | Building and running each head, the release pipeline, code signing and profiles, and how the store screenshots and artwork are produced. |

Two things are deliberately not documented here because they are not in the repository: the
release artefacts (`Releases/`) and the store submission material (`Stores/`). Both are generated
by the tools described in [building.md](building.md) and are ignored by git.

## Conventions worth knowing before reading the code

* Nullable reference types and implicit usings are off across the solution (see
  `Directory.Build.props`), so every `using` is explicit and every reference type is written as it
  was in C# 6. Match that style in new code.
* The engine (`PSSCollection.Engine`) has no MonoGame reference at all. It deals in plain
  `System.Numerics.Vector2`, its own `Entity` structs and `SoundId` values.
* Everything is drawn from primitives and every sound is synthesised; there is no content
  pipeline and no binary assets beyond the app icons and launch art.
