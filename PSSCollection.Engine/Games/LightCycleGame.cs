using System;
using System.Collections.Generic;
using System.Numerics;

namespace PSSCollection.Engine.Games
{
    /// <summary>
    /// PSS's Light Cycle: a grid arena of glowing trails. Turn, cut off the rival cycles and
    /// be the last one running. Touching any trail, including your own, ends your run.
    /// </summary>
    public sealed class LightCycleGame : GameBase
    {
        public const int Columns = 56;
        public const int Rows = 36;
        public const float Cell = 17f;
        public const float Width = Columns * Cell;    // 952
        public const float Height = Rows * Cell;      // 612

        public const int PointsPerOpponent = 250;
        public const int RoundBonus = 1000;
        public const double CountdownSeconds = 1.4;
        public const double RestartSeconds = 1.6;
        public const int MaxOpponents = 5;

        private sealed class Cycle
        {
            public int Column;
            public int Row;
            public int Dx = 1;
            public int Dy;
            public int QueuedDx;
            public int QueuedDy;
            public bool Player;
            public bool Alive = true;
            public int Colour;
            public readonly List<int> TrailColumns = new List<int>();
            public readonly List<int> TrailRows = new List<int>();
        }

        private readonly List<Entity> _entities = new List<Entity>();
        private readonly List<Cycle> _cycles = new List<Cycle>();
        private readonly int[,] _terrain = new int[Rows, Columns];

        private double _countdown;
        private double _restart;
        private double _moveAccumulator;
        private bool _roundOver;

        public override GameId Id => GameId.LightCycle;

        public override Vector2 PlayfieldSize => new Vector2(Width, Height);

        public override IReadOnlyList<Entity> Entities => _entities;

        public int OpponentCount
        {
            get
            {
                int count = 0;
                foreach (Cycle cycle in _cycles)
                    if (!cycle.Player && cycle.Alive)
                        count++;
                return count;
            }
        }

        public bool PlayerAlive
        {
            get
            {
                foreach (Cycle cycle in _cycles)
                    if (cycle.Player)
                        return cycle.Alive;
                return false;
            }
        }

        public (int Column, int Row) PlayerCell
        {
            get
            {
                foreach (Cycle cycle in _cycles)
                    if (cycle.Player)
                        return (cycle.Column, cycle.Row);
                return (-1, -1);
            }
        }

        public (int Dx, int Dy) PlayerDirection
        {
            get
            {
                foreach (Cycle cycle in _cycles)
                    if (cycle.Player)
                        return (cycle.Dx, cycle.Dy);
                return (0, 0);
            }
        }

        public int PlayerTrailLength
        {
            get
            {
                foreach (Cycle cycle in _cycles)
                    if (cycle.Player)
                        return cycle.TrailColumns.Count;
                return 0;
            }
        }

        /// <summary>True while the "get ready" countdown is running.</summary>
        public bool IsCountingDown => _countdown > 0;

        public double CellsPerSecond => 7.5 * SkillLevels.EnemySpeed(Skill) + (Level - 1) * 0.35;

        protected override void OnStart() => StartRound();

        private void StartRound()
        {
            Array.Clear(_terrain);
            _cycles.Clear();
            _roundOver = false;
            _moveAccumulator = 0;
            _countdown = CountdownSeconds;

            _cycles.Add(new Cycle
            {
                Column = 3,
                Row = Rows / 2,
                Dx = 1,
                Dy = 0,
                Player = true,
                Colour = 0
            });

            int opponents = Math.Min(1 + (Level - 1) / 2 + SkillLevels.ExtraStartingEnemies(Skill), MaxOpponents);
            for (int i = 0; i < opponents; i++)
            {
                int row = Rows / 2 + (i % 2 == 0 ? 1 : -1) * (2 + i * 4);
                row = Math.Clamp(row, 2, Rows - 3);

                _cycles.Add(new Cycle
                {
                    Column = Columns - 4,
                    Row = row,
                    Dx = -1,
                    Dy = 0,
                    Player = false,
                    Colour = 1 + i
                });
            }

            for (int i = 0; i < _cycles.Count; i++)
            {
                Cycle cycle = _cycles[i];
                _terrain[cycle.Row, cycle.Column] = i + 1;
                cycle.TrailColumns.Add(cycle.Column);
                cycle.TrailRows.Add(cycle.Row);
            }

            ShowMessage("ROUND " + Level, 1.2);
            BuildEntities();
        }

