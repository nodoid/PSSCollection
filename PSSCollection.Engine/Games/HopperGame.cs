using System;
using System.Collections.Generic;
using System.Numerics;

namespace PSSCollection.Engine.Games
{
    /// <summary>
    /// PSS's Hopper - the Oric's Frogger. Five homes in the hedge, a river of logs and diving
    /// turtles, a bank patrolled by a snake, three lanes of traffic and the start bank.
    /// One TIME budget covers a whole sheet of five frogs.
    /// </summary>
    public sealed class HopperGame : GameBase
    {
        public const int Columns = 20;
        public const int Rows = 10;
        public const float Cell = 56f;
        public const float Width = Columns * Cell;    // 1120
        public const float Height = Rows * Cell;      // 560

        public const int HomeRow = 0;
        public const int RiverTopRow = 1;
        public const int RiverBottomRow = 4;
        public const int MedianRow = 5;
        public const int RoadTopRow = 6;
        public const int RoadBottomRow = 8;
        public const int StartRow = 9;

        public const int BayCount = 5;
        public const int BayWidth = 2;

        public const double HopCooldown = 0.16;
        public const double RespawnSeconds = 1.1;
        public const int ForwardPoints = 10;
        public const int HomePoints = 100;
        public const int TimeUnits = 99;
        public const double SecondsPerTimeUnit = 0.79;
        public const double SnakeSpeed = 78.0;
        public const double FlySeconds = 7.0;
        public const int FlyBonusMin = 800;
        public const int FlyBonusMax = 1100;

        private static readonly int[] BayColumns = { 1, 5, 9, 13, 17 };

        private sealed class Lane
        {
            public int Row;
            public bool River;
            public float Speed;
            public float SlotWidth;
            public int SlotCount;
            public int LengthCells;
            public int Variant;
            public float Offset;
            public bool[] Filled;

            public float Length => LengthCells * Cell;

            public float Span => SlotCount * SlotWidth;

            public float SlotX(int i)
            {
                float x = Offset + i * SlotWidth;
                float span = Span;
                x %= span;
                if (x < 0)
                    x += span;
                return x;
            }

            public void Advance(double deltaSeconds)
            {
                Offset += (float)(Speed * deltaSeconds);
                float span = Span;
                if (Offset >= span)
                    Offset -= span;
                else if (Offset <= -span)
                    Offset += span;
            }
        }

        private readonly List<Entity> _entities = new List<Entity>();
        private readonly List<Lane> _lanes = new List<Lane>();
        private readonly bool[] _bays = new bool[BayCount];

        private float _playerX;
        private int _playerRow = StartRow;
        private double _hopTimer;
        private double _respawnTimer;
        private int _timeLeft = TimeUnits;
        private double _timeAccumulator;
        private int _bestRow = StartRow;
        private int _baysFilled;
        private bool _riding;
        private float _snakeX;
        private int _snakeDirection = 1;
        private double _flyTimer;
        private int _flyBay = -1;
        private double _divePhase;

        public override GameId Id => GameId.Hopper;

        public override Vector2 PlayfieldSize => new Vector2(Width, Height);

        public override IReadOnlyList<Entity> Entities => _entities;

        public int PlayerRow => _playerRow;

        public int PlayerColumn => Math.Clamp((int)Math.Round(_playerX / Cell), 0, Columns - 1);

        public float PlayerX => _playerX;

        public int BaysFilled => _baysFilled;

        public int TimeLeft => _timeLeft;

        public bool IsRiding => _riding;

        public float SnakeX => _snakeX;

        public int FlyBay => _flyBay;

        public bool TurtlesDive => Level >= 2;

        public bool FliesAppear => Level >= 3;

        public int CarCount
        {
            get
            {
                int count = 0;
                foreach (Lane lane in _lanes)
                    if (!lane.River)
                        foreach (bool filled in lane.Filled)
                            if (filled)
                                count++;
                return count;
            }
        }

        public int FloatCount
        {
            get
            {
                int count = 0;
                foreach (Lane lane in _lanes)
                {
                    if (!lane.River)
                        continue;
                    foreach (bool filled in lane.Filled)
                        if (filled)
                            count++;
                }

                return count;
            }
        }

        protected override void OnStart()
        {
            // The original gives five lives; the tougher skills start with fewer.
            Lives = SkillLevels.Clamp(Skill) == SkillLevel.Ace ? 3 : SkillLevels.StartingLives(Skill) + 1;
            BuildLanes();
            RespawnPlayer();
            _timeLeft = TimeUnits;
            _timeAccumulator = 0;
            _snakeX = Width * 0.5f;
            _flyTimer = FlySeconds * 2;
            BuildEntities();
        }

