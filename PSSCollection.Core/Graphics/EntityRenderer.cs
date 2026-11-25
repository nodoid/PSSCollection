using System;
using Microsoft.Xna.Framework;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;

namespace PSSCollection.Core.Graphics
{
    /// <summary>Maps a game's playfield coordinates onto a rectangle of the virtual canvas.</summary>
    public readonly struct PlayfieldView
    {
        public PlayfieldView(Rectangle area, System.Numerics.Vector2 playfield)
        {
            Area = area;
            Playfield = playfield;
        }

        public Rectangle Area { get; }

        public System.Numerics.Vector2 Playfield { get; }

        public float Scale => Math.Min(Area.Width / Playfield.X, Area.Height / Playfield.Y);

        public Vector2 Offset => new Vector2(
            Area.X + (Area.Width - Playfield.X * Scale) * 0.5f,
            Area.Y + (Area.Height - Playfield.Y * Scale) * 0.5f);

        public Vector2 ToScreen(System.Numerics.Vector2 point) => Offset + new Vector2(point.X, point.Y) * Scale;

        public Vector2 Size(System.Numerics.Vector2 size) => new Vector2(size.X, size.Y) * Scale;

        public float S(float value) => value * Scale;

        public Vector2 Centre(System.Numerics.Vector2 position, System.Numerics.Vector2 size) =>
            Offset + new Vector2(position.X + size.X * 0.5f, position.Y + size.Y * 0.5f) * Scale;
    }

    /// <summary>Turns the engine's entity list into drawn shapes.</summary>
    public static class EntityRenderer
    {
        public static void Draw(ScreenCanvas canvas, PlayfieldView view, in Entity entity, GameId game, double time)
        {
            if (!entity.Visible)
                return;

            Vector2 centre = view.Centre(entity.Position, entity.Size);
            float width = Math.Max(2f, view.S(entity.Size.X));
            float height = Math.Max(2f, view.S(entity.Size.Y));

            switch (entity.Kind)
            {
                case EntityKind.Player:
                    if (game == GameId.Hopper)
                        DrawFrog(canvas, centre, width, height, time);
                    else if (game == GameId.LightCycle)
                        DrawCycle(canvas, centre, width, entity.Rotation, Palette.Cyan);
                    else
                        DrawCraft(canvas, centre, width, height, entity.Variant, game);
                    break;

                case EntityKind.Enemy:
                    if (game == GameId.LightCycle)
                        DrawCycle(canvas, centre, width, entity.Rotation, TrailColour(entity.Variant));
                    else if (game == GameId.Hopper)
                        DrawCar(canvas, centre, width, height, entity.Variant);
                    else if (game == GameId.ElektroStorm)
                        DrawIncomingMissile(canvas, centre, entity.Rotation);
                    else
                        DrawEnemy(canvas, centre, width, height, entity.Variant, Palette.Magenta);
                    break;

                case EntityKind.Log:
                    DrawLog(canvas, centre, width, height);
                    break;

                case EntityKind.Turtle:
                    DrawTurtles(canvas, centre, width, height, entity.Variant == 2);
                    break;

                case EntityKind.Home:
                    DrawHome(canvas, centre, width, height, entity.Variant == 1, time);
                    break;

                case EntityKind.Snake:
                    DrawSnake(canvas, centre, width, height, entity.Variant == 1);
                    break;

                case EntityKind.Fly:
                    DrawFly(canvas, centre, width, height, time);
                    break;

                case EntityKind.City:
                    DrawCity(canvas, centre, width, height, entity.Variant == 1);
                    break;

                case EntityKind.Silo:
                    DrawSilo(canvas, centre, width, height, entity.Variant);
                    break;

                case EntityKind.Crosshair:
                    DrawCrosshair(canvas, centre, width);
                    break;

                case EntityKind.Trail:
                    DrawTrail(canvas, centre, width, TrailColour(entity.Variant));
                    break;

                case EntityKind.Alien:
                    DrawAlien(canvas, centre, width, height, entity.Variant);
                    break;

                case EntityKind.Bullet:
                    canvas.Rect(centre.X - width * 0.5f, centre.Y - height * 0.5f, width, height, Palette.Cyan);
                    canvas.Glow(centre, width * 2.2f, Palette.WithAlpha(Palette.Cyan, 0.5f));
                    break;

                case EntityKind.Bomb:
                case EntityKind.BossBomb:
                    DrawBomb(canvas, centre, width, height, entity.Kind == EntityKind.BossBomb);
                    break;

                case EntityKind.Shield:
                    canvas.RoundedRect(new Rectangle((int)(centre.X - width * 0.5f), (int)(centre.Y - height * 0.5f),
                        (int)Math.Max(2, width), (int)Math.Max(2, height)), Palette.Green, 2);
                    break;

                case EntityKind.Ufo:
                    DrawUfo(canvas, centre, width, height, time);
                    break;

                case EntityKind.CentipedeHead:
                    DrawCentipede(canvas, centre, width, height, entity.Variant, true);
                    break;

                case EntityKind.CentipedeBody:
                    DrawCentipede(canvas, centre, width, height, entity.Variant, false);
                    break;

                case EntityKind.Mushroom:
                    DrawMushroom(canvas, centre, width, height, entity.Variant);
                    break;

                case EntityKind.Spider:
                    DrawSpider(canvas, centre, width, height, time);
                    break;

                case EntityKind.Flea:
                    DrawFlea(canvas, centre, width, height);
                    break;

                case EntityKind.IceBlock:
                    DrawIceBlock(canvas, centre, width, height, entity.Variant == 1);
                    break;

                case EntityKind.DiamondBlock:
                    DrawDiamond(canvas, centre, width, height, time);
                    break;

                case EntityKind.Egg:
                    DrawEgg(canvas, centre, width, height);
                    break;

                case EntityKind.Snobee:
                    DrawSnobee(canvas, centre, width, height, entity.Variant == 1);
                    break;

                case EntityKind.Star:
                    float size = Math.Max(1f, width);
                    float brightness = 0.35f + entity.Variant * 0.22f;
                    canvas.Rect(centre.X - size * 0.5f, centre.Y - size * 0.5f, size, size,
                        Palette.WithAlpha(Palette.Text, brightness));
                    break;

                case EntityKind.Explosion:
                    if (game == GameId.ElektroStorm)
                    {
                        canvas.Glow(centre, width * 0.75f, Palette.WithAlpha(Palette.Amber, 0.55f));
                        canvas.Circle(centre, width * 0.5f, Palette.WithAlpha(Palette.Amber, 0.30f));
                        canvas.CircleOutline(centre, width * 0.5f, Palette.Amber, 3f);
                        canvas.CircleOutline(centre, width * 0.34f, Palette.WithAlpha(Palette.White, 0.7f), 2f);
                    }
                    else
                    {
                        canvas.CircleOutline(centre, width * 0.6f, Palette.Amber, 3f);
                        canvas.CircleOutline(centre, width * 0.9f, Palette.WithAlpha(Palette.Magenta, 0.5f), 2f);
                    }

                    break;

                case EntityKind.PowerUp:
                    DrawPowerUp(canvas, centre, width, entity.Variant, time);
                    break;

                case EntityKind.Guardian:
                    DrawGuardian(canvas, centre, width, height, time);
                    break;
            }
        }