        protected override void UpdateCore(double deltaSeconds, InputState input)
        {
            if (_countdown > 0)
            {
                _countdown -= deltaSeconds;
                if (_countdown <= 0)
                {
                    _countdown = 0;
                    _moveAccumulator = 0;
                }

                BuildEntities();
                return;
            }

            if (_restart > 0)
            {
                _restart -= deltaSeconds;
                if (_restart <= 0 && !IsGameOver)
                    StartRound();

                BuildEntities();
                return;
            }

            if (_roundOver)
            {
                BuildEntities();
                return;
            }

            ReadInput(input);

            _moveAccumulator += deltaSeconds * CellsPerSecond;
            int steps = 0;
            while (_moveAccumulator >= 1.0 && steps < 8)
            {
                _moveAccumulator -= 1.0;
                steps++;
                StepRound();
                if (_roundOver)
                    break;
            }

            BuildEntities();
        }

        private void ReadInput(InputState input)
        {
            Cycle player = Player();
            if (player == null || !player.Alive)
                return;

            if (input.Left && player.Dx == 0)
            {
                player.QueuedDx = -1;
                player.QueuedDy = 0;
            }
            else if (input.Right && player.Dx == 0)
            {
                player.QueuedDx = 1;
                player.QueuedDy = 0;
            }
            else if (input.Up && player.Dy == 0)
            {
                player.QueuedDx = 0;
                player.QueuedDy = -1;
            }
            else if (input.Down && player.Dy == 0)
            {
                player.QueuedDx = 0;
                player.QueuedDy = 1;
            }
        }

        private Cycle Player()
        {
            foreach (Cycle cycle in _cycles)
                if (cycle.Player)
                    return cycle;
            return null;
        }

        private void StepRound()
        {
            for (int i = 0; i < _cycles.Count; i++)
            {
                Cycle cycle = _cycles[i];
                if (!cycle.Alive)
                    continue;

                if (!cycle.Player)
                    DecideDirection(cycle);

                Step(cycle, i);
            }

            bool playerAlive = PlayerAlive;
            int opponentsLeft = OpponentCount;

            if (!playerAlive)
            {
                PlayerCrashed();
                _roundOver = true;
                return;
            }

            if (opponentsLeft == 0)
            {
                Award(RoundBonus + 100 * Level);
                Level++;
                ShowMessage("ROUND CLEAR", 1.4);
                _restart = RestartSeconds;
                _roundOver = true;
            }
        }

        private void Step(Cycle cycle, int index)
        {
            int dx = cycle.QueuedDx;
            int dy = cycle.QueuedDy;

            if (dx == 0 && dy == 0)
            {
                dx = cycle.Dx;
                dy = cycle.Dy;
            }

            // A light cycle can only turn; it can never reverse.
            if (dx == -cycle.Dx && dy == -cycle.Dy)
            {
                dx = cycle.Dx;
                dy = cycle.Dy;
            }

            cycle.Dx = dx;
            cycle.Dy = dy;
            cycle.QueuedDx = 0;
            cycle.QueuedDy = 0;

            int column = cycle.Column + dx;
            int row = cycle.Row + dy;

            if (column < 0 || column >= Columns || row < 0 || row >= Rows || _terrain[row, column] != 0)
            {
                cycle.Alive = false;
                if (!cycle.Player)
                {
                    Queue(SoundId.Explosion);
                    Award(PointsPerOpponent);
                }

                return;
            }

            cycle.Column = column;
            cycle.Row = row;
            cycle.TrailColumns.Add(column);
            cycle.TrailRows.Add(row);
            _terrain[row, column] = index + 1;
        }

        private void DecideDirection(Cycle cycle)
        {
            Span<(int Dx, int Dy)> options = stackalloc (int, int)[3];
            int count = 0;

            AddOption(options, ref count, cycle, cycle.Dx, cycle.Dy);
            AddOption(options, ref count, cycle, -cycle.Dy, cycle.Dx);
            AddOption(options, ref count, cycle, cycle.Dy, -cycle.Dx);

            if (count == 0)
                return;

            (int Dx, int Dy) best = options[0];
            int bestScore = -1;

            for (int i = 0; i < count; i++)
            {
                (int dx, int dy) = options[i];
                int score = Look(cycle.Column, cycle.Row, dx, dy) * 4;

                // Lean towards the player's lane so the rivals are a real threat.
                Cycle player = Player();
                if (player != null && player.Alive)
                {
                    if (dy == 0 && Math.Abs(player.Row - cycle.Row) <= 2)
                        score += 3;
                    if (dx == 0 && Math.Abs(player.Column - cycle.Column) <= 2)
                        score += 3;
                    if (dx != 0 && Math.Sign(player.Column - cycle.Column) == dx)
                        score += 1;
                }

                score += Rng.Next(3);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = options[i];
                }
            }