        private void BuildLanes()
        {
            _lanes.Clear();

            float[] riverSpeeds = { 78f, -96f, 86f, -112f };
            float[] roadSpeeds = { -128f, 152f, -168f };
            float speedScale = (float)(SkillLevels.EnemySpeed(Skill) * (1.0 + 0.06 * (Level - 1)));
            float density = Density();

            for (int row = RiverTopRow; row <= RiverBottomRow; row++)
            {
                int lane = row - RiverTopRow;
                bool turtles = lane == 1;
                _lanes.Add(new Lane
                {
                    Row = row,
                    River = true,
                    Speed = riverSpeeds[lane] * speedScale,
                    SlotCount = 4,
                    SlotWidth = Width / 4f,
                    LengthCells = turtles ? 2 : 3,
                    Variant = turtles ? 1 : 0,
                    Offset = (float)Rng.Range(0.0, Width),
                    Filled = FillSlots(4, density, true)
                });
            }

            for (int row = RoadTopRow; row <= RoadBottomRow; row++)
            {
                int lane = row - RoadTopRow;
                _lanes.Add(new Lane
                {
                    Row = row,
                    River = false,
                    Speed = roadSpeeds[lane] * speedScale,
                    SlotCount = 4,
                    SlotWidth = Width / 4f,
                    LengthCells = 2,
                    Variant = lane,
                    Offset = (float)Rng.Range(0.0, Width),
                    Filled = FillSlots(4, density, false)
                });
            }
        }

        private float Density()
        {
            switch (SkillLevels.Clamp(Skill))
            {
                case SkillLevel.Novice: return 0.55f;
                case SkillLevel.Skilled: return 0.7f;
                case SkillLevel.Veteran: return 0.85f;
                default: return 1.0f;
            }
        }

        private bool[] FillSlots(int count, float density, bool river)
        {
            var filled = new bool[count];
            for (int i = 0; i < count; i++)
                filled[i] = river ? i < 3 || Rng.NextDouble() < density : Rng.NextDouble() < density;

            if (!river)
            {
                bool any = false;
                foreach (bool slot in filled)
                    any |= slot;
                if (!any)
                    filled[Rng.Next(count)] = true;
            }

            return filled;
        }

        private void RespawnPlayer()
        {
            _playerX = BayColumns[BayCount / 2] * Cell;
            _playerRow = StartRow;
            _bestRow = StartRow;
            _hopTimer = 0;
            _riding = false;
        }

        protected override void UpdateCore(double deltaSeconds, InputState input)
        {
            if (_respawnTimer > 0)
            {
                _respawnTimer -= deltaSeconds;
                BuildEntities();
                return;
            }

            foreach (Lane lane in _lanes)
                lane.Advance(deltaSeconds);

            MoveSnake(deltaSeconds);
            UpdateFly(deltaSeconds);
            _divePhase += deltaSeconds;

            if (_hopTimer > 0)
                _hopTimer -= deltaSeconds;

            if (InRiver(_playerRow))
            {
                _riding = RideFloat(deltaSeconds);
                if (!_riding)
                {
                    Die("SPLASH");
                    return;
                }

                if (_playerX < -Cell * 0.4f || _playerX > Width - Cell * 0.6f)
                {
                    Die("WASHED AWAY");
                    return;
                }
            }
            else
            {
                _riding = false;
            }

            if (_hopTimer <= 0)
                ReadInput(input);

            if (CarHitsPlayer())
            {
                Die("OUCH");
                return;
            }

            if (SnakeHitsPlayer())
            {
                Die("SNAKE");
                return;
            }

            _timeAccumulator += deltaSeconds;
            while (_timeAccumulator >= SecondsPerTimeUnit && _timeLeft > 0)
            {
                _timeAccumulator -= SecondsPerTimeUnit;
                _timeLeft--;
            }

            if (_timeLeft <= 0)
            {
                Die("TIME UP");
                return;
            }

            BuildEntities();
        }

        private void MoveSnake(double deltaSeconds)
        {
            _snakeX += (float)(SnakeSpeed * _snakeDirection * deltaSeconds
                * (1.0 + 0.05 * (Level - 1)));
            if (_snakeX < Cell * 0.5f)
            {
                _snakeX = Cell * 0.5f;
                _snakeDirection = 1;
            }
            else if (_snakeX > Width - Cell * 1.5f)
            {
                _snakeX = Width - Cell * 1.5f;
                _snakeDirection = -1;
            }
        }