        public static void Background(ScreenCanvas canvas, GameId game, PlayfieldView view, double phase)
        {
            Rectangle area = view.Area;
            canvas.Rect(area, new Color(4, 6, 14));

            switch (game)
            {
                case GameId.Invaders:
                    Ui_Stars(canvas, area, phase, 40, Palette.Green);
                    canvas.Rect(area.X, area.Bottom - 3, area.Width, 3, Palette.WithAlpha(Palette.Green, 0.5f));
                    canvas.Line(new Vector2(area.X, area.Y + 8), new Vector2(area.Right, area.Y + 8),
                        Palette.WithAlpha(Palette.Cyan, 0.25f), 2f);
                    break;

                case GameId.Centipede:
                    for (int i = 0; i < 12; i++)
                    {
                        float y = area.Y + i * area.Height / 12f;
                        canvas.Rect(area.X, y, area.Width, 1, Palette.WithAlpha(Palette.Magenta, 0.05f));
                    }

                    float zoneTop = area.Y + area.Height * (CentipedeZoneTop());
                    canvas.Rect(area.X, zoneTop, area.Width, area.Bottom - zoneTop, Palette.WithAlpha(Palette.Magenta, 0.06f));
                    canvas.Rect(area.X, zoneTop, area.Width, 2, Palette.WithAlpha(Palette.Magenta, 0.28f));
                    break;

                case GameId.Hopper:
                    DrawHopperBackground(canvas, view, phase);
                    break;

                case GameId.ElektroStorm:
                    DrawStormBackground(canvas, view, phase);
                    break;

                case GameId.LightCycle:
                    canvas.Gradient(new Color(4, 6, 14), new Color(2, 3, 8));
                    for (int i = 0; i <= 56; i += 2)
                    {
                        float x = area.X + i * area.Width / 56f;
                        canvas.Rect(x, area.Y, 1, area.Height, Palette.WithAlpha(Palette.Cyan, 0.05f));
                    }

                    for (int i = 0; i <= 36; i += 2)
                    {
                        float y = area.Y + i * area.Height / 36f;
                        canvas.Rect(area.X, y, area.Width, 1, Palette.WithAlpha(Palette.Cyan, 0.05f));
                    }

                    float pulse = 0.25f + 0.1f * (float)Math.Sin(phase * 2.0);
                    canvas.RectOutline(area, Palette.WithAlpha(Palette.Amber, pulse), 2);
                    break;

                default:
                    Ui_Stars(canvas, area, phase * 1.6, 60, Palette.Cyan);
                    canvas.Glow(new Vector2(area.X + area.Width * 0.28f, area.Y + area.Height * 0.3f),
                        area.Width * 0.25f, Palette.WithAlpha(Palette.Violet, 0.22f));
                    canvas.Glow(new Vector2(area.X + area.Width * 0.72f, area.Y + area.Height * 0.55f),
                        area.Width * 0.2f, Palette.WithAlpha(Palette.Cyan, 0.18f));
                    break;
            }

            canvas.RectOutline(area, Palette.WithAlpha(Palette.PanelEdge, 0.7f), 2);
        }

        private static float CentipedeZoneTop() => Engine.Games.CentipedeGame.PlayerZoneTopRow / (float)Engine.Games.CentipedeGame.Rows;

        private static void Ui_Stars(ScreenCanvas canvas, Rectangle area, double phase, int count, Color tint)
        {
            for (int i = 0; i < count; i++)
            {
                float x = area.X + (i * 197.3f) % area.Width;
                float y = area.Y + (float)((i * 97.7 + phase * (14 + i % 6 * 7)) % area.Height);
                float size = i % 9 == 0 ? 3f : i % 4 == 0 ? 2f : 1.5f;
                canvas.Rect(x, y, size, size, Palette.WithAlpha(i % 7 == 0 ? tint : Palette.Text, 0.14f + (i % 5) * 0.05f));
            }
        }