            cycle.QueuedDx = best.Dx;
            cycle.QueuedDy = best.Dy;
        }

        private static void AddOption(Span<(int Dx, int Dy)> options, ref int count, Cycle cycle, int dx, int dy)
        {
            if (dx == 0 && dy == 0)
                return;
            if (dx == -cycle.Dx && dy == -cycle.Dy)
                return;

            options[count++] = (dx, dy);
        }

        private int Look(int column, int row, int dx, int dy, int max = 14)
        {
            int free = 0;
            for (int i = 1; i <= max; i++)
            {
                int nc = column + dx * i;
                int nr = row + dy * i;
                if (nc < 0 || nc >= Columns || nr < 0 || nr >= Rows || _terrain[nr, nc] != 0)
                    break;
                free++;
            }

            return free;
        }

        private void PlayerCrashed()
        {
            LoseLife();
            if (Lives <= 0)
            {
                EndGame();
                return;
            }

            ShowMessage("CRASH", 1.2);
            _restart = RestartSeconds;
        }

        /// <summary>Pixel position of a grid cell's top-left corner.</summary>
        public static Vector2 CellPosition(int column, int row) => new Vector2(column * Cell, row * Cell);

        private void BuildEntities()
        {
            _entities.Clear();

            foreach (Cycle cycle in _cycles)
            {
                for (int i = 0; i < cycle.TrailColumns.Count; i++)
                {
                    _entities.Add(new Entity(EntityKind.Trail,
                        CellPosition(cycle.TrailColumns[i], cycle.TrailRows[i]),
                        new Vector2(Cell, Cell), cycle.Colour));
                }
            }

            foreach (Cycle cycle in _cycles)
            {
                if (!cycle.Alive)
                    continue;

                float rotation = cycle.Dx > 0 ? 0f
                    : cycle.Dx < 0 ? MathHelper_Pi
                    : cycle.Dy > 0 ? MathHelper_HalfPi
                    : -MathHelper_HalfPi;

                _entities.Add(new Entity(cycle.Player ? EntityKind.Player : EntityKind.Enemy,
                    CellPosition(cycle.Column, cycle.Row), new Vector2(Cell, Cell), cycle.Colour, rotation));
            }
        }

        private const float MathHelper_Pi = (float)Math.PI;
        private const float MathHelper_HalfPi = (float)(Math.PI / 2.0);

        // ---- Test support -------------------------------------------------------------

        internal void TestSkipCountdown() => _countdown = 0;

        internal void TestStepOnce()
        {
            _countdown = 0;
            if (!_roundOver && _restart <= 0)
                StepRound();
            BuildEntities();
        }

        internal void TestPlacePlayer(int column, int row, int dx, int dy)
        {
            Cycle player = Player();
            if (player == null)
                return;

            player.Column = column;
            player.Row = row;
            player.Dx = dx;
            player.Dy = dy;
            _terrain[row, column] = 1;
        }

        internal void TestQueueTurn(int dx, int dy)
        {
            Cycle player = Player();
            if (player == null)
                return;
            player.QueuedDx = dx;
            player.QueuedDy = dy;
        }

        internal void TestOccupy(int column, int row)
        {
            if (column >= 0 && column < Columns && row >= 0 && row < Rows)
                _terrain[row, column] = 9;
        }

        internal void TestClearOpponents()
        {
            for (int i = _cycles.Count - 1; i >= 0; i--)
                if (!_cycles[i].Player)
                    _cycles.RemoveAt(i);
        }

        internal void TestAddOpponent(int column, int row, int dx, int dy)
        {
            var cycle = new Cycle
            {
                Column = column,
                Row = row,
                Dx = dx,
                Dy = dy,
                Player = false,
                Colour = _cycles.Count
            };
            _terrain[row, column] = _cycles.Count + 1;
            cycle.TrailColumns.Add(column);
            cycle.TrailRows.Add(row);
            _cycles.Add(cycle);
        }

        internal bool TestIsRoundOver => _roundOver;
    }
}