        private void UpdateFly(double deltaSeconds)
        {
            if (!FliesAppear)
            {
                _flyBay = -1;
                return;
            }

            _flyTimer -= deltaSeconds;
            if (_flyTimer > 0)
                return;

            if (_flyBay >= 0)
            {
                _flyBay = -1;
                _flyTimer = Rng.Range(6.0, 10.0);
                return;
            }

            var empty = new List<int>();
            for (int i = 0; i < BayCount; i++)
                if (!_bays[i])
                    empty.Add(i);

            if (empty.Count > 0)
            {
                _flyBay = empty[Rng.Next(empty.Count)];
                _flyTimer = FlySeconds;
            }
            else
            {
                _flyTimer = 1.0;
            }
        }

        private static bool InRiver(int row) => row >= RiverTopRow && row <= RiverBottomRow;

        private void ReadInput(InputState input)
        {
            int dx = 0;
            int dy = 0;

            if (input.Left)
                dx = -1;
            else if (input.Right)
                dx = 1;
            else if (input.Up)
                dy = -1;
            else if (input.Down)
                dy = 1;

            if (dx != 0 || dy != 0)
                Hop(dx, dy);
        }

        private void Hop(int dx, int dy)
        {
            _hopTimer = HopCooldown;
            Queue(SoundId.Push);

            int row = Math.Clamp(_playerRow + dy, HomeRow, StartRow);
            int column = Math.Clamp((int)Math.Round(_playerX / Cell) + dx, 0, Columns - 1);
            float x = column * Cell;

            if (dy < 0 && row == HomeRow)
            {
                TryEnterHome(x);
                return;
            }

            _playerX = x;
            _playerRow = row;

            if (row < _bestRow)
            {
                Award(ForwardPoints * (_bestRow - row));
                _bestRow = row;
            }
        }

        private void TryEnterHome(float x)
        {
            for (int i = 0; i < BayCount; i++)
            {
                float bayLeft = BayColumns[i] * Cell;
                if (x < bayLeft || x > bayLeft + (BayWidth - 1) * Cell)
                    continue;

                if (_bays[i])
                {
                    Die("OCCUPIED");
                    return;
                }

                _bays[i] = true;
                _baysFilled++;

                if (_flyBay == i)
                {
                    Award(Rng.Range(FlyBonusMin, FlyBonusMax + 1));
                    _flyBay = -1;
                    _flyTimer = Rng.Range(6.0, 10.0);
                }

                Award(HomePoints);
                Queue(SoundId.WaveClear);

                if (_baysFilled >= BayCount)
                {
                    // Clearing the sheet pays ten for every unit of TIME left on the clock.
                    Award(ForwardPoints * _timeLeft);
                    Level++;
                    _baysFilled = 0;
                    Array.Clear(_bays, 0, _bays.Length);
                    _timeLeft = TimeUnits;
                    _timeAccumulator = 0;
                    _flyBay = -1;
                    _flyTimer = FlySeconds * 2;
                    BuildLanes();
                    ShowMessage("SHEET " + Level, 1.6);
                }
                else
                {
                    ShowMessage("HOME", 1.0);
                }

                RespawnPlayer();
                return;
            }

            Die("HEDGE");
        }