        private static void DrawHopperBackground(ScreenCanvas canvas, PlayfieldView view, double phase)
        {
            const float cell = Engine.Games.HopperGame.Cell;
            Vector2 offset = view.Offset;
            float width = view.S(Engine.Games.HopperGame.Width);
            float band = view.S(cell);
            float RowY(int row) => offset.Y + view.S(row * cell);

            canvas.Rect(offset.X, RowY(0), width, band, new Color(16, 44, 26));           // hedge
            canvas.Rect(offset.X, RowY(1), width, band * 4, new Color(8, 26, 54));        // river
            canvas.Rect(offset.X, RowY(5), width, band, new Color(22, 46, 24));           // snake bank
            canvas.Rect(offset.X, RowY(6), width, band * 3, new Color(20, 20, 26));       // road
            canvas.Rect(offset.X, RowY(9), width, band, new Color(28, 56, 28));           // start bank

            for (int row = 1; row <= 4; row++)
            {
                float y = RowY(row) + band * 0.5f;
                float wiggle = (float)Math.Sin(phase * 2.0 + row) * band * 0.12f;
                canvas.Line(new Vector2(offset.X, y + wiggle), new Vector2(offset.X + width, y - wiggle),
                    Palette.WithAlpha(Palette.Cyan, 0.10f), 2f);
            }

            for (int row = 7; row <= 8; row++)
            {
                float y = RowY(row);
                for (float x = offset.X; x < offset.X + width; x += view.S(cell) * 0.5f)
                    canvas.Rect(x, y, view.S(cell) * 0.25f, 2, Palette.WithAlpha(Palette.Amber, 0.25f));
            }

            canvas.Rect(offset.X, RowY(9) - 2, width, 2, Palette.WithAlpha(Palette.Ice, 0.18f));
            canvas.Rect(offset.X, RowY(6) - 2, width, 2, Palette.WithAlpha(Palette.Ice, 0.12f));
        }

        private static void DrawStormBackground(ScreenCanvas canvas, PlayfieldView view, double phase)
        {
            float width = view.S(Engine.Games.ElektroStormGame.Width);
            float ground = view.Offset.Y + view.S(Engine.Games.ElektroStormGame.GroundY);
            float bottom = view.Offset.Y + view.S(Engine.Games.ElektroStormGame.Height);

            canvas.Rect(view.Offset.X, view.Offset.Y, width, bottom - view.Offset.Y, new Color(6, 8, 18));
            canvas.Rect(view.Offset.X, ground, width, bottom - ground, new Color(22, 20, 30));
            canvas.Rect(view.Offset.X, ground, width, 2, Palette.WithAlpha(Palette.Cyan, 0.5f));

            for (int i = 0; i < 90; i++)
            {
                float x = view.Offset.X + (float)((i * 137.5) % width);
                float y = view.Offset.Y + (float)((i * 61.7) % Math.Max(1f, ground - view.Offset.Y));
                float twinkle = 0.15f + 0.2f * (float)Math.Abs(Math.Sin(phase * 0.7 + i));
                canvas.Rect(x, y, 2, 2, Palette.WithAlpha(Palette.Text, twinkle));
            }
        }

        /// <summary>
        /// A frog seen from above: fat body, two bulging eyes, front arms and long back legs that
        /// kick through a hop cycle.
        /// </summary>
        private static void DrawFrog(ScreenCanvas canvas, Vector2 centre, float width, float height, double time)
        {
            Color skin = new Color(122, 222, 106);
            Color shade = new Color(64, 156, 70);
            Color pale = new Color(190, 240, 150);

            float w = width * 0.5f;
            float h = height * 0.5f;

            // One hop cycle: legs croak out, body lifts a little.
            float phase = (float)((time * 1.6) % 1.0);
            float kick = MathF.Sin(phase * MathF.PI * 2f);
            Vector2 lift = new Vector2(0, -h * 0.10f * MathF.Max(0f, kick));
            Vector2 c = centre + lift;

            canvas.Glow(centre, w * 2.2f, Palette.WithAlpha(Palette.Green, 0.22f));

            // Back legs: thigh out, knee up, long foot trailing.
            float reach = w * (0.70f + 0.18f * kick);
            float kneeY = h * (0.28f - 0.16f * kick);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 hip = c + new Vector2(side * w * 0.34f, h * 0.10f);
                Vector2 knee = c + new Vector2(side * reach, kneeY);
                Vector2 foot = c + new Vector2(side * w * 1.02f, h * (0.62f + 0.12f * kick));

                canvas.Line(hip, knee, skin, 4.5f);
                canvas.Line(knee, foot, skin, 3.5f);
                canvas.Circle(knee, w * 0.11f, skin);
            }

            // Front arms, reaching forward and slightly out.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 shoulder = c + new Vector2(side * w * 0.30f, -h * 0.34f);
                Vector2 hand = c + new Vector2(side * w * (0.52f + 0.06f * kick), -h * (0.62f - 0.05f * kick));
                canvas.Line(shoulder, hand, skin, 3.5f);
                canvas.Circle(hand, w * 0.07f, skin);
            }

            // Body: wide in the middle, tapering into the snout.
            canvas.Circle(c + new Vector2(0, h * 0.16f), w * 0.60f, skin);
            canvas.Circle(c + new Vector2(0, -h * 0.18f), w * 0.44f, skin);
            canvas.Rect(c.X - w * 0.58f, c.Y - h * 0.22f, w * 1.16f, h * 0.42f, skin);

            // Pale spine stripe, which reads at small sizes.
            canvas.Rect(c.X - w * 0.10f, c.Y - h * 0.30f, w * 0.20f, h * 0.52f, pale);

            // Head: snout and a wide mouth line.
            canvas.Circle(c + new Vector2(0, -h * 0.52f), w * 0.30f, skin);
            canvas.Line(c + new Vector2(-w * 0.34f, -h * 0.40f), c + new Vector2(w * 0.34f, -h * 0.40f), shade, 2.5f);

