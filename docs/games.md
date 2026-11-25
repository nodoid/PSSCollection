# The five games

All five are Personal Software Services releases for the Oric-1 from 1983, reimplemented from their
rules. Everything below is taken from the constants in `PSSCollection.Engine/Games`.

## Skill levels

`SkillLevels` (Engine/SkillLevel.cs) tunes every game at once, so the four levels mean the same
thing whichever game you pick.

| Skill | Enemy speed | Enemy fire | Score | Lives | Extra enemies |
|-------|-------------|------------|-------|-------|---------------|
| Novice | x0.75 | x0.55 | x1 | 4 | 0 |
| Skilled | x1.0 | x1.0 | x1.25 | 3 | 1 |
| Veteran | x1.3 | x1.45 | x1.5 | 3 | 2 |
| Ace | x1.6 | x2.0 | x2.0 | 3 | 3 |

Common to all games (`GameBase`): an extra life every **10,000** points, the score is multiplied by
the skill multiplier and rounded away from zero (minimum 1 point), and a wave clear awards a bonus
before the level counter advances.

## Invaders

A 1000x700 playfield. Eleven columns of aliens, up to six rows deep, starting at y=140. The
formation steps 10px sideways and drops 24px whenever it touches an edge; it speeds up as rows are
cleared. Four bunkers of 8x3 destructible blocks at y=544 absorb both your shots and their bombs; a
mystery ship crosses the band above the formation for 50 to 300 points depending on how early you
hit it. Your ship moves at 430 px/s with a 0.26s fire cooldown. Five rows start a wave; later
waves are deeper and faster.

## Centipede

A 40x30 grid of 20px cells (800x600) with the player confined to the bottom six rows. The centipede
starts twelve segments long and a wave starts with 26 mushrooms; a mushroom takes four hits. Hit the
head and the body behind it continues as a new centipede. Scores: head 100, body 10, mushroom 1,
flea 200. A bouncing spider crosses the player's band, and the flea drops new mushrooms in the
column it falls through to repair the field. Fire cooldown 0.22s.

## Hopper

A 20x10 grid of 56px cells (1120x560), which is one sheet of five frogs:

| Row | Contents |
|-----|----------|
| 0 | Five home bays at columns 1, 5, 9, 13, 17 |
| 1-4 | River: logs and diving turtles |
| 5 | Median |
| 6-8 | Three traffic lanes |
| 9 | Start bank |

One hop per input with a 0.16s cooldown, 1.1s to respawn after a death. A **single TIME budget of 99
units (0.79s each) covers the whole sheet**, not one frog, which is the original's stingiest rule.
Scoring: 10 per forward hop, 100 per home bay, and 800-1100 for the fly that appears for 7 seconds in
an empty bay. A patrol snake crosses the median at 78 px/s. On a phone the frog hops by tilting
(`TILT TO HOP`).

## Elektro Storm

A 1120x616 board with six cities along the ground line at y=520 and three batteries between them,
each holding ten missiles — thirty a wave, plus five awarded per wave cleared. Enemy missiles fall
towards a city or a battery; you move a crosshair at 430 px/s, fire counter-missiles at 720 px/s,
and detonate them for a 74px blast lasting 0.78s. A missile is 25 points, a surviving city 100. A
wave ends when every incoming missile is gone, and the next falls faster.

## Light Cycle

A 56x36 grid of 17px cells (952x612). You can only turn, never reverse, and any trail — including
your own — ends the run. Rounds start with a 1.4s countdown, award 250 per rival eliminated and a
1000 round bonus for surviving, then restart after 1.6s with up to five rivals and a faster step.
Turning is by pad on a desktop and by tilt on a phone (`TILT TO TURN`).

## Presentation

The engine never draws anything: it emits `Entity` values that `EntityRenderer` (Core/Graphics) turns
into rectangles, lines and simple sprites using the palette in `Palette.cs`. Sound is queued as
`SoundId` values and synthesised by `Synth`/`SoundBank`. That split is why the entire rule set can be
unit tested with no window, no graphics device and no audio device.