        private bool RideFloat(double deltaSeconds)
        {
            foreach (Lane lane in _lanes)
            {
                if (lane.Row != _playerRow || !lane.River)
                    continue;

                float centre = _playerX + Cell * 0.5f;
                for (int i = 0; i < lane.SlotCount; i++)
                {
                    if (!lane.Filled[i])
                        continue;

                    float left = lane.SlotX(i);
                    if (centre < left || centre > left + lane.Length)
                        continue;

                    if (lane.Variant == 1 && TurtlesDive && IsSubmerged(i))
                        return false;

                    _playerX += (float)(lane.Speed * deltaSeconds);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Turtles surface and dive in a staggered cycle from sheet two onward.</summary>
        public bool IsSubmerged(int slot) => ((_divePhase + slot * 0.9) % 4.4) > 3.2;

        private bool CarHitsPlayer()
        {
            float frogLeft = _playerX + Cell * 0.18f;
            float frogRight = _playerX + Cell * 0.82f;

            foreach (Lane lane in _lanes)
            {
                if (lane.River || lane.Row != _playerRow)
                    continue;

                for (int i = 0; i < lane.SlotCount; i++)
                {
                    if (!lane.Filled[i])
                        continue;

                    float left = lane.SlotX(i);
                    if (Hits(left, lane.Length, frogLeft, frogRight) || Hits(left - lane.Span, lane.Length, frogLeft, frogRight))
                        return true;
                }
            }

            return false;

            static bool Hits(float carLeft, float carLength, float frogLeft, float frogRight) =>
                frogLeft < carLeft + carLength && frogRight > carLeft;
        }

        private bool SnakeHitsPlayer()
        {
            if (_playerRow != MedianRow)
                return false;

            float frogLeft = _playerX + Cell * 0.2f;
            float frogRight = _playerX + Cell * 0.8f;
            return frogLeft < _snakeX + Cell && frogRight > _snakeX;
        }

        private void Die(string reason)
        {
            LoseLife();
            ShowMessage(reason, 1.2);
            _riding = false;

            if (Lives <= 0)
            {
                EndGame();
                return;
            }

            _respawnTimer = RespawnSeconds;
            RespawnPlayer();

            if (reason == "TIME UP")
            {
                _timeLeft = TimeUnits;
                _timeAccumulator = 0;
            }
        }

        private void BuildEntities()
        {
            _entities.Clear();

            for (int i = 0; i < BayCount; i++)
            {
                _entities.Add(new Entity(EntityKind.Home,
                    new Vector2(BayColumns[i] * Cell, HomeRow * Cell),
                    new Vector2(BayWidth * Cell, Cell), _bays[i] ? 1 : 0));
            }

            if (_flyBay >= 0)
            {
                _entities.Add(new Entity(EntityKind.Fly,
                    new Vector2(BayColumns[_flyBay] * Cell + Cell * 0.25f, HomeRow * Cell + Cell * 0.2f),
                    new Vector2(Cell * 0.5f, Cell * 0.55f)));
            }

            foreach (Lane lane in _lanes)
            {
                EntityKind kind = !lane.River ? EntityKind.Enemy
                    : lane.Variant == 1 ? EntityKind.Turtle
                    : EntityKind.Log;

                for (int i = 0; i < lane.SlotCount; i++)
                {
                    if (!lane.Filled[i])
                        continue;

                    int variant = lane.Variant;
                    bool hidden = lane.River && variant == 1 && TurtlesDive && IsSubmerged(i);
                    if (hidden)
                        variant = 2;

                    float left = lane.SlotX(i);
                    var size = new Vector2(lane.Length, Cell - 6);
                    var position = new Vector2(left, lane.Row * Cell + 3);
                    _entities.Add(new Entity(kind, position, size, variant));

                    if (left + lane.Length > Width)
                        _entities.Add(new Entity(kind, new Vector2(left - lane.Span, lane.Row * Cell + 3), size, variant));
                }
            }

            _entities.Add(new Entity(EntityKind.Snake,
                new Vector2(_snakeX, MedianRow * Cell + Cell * 0.25f),
                new Vector2(Cell, Cell * 0.5f), _snakeDirection > 0 ? 0 : 1));

            if (_respawnTimer <= 0)
            {
                _entities.Add(new Entity(EntityKind.Player,
                    new Vector2(_playerX + Cell * 0.1f, _playerRow * Cell + Cell * 0.1f),
                    new Vector2(Cell * 0.8f, Cell * 0.8f)));
            }
        }

        // ---- Test support -------------------------------------------------------------

        internal void TestClearTraffic()
        {
            foreach (Lane lane in _lanes)
                for (int i = 0; i < lane.Filled.Length; i++)
                    lane.Filled[i] = false;
        }

        internal void TestSetPlayer(int column, int row)
        {
            _playerX = Math.Clamp(column, 0, Columns - 1) * Cell;
            _playerRow = Math.Clamp(row, HomeRow, StartRow);
            _hopTimer = 0;
            _respawnTimer = 0;
        }

        internal void TestHop(int dx, int dy)
        {
            _hopTimer = 0;
            Hop(dx, dy);
        }

        internal void TestSetTimeLeft(int units)
        {
            _timeLeft = units;
            _timeAccumulator = 0;
        }

        internal void TestAddCar(int row, float x, int lengthCells)
        {
            Lane lane = LaneForRow(row, false);
            if (lane == null)
                return;

            lane.Filled[0] = true;
            lane.LengthCells = lengthCells;
            lane.Offset = x;
            lane.Speed = 0;
            BuildEntities();
        }

        internal void TestAddFloat(int row, float x, int lengthCells, float speed)
        {
            Lane lane = LaneForRow(row, true);
            if (lane == null)
                return;

            lane.Filled[0] = true;
            lane.LengthCells = lengthCells;
            lane.Offset = x;
            lane.Speed = speed;
            BuildEntities();
        }

        internal void TestSetSnake(float x) => _snakeX = x;

        internal void TestSetLevel(int level) => Level = Math.Max(1, level);

        internal void TestSpawnFly(int bay)
        {
            if (Level < 3)
                Level = 3;
            _flyBay = bay;
            _flyTimer = FlySeconds;
            BuildEntities();
        }

        internal void TestFillBay(int index)
        {
            if (index < 0 || index >= BayCount || _bays[index])
                return;

            _bays[index] = true;
            _baysFilled++;
        }

        private Lane LaneForRow(int row, bool river)
        {
            foreach (Lane lane in _lanes)
                if (lane.Row == row && lane.River == river)
                    return lane;
            return null;
        }
    }
}