            // Eyes: two domes sitting proud of the head, with dark pupils.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 eye = c + new Vector2(side * w * 0.30f, -h * 0.62f);
                canvas.Circle(eye, w * 0.19f, skin);
                canvas.Circle(eye, w * 0.12f, Palette.White);
                canvas.Circle(eye + new Vector2(0, h * 0.02f), w * 0.06f, new Color(20, 30, 20));
            }
        }

        private static void DrawCar(ScreenCanvas canvas, Vector2 centre, float width, float height, int variant)
        {
            Color body = variant switch
            {
                0 => new Color(230, 120, 70),
                1 => new Color(110, 200, 240),
                _ => new Color(230, 200, 90)
            };

            float w = width * 0.92f;
            float h = height * 0.62f;
            canvas.RoundedRect(new Rectangle((int)(centre.X - w * 0.5f), (int)(centre.Y - h * 0.5f),
                (int)w, (int)h), body, 5);
            canvas.Rect(centre.X - w * 0.22f, centre.Y - h * 0.42f, w * 0.44f, h * 0.42f,
                Palette.WithAlpha(Palette.Background, 0.55f));
            canvas.Circle(centre - new Vector2(w * 0.3f, h * 0.5f), h * 0.2f, new Color(20, 20, 24));
            canvas.Circle(centre + new Vector2(w * 0.3f, h * 0.5f), h * 0.2f, new Color(20, 20, 24));
        }

        private static void DrawLog(ScreenCanvas canvas, Vector2 centre, float width, float height)
        {
            float w = width * 0.96f;
            float h = height * 0.62f;
            var rect = new Rectangle((int)(centre.X - w * 0.5f), (int)(centre.Y - h * 0.5f), (int)w, (int)h);
            canvas.RoundedRect(rect, new Color(120, 82, 48), 6);
            canvas.RoundedOutline(rect, new Color(78, 52, 30), 6, 2);

            for (int i = 1; i < 5; i++)
            {
                float x = rect.X + rect.Width * i / 5f;
                canvas.Rect(x, rect.Y + 2, 1, rect.Height - 4, new Color(96, 64, 36));
            }

            canvas.Circle(new Vector2(rect.X + 6, centre.Y), h * 0.28f, new Color(146, 104, 62));
            canvas.Circle(new Vector2(rect.Right - 6, centre.Y), h * 0.28f, new Color(146, 104, 62));
        }

        private static void DrawTurtles(ScreenCanvas canvas, Vector2 centre, float width, float height, bool diving)
        {
            int count = width > 70 ? 4 : 2;
            float size = Math.Min(width / count * 0.8f, height * 0.8f);
            Color shell = diving ? Palette.WithAlpha(new Color(60, 120, 110), 0.35f) : new Color(70, 170, 130);
            Color rim = diving ? Palette.WithAlpha(Palette.Cyan, 0.25f) : new Color(190, 240, 180);

            for (int i = 0; i < count; i++)
            {
                float x = centre.X - width * 0.5f + width * (i + 0.5f) / count;
                canvas.Circle(new Vector2(x, centre.Y), size * 0.42f, shell);
                canvas.CircleOutline(new Vector2(x, centre.Y), size * 0.42f, rim, 2f);
                canvas.Circle(new Vector2(x, centre.Y - size * 0.5f), size * 0.14f, shell);
            }

            if (diving)
            {
                canvas.Line(new Vector2(centre.X - width * 0.5f, centre.Y + height * 0.3f),
                    new Vector2(centre.X + width * 0.5f, centre.Y + height * 0.3f),
                    Palette.WithAlpha(Palette.Cyan, 0.25f), 2f);
            }
        }

        private static void DrawHome(ScreenCanvas canvas, Vector2 centre, float width, float height, bool filled, double time)
        {
            float w = width * 0.8f;
            float h = height * 0.7f;
            var rect = new Rectangle((int)(centre.X - w * 0.5f), (int)(centre.Y - h * 0.5f), (int)w, (int)h);

            if (filled)
            {
                canvas.RoundedRect(rect, new Color(30, 90, 50), 8);
                canvas.RoundedOutline(rect, Palette.Green, 8, 2);
                DrawFrog(canvas, centre, w * 0.7f, h * 0.7f, time);
            }
            else
            {
                canvas.RoundedRect(rect, new Color(8, 16, 12), 8);
                canvas.RoundedOutline(rect, Palette.WithAlpha(Palette.Green, 0.45f), 8, 2);
            }
        }

        private static void DrawSnake(ScreenCanvas canvas, Vector2 centre, float width, float height, bool facingLeft)
        {
            Color body = new Color(200, 210, 90);
            float step = width / 6f;
            Vector2 previous = new Vector2(centre.X - width * 0.5f, centre.Y);

            for (int i = 1; i <= 6; i++)
            {
                float x = centre.X - width * 0.5f + step * i;
                float y = centre.Y + (float)Math.Sin(i * 1.1) * height * 0.3f;
                var next = new Vector2(x, y);
                canvas.Line(previous, next, body, Math.Max(2f, height * 0.18f));
                previous = next;
            }

            float headX = facingLeft ? centre.X - width * 0.5f : centre.X + width * 0.5f;
            canvas.Circle(new Vector2(headX, previous.Y), Math.Max(2f, height * 0.16f), body);
            canvas.Circle(new Vector2(headX + (facingLeft ? -2f : 2f), previous.Y - 1), 1.5f, Palette.Red);
        }

        private static void DrawFly(ScreenCanvas canvas, Vector2 centre, float width, float height, double time)
        {
            float flap = 0.5f + 0.5f * (float)Math.Sin(time * 22.0);
            canvas.Circle(centre, Math.Max(3f, width * 0.22f), new Color(30, 30, 36));
            canvas.Circle(centre - new Vector2(width * 0.22f, height * 0.18f), Math.Max(2f, width * 0.14f),
                Palette.WithAlpha(Palette.White, 0.55f + 0.3f * flap));
            canvas.Circle(centre + new Vector2(width * 0.22f, height * 0.18f), Math.Max(2f, width * 0.14f),
                Palette.WithAlpha(Palette.White, 0.55f + 0.3f * (1f - flap)));
        }

        private static void DrawCity(ScreenCanvas canvas, Vector2 centre, float width, float height, bool rubble)
        {
            Color wall = rubble ? new Color(70, 66, 70) : new Color(120, 190, 230);
            Color window = rubble ? new Color(30, 28, 32) : new Color(240, 230, 150);
            float unit = width / 6f;

            for (int i = 0; i < 5; i++)
            {
                float towerHeight = rubble
                    ? height * (0.25f + 0.1f * (i % 3))
                    : height * (0.45f + 0.13f * ((i * 7) % 4));
                float x = centre.X - width * 0.5f + unit * i + 1;
                float y = centre.Y + height * 0.4f - towerHeight;
                canvas.Rect(x, y, unit - 2, towerHeight, wall);

                if (!rubble)
                {
                    for (int w = 0; w < 3; w++)
                    {
                        if ((i + w) % 2 == 0)
                            canvas.Rect(x + 2, y + 4 + w * (unit * 0.5f), Math.Max(1f, unit * 0.35f),
                                Math.Max(1f, unit * 0.2f), window);
                    }
                }
            }

            canvas.Rect(centre.X - width * 0.5f, centre.Y + height * 0.4f, width, Math.Max(2f, height * 0.12f), wall);
        }

        private static void DrawSilo(ScreenCanvas canvas, Vector2 centre, float width, float height, int ammo)
        {
            bool destroyed = ammo <= 0;
            Color body = destroyed ? new Color(70, 60, 60) : Palette.Cyan;
            float w = width * 0.8f;
            float h = height * 0.6f;
            var baseRect = new Rectangle((int)(centre.X - w * 0.5f), (int)(centre.Y - h * 0.2f), (int)w, (int)h);

            canvas.RoundedRect(baseRect, Palette.WithAlpha(body, 0.25f), 4);
            canvas.RoundedOutline(baseRect, body, 4, 2);

            if (!destroyed)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = centre.X - w * 0.28f + i * w * 0.28f;
                    canvas.Line(new Vector2(x, baseRect.Y), new Vector2(x, baseRect.Y - h * 0.7f), body, 3f);
                    canvas.Circle(new Vector2(x, baseRect.Y - h * 0.7f), 2f, Palette.White);
                }
            }
        }

        private static void DrawCrosshair(ScreenCanvas canvas, Vector2 centre, float width)
        {
            float arm = width * 0.5f;
            Color colour = Palette.WithAlpha(Palette.Green, 0.85f);
            canvas.Line(centre - new Vector2(arm, 0), centre - new Vector2(arm * 0.35f, 0), colour, 2f);
            canvas.Line(centre + new Vector2(arm * 0.35f, 0), centre + new Vector2(arm, 0), colour, 2f);
            canvas.Line(centre - new Vector2(0, arm), centre - new Vector2(0, arm * 0.35f), colour, 2f);
            canvas.Line(centre + new Vector2(0, arm * 0.35f), centre + new Vector2(0, arm), colour, 2f);
            canvas.CircleOutline(centre, arm * 0.45f, Palette.WithAlpha(colour, 0.6f), 1f);
        }

        private static void DrawIncomingMissile(ScreenCanvas canvas, Vector2 centre, float rotation)
        {
            Vector2 forward = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));
            Vector2 tail = centre - forward * 26f;
            canvas.Line(tail, centre, Palette.WithAlpha(Palette.Red, 0.65f), 2f);
            canvas.Circle(centre, 3.4f, Palette.Red);
            canvas.Glow(centre, 12f, Palette.WithAlpha(Palette.Red, 0.5f));
        }

        /// <summary>Trail colour by owner: 0 is the player, 1 and up are the rivals.</summary>
        private static Color TrailColour(int variant)
        {
            switch (variant)
            {
                case 0: return Palette.Cyan;
                case 1: return Palette.Magenta;
                case 2: return Palette.Amber;
                case 3: return Palette.Violet;
                case 4: return Palette.Green;
                default: return Palette.Red;
            }
        }

        private static void DrawTrail(ScreenCanvas canvas, Vector2 centre, float width, Color colour)
        {
            float size = Math.Max(2f, width * 0.92f);
            canvas.Rect(centre.X - size * 0.5f, centre.Y - size * 0.5f, size, size,
                Palette.WithAlpha(colour, 0.85f));
            canvas.Glow(centre, size * 0.9f, Palette.WithAlpha(colour, 0.35f));
        }

        private static void DrawCycle(ScreenCanvas canvas, Vector2 centre, float width, float rotation, Color colour)
        {
            float size = Math.Max(4f, width * 0.9f);
            Vector2 forward = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));
            Vector2 side = new Vector2(-forward.Y, forward.X);

            // Two light walls trailing from the rear of the cycle.
            canvas.Line(centre - forward * size * 0.5f, centre - forward * size * 0.5f + side * size * 0.5f, colour, 3f);
            canvas.Line(centre - forward * size * 0.5f, centre - forward * size * 0.5f - side * size * 0.5f, colour, 3f);
            canvas.Line(centre + forward * size * 0.35f, centre - forward * size * 0.5f + side * size * 0.5f, colour, 2f);
            canvas.Line(centre + forward * size * 0.35f, centre - forward * size * 0.5f - side * size * 0.5f, colour, 2f);

            canvas.Circle(centre, size * 0.26f, colour);
            canvas.Circle(centre + forward * size * 0.08f, size * 0.10f, Palette.White);
            canvas.Glow(centre, size * 0.9f, Palette.WithAlpha(colour, 0.45f));
        }

        private static void DrawCraft(ScreenCanvas canvas, Vector2 centre, float width, float height, int variant, GameId game)
        {
            Color body = variant == 1 ? Palette.Cyan : Palette.Text;
            Color accent = game == GameId.Invaders ? Palette.Green : Palette.Cyan;

            canvas.Glow(centre + new Vector2(0, height * 0.35f), width * 0.8f, Palette.WithAlpha(accent, 0.35f));

            // Hull: a pointed nose with two swept wings.
            canvas.Circle(centre, width * 0.30f, body);
            canvas.Rect(centre.X - width * 0.30f, centre.Y, width * 0.6f, height * 0.30f, body);
            canvas.Line(new Vector2(centre.X - width * 0.48f, centre.Y + height * 0.34f),
                new Vector2(centre.X + width * 0.48f, centre.Y + height * 0.34f), accent, 3.5f);
            canvas.Line(centre, new Vector2(centre.X - width * 0.48f, centre.Y + height * 0.34f), body, 2.5f);
            canvas.Line(centre, new Vector2(centre.X + width * 0.48f, centre.Y + height * 0.34f), body, 2.5f);

            canvas.Rect(centre.X - width * 0.06f, centre.Y - height * 0.18f, width * 0.12f, height * 0.14f, accent);
            canvas.Glow(centre + new Vector2(0, height * 0.42f), width * 0.3f, Palette.WithAlpha(Palette.Amber, 0.6f));
        }

        private static void DrawEnemy(ScreenCanvas canvas, Vector2 centre, float width, float height, int variant, Color colour)
        {
            Color hull = variant switch
            {
                1 => Palette.Amber,
                2 => Palette.Violet,
                _ => colour
            };

            canvas.Line(new Vector2(centre.X - width * 0.45f, centre.Y), new Vector2(centre.X, centre.Y - height * 0.45f), hull, 2.5f);
            canvas.Line(new Vector2(centre.X + width * 0.45f, centre.Y), new Vector2(centre.X, centre.Y - height * 0.45f), hull, 2.5f);
            canvas.Circle(centre, width * 0.30f, hull);
            canvas.Circle(centre + new Vector2(0, height * 0.08f), width * 0.14f, Palette.Background);
            canvas.Rect(centre.X - width * 0.10f, centre.Y + height * 0.28f, width * 0.20f, height * 0.16f,
                Palette.WithAlpha(Palette.Amber, 0.8f));
        }

        private static void DrawAlien(ScreenCanvas canvas, Vector2 centre, float width, float height, int variant)
        {
            // Low two bits pick the alien, bit two is the marching frame.
            int type = variant & 3;
            bool marching = (variant & 4) != 0;

            Color colour = type switch
            {
                0 => Palette.Green,
                1 => Palette.Cyan,
                _ => Palette.Amber
            };

            // Legs swing between the two poses so the formation looks alive as it steps.
            float legTop = centre.Y + height * (marching ? 0.24f : 0.32f);
            canvas.Line(new Vector2(centre.X - width * 0.20f, legTop), new Vector2(centre.X - width * 0.36f, legTop),
                colour, 3f);
            canvas.Line(new Vector2(centre.X + width * 0.20f, legTop), new Vector2(centre.X + width * 0.36f, legTop),
                colour, 3f);

            switch (type)
            {
                case 0:
                    canvas.Circle(centre, width * 0.30f, colour);
                    canvas.Rect(centre.X - width * 0.42f, centre.Y - height * 0.05f, width * 0.84f, height * 0.18f, colour);
                    canvas.Rect(centre.X - width * 0.16f, centre.Y - height * 0.16f, width * 0.10f, height * 0.10f, Palette.Background);
                    canvas.Rect(centre.X + width * 0.06f, centre.Y - height * 0.16f, width * 0.10f, height * 0.10f, Palette.Background);
                    canvas.Rect(centre.X - width * 0.30f, centre.Y + height * 0.30f, width * 0.12f, height * 0.14f, colour);
                    canvas.Rect(centre.X + width * 0.18f, centre.Y + height * 0.30f, width * 0.12f, height * 0.14f, colour);
                    break;

                case 1:
                    canvas.Line(new Vector2(centre.X - width * 0.34f, centre.Y + height * 0.34f),
                        new Vector2(centre.X, centre.Y - height * 0.42f), colour, 4f);
                    canvas.Line(new Vector2(centre.X + width * 0.34f, centre.Y + height * 0.34f),
                        new Vector2(centre.X, centre.Y - height * 0.42f), colour, 4f);
                    canvas.Line(new Vector2(centre.X - width * 0.34f, centre.Y + height * 0.34f),
                        new Vector2(centre.X + width * 0.34f, centre.Y + height * 0.34f), colour, 4f);
                    canvas.Circle(centre, width * 0.14f, Palette.Background);
                    break;

                default:
                    canvas.RoundedRect(new Rectangle((int)(centre.X - width * 0.42f), (int)(centre.Y - height * 0.30f),
                        (int)(width * 0.84f), (int)(height * 0.60f)), colour, 6);
                    canvas.Rect(centre.X - width * 0.44f, centre.Y - height * 0.05f, width * 0.88f, height * 0.14f, Palette.Background);
                    canvas.Rect(centre.X - width * 0.20f, centre.Y + height * 0.34f, width * 0.12f, height * 0.12f, colour);
                    canvas.Rect(centre.X + width * 0.08f, centre.Y + height * 0.34f, width * 0.12f, height * 0.12f, colour);
                    break;
            }
        }

        private static void DrawBomb(ScreenCanvas canvas, Vector2 centre, float width, float height, bool big)
        {
            Color colour = big ? Palette.Red : Palette.Amber;
            canvas.Rect(centre.X - width * 0.5f, centre.Y - height * 0.5f, width, height, colour);
            canvas.Glow(centre, width * 2.4f, Palette.WithAlpha(colour, 0.4f));
        }

        private static void DrawUfo(ScreenCanvas canvas, Vector2 centre, float width, float height, double time)
        {
            canvas.Circle(new Vector2(centre.X, centre.Y + height * 0.10f), width * 0.46f, Palette.Amber);
            canvas.Circle(centre + new Vector2(0, -height * 0.14f), width * 0.22f, Palette.Cyan);
            for (int i = -2; i <= 2; i++)
            {
                float blink = 0.4f + 0.6f * (float)((Math.Sin(time * 8 + i) + 1) * 0.5);
                canvas.Circle(new Vector2(centre.X + i * width * 0.16f, centre.Y + height * 0.20f), width * 0.05f,
                    Palette.WithAlpha(Palette.Magenta, blink));
            }

            canvas.Glow(centre, width * 0.9f, Palette.WithAlpha(Palette.Amber, 0.25f));
        }

        private static void DrawCentipede(ScreenCanvas canvas, Vector2 centre, float width, float height, int variant, bool head)
        {
            Color body = variant % 2 == 0 ? Palette.Magenta : Palette.Violet;
            float radius = Math.Min(width, height) * 0.42f;

            canvas.Circle(centre, radius, body);
            canvas.Circle(centre, radius * 0.55f, Palette.WithAlpha(Palette.Background, 0.5f));

            float leg = radius * 0.9f;
            canvas.Line(new Vector2(centre.X - radius, centre.Y), new Vector2(centre.X - radius - leg, centre.Y - leg * 0.5f), body, 2f);
            canvas.Line(new Vector2(centre.X - radius, centre.Y), new Vector2(centre.X - radius - leg, centre.Y + leg * 0.5f), body, 2f);
            canvas.Line(new Vector2(centre.X + radius, centre.Y), new Vector2(centre.X + radius + leg, centre.Y - leg * 0.5f), body, 2f);
            canvas.Line(new Vector2(centre.X + radius, centre.Y), new Vector2(centre.X + radius + leg, centre.Y + leg * 0.5f), body, 2f);

            if (!head)
                return;

            canvas.Circle(new Vector2(centre.X - radius * 0.35f, centre.Y - radius * 0.15f), radius * 0.2f, Palette.Amber);
            canvas.Circle(new Vector2(centre.X + radius * 0.35f, centre.Y - radius * 0.15f), radius * 0.2f, Palette.Amber);
        }

        private static void DrawMushroom(ScreenCanvas canvas, Vector2 centre, float width, float height, int health)
        {
            Color cap = health switch
            {
                4 => Palette.Magenta,
                3 => new Color(214, 96, 160),
                2 => new Color(170, 92, 150),
                _ => new Color(120, 92, 140)
            };

            canvas.Circle(new Vector2(centre.X, centre.Y - height * 0.12f), width * 0.42f, cap);
            canvas.Rect(centre.X - width * 0.16f, centre.Y + height * 0.10f, width * 0.32f, height * 0.36f,
                Palette.WithAlpha(Palette.Text, 0.85f));
            canvas.Rect(centre.X - width * 0.28f, centre.Y - height * 0.30f, width * 0.14f, height * 0.12f,
                Palette.WithAlpha(Palette.Text, 0.5f));
        }

        private static void DrawSpider(ScreenCanvas canvas, Vector2 centre, float width, float height, double time)
        {
            float radius = Math.Min(width, height) * 0.30f;
            canvas.Circle(centre, radius, Palette.Amber);
            canvas.Circle(centre + new Vector2(0, -radius * 0.4f), radius * 0.6f, Palette.Violet);

            for (int i = 0; i < 4; i++)
            {
                float offset = (i - 1.5f) * radius * 0.5f;
                float wobble = (float)Math.Sin(time * 10 + i) * radius * 0.3f;
                canvas.Line(new Vector2(centre.X + offset, centre.Y), new Vector2(centre.X + offset - radius, centre.Y + radius + wobble), Palette.Amber, 2f);
                canvas.Line(new Vector2(centre.X + offset, centre.Y), new Vector2(centre.X + offset + radius, centre.Y + radius + wobble), Palette.Amber, 2f);
            }
        }

        private static void DrawFlea(ScreenCanvas canvas, Vector2 centre, float width, float height)
        {
            canvas.RoundedRect(new Rectangle((int)(centre.X - width * 0.3f), (int)(centre.Y - height * 0.4f),
                (int)(width * 0.6f), (int)(height * 0.8f)), Palette.Red, 4);
            canvas.Rect(centre.X - width * 0.12f, centre.Y - height * 0.2f, width * 0.24f, height * 0.14f, Palette.Text);
        }

        private static void DrawIceBlock(ScreenCanvas canvas, Vector2 centre, float width, float height, bool wall)
        {
            var area = new Rectangle((int)(centre.X - width * 0.5f), (int)(centre.Y - height * 0.5f),
                (int)width, (int)height);
            Color fill = wall ? new Color(38, 58, 96) : new Color(58, 96, 152);
            Color edge = wall ? new Color(64, 92, 146) : new Color(148, 208, 255);

            canvas.RoundedRect(area, fill, 5);
            canvas.RoundedOutline(area, edge, 5, 2);
            canvas.Line(new Vector2(area.X + 6, area.Bottom - 8), new Vector2(area.Right - 8, area.Y + 6),
                Palette.WithAlpha(Palette.White, 0.18f), 2f);
        }

        private static void DrawDiamond(ScreenCanvas canvas, Vector2 centre, float width, float height, double time)
        {
            float radius = Math.Min(width, height) * 0.42f;
            int rows = Math.Max(3, (int)(radius * 2));

            for (int i = 0; i < rows; i++)
            {
                float t = i / (float)(rows - 1);
                float distance = Math.Abs(t - 0.5f) * 2;
                float halfWidth = radius * (1 - distance);
                float y = centre.Y - radius + i;
                Color colour = Color.Lerp(Palette.Cyan, Palette.White, 0.25f + 0.35f * (1 - distance));
                canvas.Rect(centre.X - halfWidth, y, halfWidth * 2, 1.2f, colour);
            }

            float shine = 0.5f + 0.5f * (float)Math.Sin(time * 4);
            canvas.Glow(centre + new Vector2(-radius * 0.2f, -radius * 0.2f), radius, Palette.WithAlpha(Palette.White, 0.18f * shine));
        }

        private static void DrawEgg(ScreenCanvas canvas, Vector2 centre, float width, float height)
        {
            canvas.Circle(centre, Math.Min(width, height) * 0.38f, new Color(244, 236, 210));
            canvas.Circle(centre + new Vector2(-width * 0.08f, -height * 0.06f), width * 0.28f, new Color(255, 250, 235));
            canvas.Circle(centre + new Vector2(width * 0.12f, height * 0.12f), width * 0.08f, new Color(200, 190, 170));
        }

        private static void DrawSnobee(ScreenCanvas canvas, Vector2 centre, float width, float height, bool stunned)
        {
            Color body = stunned ? Palette.TextDim : Palette.Magenta;
            canvas.Circle(centre, Math.Min(width, height) * 0.40f, body);
            canvas.Circle(centre + new Vector2(0, Math.Min(width, height) * 0.12f), Math.Min(width, height) * 0.24f, Palette.Background);
            canvas.Rect(centre.X - width * 0.20f, centre.Y - height * 0.10f, width * 0.10f, height * 0.12f, Palette.Text);
            canvas.Rect(centre.X + width * 0.10f, centre.Y - height * 0.10f, width * 0.10f, height * 0.12f, Palette.Text);

            if (stunned)
                canvas.CircleOutline(centre, Math.Min(width, height) * 0.48f, Palette.Amber, 2f);
        }

        private static void DrawPenguin(ScreenCanvas canvas, Vector2 centre, float width, float height, int facing)
        {
            float scale = Math.Min(width, height);

            canvas.Circle(centre, scale * 0.42f, new Color(26, 38, 66));
            canvas.Circle(centre + new Vector2(0, scale * 0.06f), scale * 0.30f, new Color(240, 246, 255));
            canvas.Circle(centre + new Vector2(0, -scale * 0.22f), scale * 0.24f, new Color(26, 38, 66));
            canvas.Rect(centre.X - scale * 0.06f, centre.Y - scale * 0.26f, scale * 0.12f, scale * 0.10f, Palette.Amber);
            canvas.Circle(centre + new Vector2(-scale * 0.09f, -scale * 0.30f), scale * 0.05f, Palette.White);
            canvas.Circle(centre + new Vector2(scale * 0.09f, -scale * 0.30f), scale * 0.05f, Palette.White);
            canvas.Circle(centre + new Vector2(-scale * 0.09f, -scale * 0.30f), scale * 0.022f, Palette.Black);
            canvas.Circle(centre + new Vector2(scale * 0.09f, -scale * 0.30f), scale * 0.022f, Palette.Black);
            canvas.Rect(centre.X - scale * 0.14f, centre.Y + scale * 0.34f, scale * 0.10f, scale * 0.10f, Palette.Amber);
            canvas.Rect(centre.X + scale * 0.04f, centre.Y + scale * 0.34f, scale * 0.10f, scale * 0.10f, Palette.Amber);
            _ = facing;
        }

        private static void DrawPowerUp(ScreenCanvas canvas, Vector2 centre, float width, int variant, double time)
        {
            Color colour = variant switch
            {
                0 => Palette.Amber,
                1 => Palette.Cyan,
                _ => Palette.Green
            };

            float radius = width * 0.5f;
            float pulse = 0.7f + 0.3f * (float)Math.Sin(time * 6);
            canvas.Circle(centre, radius, Palette.WithAlpha(colour, 0.9f));
            canvas.CircleOutline(centre, radius * 1.15f * pulse, Palette.WithAlpha(colour, 0.6f), 2f);
            canvas.Circle(centre, radius * 0.55f, Palette.Background);

            switch (variant)
            {
                case 0:
                    canvas.Rect(centre.X - radius * 0.35f, centre.Y - radius * 0.3f, radius * 0.7f, radius * 0.18f, colour);
                    canvas.Rect(centre.X - radius * 0.35f, centre.Y + radius * 0.05f, radius * 0.7f, radius * 0.18f, colour);
                    break;
                case 1:
                    canvas.Rect(centre.X - radius * 0.42f, centre.Y - radius * 0.08f, radius * 0.22f, radius * 0.5f, colour);
                    canvas.Rect(centre.X + radius * 0.20f, centre.Y - radius * 0.08f, radius * 0.22f, radius * 0.5f, colour);
                    break;
                default:
                    canvas.CircleOutline(centre, radius * 0.45f, colour, 2.5f);
                    break;
            }
        }

        private static void DrawGuardian(ScreenCanvas canvas, Vector2 centre, float width, float height, double time)
        {
            Color hull = Palette.Violet;
            canvas.Glow(centre, width * 0.6f, Palette.WithAlpha(Palette.Violet, 0.25f));

            canvas.RoundedRect(new Rectangle((int)(centre.X - width * 0.5f), (int)(centre.Y - height * 0.30f),
                (int)width, (int)(height * 0.55f)), hull, 10);
            canvas.RoundedRect(new Rectangle((int)(centre.X - width * 0.22f), (int)(centre.Y - height * 0.45f),
                (int)(width * 0.44f), (int)(height * 0.4f)), Palette.Cyan, 8);

            canvas.Circle(centre, Math.Min(width, height) * 0.16f, Palette.Amber);

            for (int i = -2; i <= 2; i++)
            {
                float blink = 0.35f + 0.65f * (float)((Math.Sin(time * 5 + i * 0.8) + 1) * 0.5);
                canvas.Circle(new Vector2(centre.X + i * width * 0.17f, centre.Y + height * 0.28f),
                    Math.Max(2f, width * 0.025f), Palette.WithAlpha(Palette.Red, blink));
            }
        }
    }
}
