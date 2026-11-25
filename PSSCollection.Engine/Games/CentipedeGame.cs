using System;
using System.Collections.Generic;
using System.Numerics;

namespace PSSCollection.Engine.Games
{
    /// <summary>
    /// PSS's Centipede: a segmented 'pede snakes down through a mushroom field. Shoot a
    /// segment and it becomes a mushroom while the body behind it splits into a new 'pede.
    /// A bouncing spider and the mushroom-planting flea keep the lower band dangerous.
    /// </summary>
    public sealed class CentipedeGame : GameBase
    {
        public const int Columns = 40;
        public const int Rows = 30;
        public const float Cell = 20f;
        public const float Width = Columns * Cell;    // 800
        public const float Height = Rows * Cell;      // 600
        public const int SpawnRow = 6;
        public const int PlayerZoneTopRow = 24;
        public const float PlayerZoneTop = PlayerZoneTopRow * Cell;   // 480
        public const float PlayerSize = 18f;
        public const float PlayerSpeed = 240f;
        public const float BulletSpeed = 900f;
        public const int MushroomHealth = 4;
        public const int StartingSegments = 12;
        public const int StartingMushrooms = 26;
        public const double FireCooldown = 0.22;
        public const int HeadScore = 100;
        public const int BodyScore = 10;
        public const int MushroomScore = 1;
        public const int FleaScore = 200;

        private sealed class Segment
        {
            public int Column;
            public int Row;
        }

        private sealed class Chain
        {
            public readonly List<Segment> Segments = new List<Segment>();
            public int Direction = 1;
            public int Vertical = 1;
            public double MoveTimer;
            public int Length => Segments.Count;
        }

        private sealed class Spider
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public double Life;
            public bool Alive = true;
        }

        private sealed class Flea
        {
            public int Column;
            public float Y;
            public double DropTimer;
            public bool Alive = true;
        }

        private readonly List<Entity> _entities = new List<Entity>();
        private readonly byte[,] _mushrooms = new byte[Rows, Columns];
        private readonly List<Chain> _chains = new List<Chain>();
        private readonly List<Spider> _spiders = new List<Spider>();
        private readonly List<Flea> _fleas = new List<Flea>();

        private Vector2 _player;
        private double _fireCooldown;
        private float _bulletX;
        private float _bulletY;
        private bool _bulletActive;
        private double _spiderTimer;
        private double _fleaTimer;
        private double _invulnerableTimer;
        private double _respawnTimer;
        private int _mushroomCount;

        public override GameId Id => GameId.Centipede;

        public override Vector2 PlayfieldSize => new Vector2(Width, Height);

        public override IReadOnlyList<Entity> Entities => _entities;

        public Vector2 PlayerPosition => _player;

        public bool BulletActive => _bulletActive;

        public int MushroomCount => _mushroomCount;

        public int ChainCount => _chains.Count;

        public int SegmentCount
        {
            get
            {
                int count = 0;
                foreach (Chain chain in _chains)
                    count += chain.Length;
                return count;
            }
        }

        public int SpiderCount
        {
            get
            {
                int count = 0;
                foreach (Spider spider in _spiders)
                    if (spider.Alive)
                        count++;
                return count;
            }
        }

        public int FleaCount
        {
            get
            {
                int count = 0;
                foreach (Flea flea in _fleas)
                    if (flea.Alive)
                        count++;
                return count;
            }
        }

        public bool PlayerInvulnerable => _invulnerableTimer > 0 || _respawnTimer > 0;

        public byte MushroomAt(int column, int row) =>
            column < 0 || column >= Columns || row < 0 || row >= Rows ? (byte)0 : _mushrooms[row, column];

        protected override void OnStart()
        {
            Array.Clear(_mushrooms);
            _mushroomCount = 0;
            _chains.Clear();
            _spiders.Clear();
            _fleas.Clear();

            int mushrooms = StartingMushrooms + SkillLevels.ExtraStartingEnemies(Skill) * 3;
            for (int i = 0; i < mushrooms; i++)
            {
                int column = Rng.Next(Columns);
                int row = Rng.Range(3, Rows - 8);

                // Keep the player's opening cell and its neighbours clear.
                if (column >= Columns / 2 - 3 && column <= Columns / 2 + 3 && row >= Rows - 8)
                    continue;

                if (_mushrooms[row, column] == 0)
                {
                    _mushrooms[row, column] = MushroomHealth;
                    _mushroomCount++;
                }
            }

            _player = new Vector2(Width * 0.5f - PlayerSize * 0.5f, Height - PlayerSize - 8);
            _fireCooldown = 0;
            _bulletActive = false;
            _spiderTimer = Rng.Range(5.0, 9.0);
            _fleaTimer = 12.0;
            _invulnerableTimer = 2.0;
            _respawnTimer = 0;

            SpawnChain(Level);
            RebuildEntities();
        }

        private void SpawnChain(int level)
        {
            var chain = new Chain { Direction = Rng.Chance(0.5) ? 1 : -1, Vertical = 1 };

            // Start clear of the top edge: on a phone the HUD strip sits over the first 96 pixels
            // of the board, and the centipede used to be born underneath it.
            int startRow = SpawnRow;
            int startColumn = chain.Direction > 0 ? 0 : Columns - 1;

            int length = StartingSegments + Math.Min(level - 1, 4);
            for (int i = 0; i < length; i++)
            {
                chain.Segments.Add(new Segment
                {
                    Column = Math.Clamp(startColumn - chain.Direction * i, 0, Columns - 1),
                    Row = startRow
                });
            }

            chain.MoveTimer = 0;
            _chains.Add(chain);
        }

        protected override void UpdateCore(double deltaSeconds, InputState input)
        {
            if (_invulnerableTimer > 0)
                _invulnerableTimer = Math.Max(0, _invulnerableTimer - deltaSeconds);

            if (_respawnTimer > 0)
            {
                _respawnTimer -= deltaSeconds;
                if (_respawnTimer <= 0)
                {
                    _respawnTimer = 0;
                    _invulnerableTimer = 2.0;
                }
            }
            else
            {
                MovePlayer(deltaSeconds, input);
                HandleFiring(deltaSeconds, input);
            }

            UpdateChains(deltaSeconds);
            UpdateSpiders(deltaSeconds);
            UpdateFleas(deltaSeconds);
            UpdateBullet(deltaSeconds);
            CheckPlayerContact();
            CheckWaveState();
            RebuildEntities();
        }

        private void MovePlayer(double deltaSeconds, InputState input)
        {
            float distance = (float)(PlayerSpeed * deltaSeconds);

            if (input.Left && !input.Right)
                _player.X -= distance;
            if (input.Right && !input.Left)
                _player.X += distance;
            if (input.Up && !input.Down)
                _player.Y -= distance;
            if (input.Down && !input.Up)
                _player.Y += distance;

            _player.X = Math.Clamp(_player.X, 0, Width - PlayerSize);
            _player.Y = Math.Clamp(_player.Y, PlayerZoneTop, Height - PlayerSize - 2);
        }

        private void HandleFiring(double deltaSeconds, InputState input)
        {
            _fireCooldown = Math.Max(0, _fireCooldown - deltaSeconds);
            if (!input.Fire || _bulletActive || _fireCooldown > 0)
                return;

            _bulletActive = true;
            _bulletX = _player.X + PlayerSize * 0.5f - 2f;
            _bulletY = _player.Y - 10f;
            _fireCooldown = FireCooldown;
            Queue(SoundId.Shoot);
        }

        private void UpdateBullet(double deltaSeconds)
        {
            if (!_bulletActive)
                return;

            _bulletY -= (float)(BulletSpeed * deltaSeconds);
            if (_bulletY < -12f)
            {
                _bulletActive = false;
                return;
            }

            int column = (int)(_bulletX / Cell);
            int row = (int)(_bulletY / Cell);

            if (column >= 0 && column < Columns && row >= 0 && row < Rows && _mushrooms[row, column] > 0)
            {
                _mushrooms[row, column]--;
                if (_mushrooms[row, column] == 0)
                    _mushroomCount--;
                Award(MushroomScore);
                Queue(SoundId.EnemyShoot);
                _bulletActive = false;
                return;
            }

            // Segments: walk chains in order so the head is tested first.
            for (int c = 0; c < _chains.Count; c++)
            {
                Chain chain = _chains[c];
                for (int s = 0; s < chain.Segments.Count; s++)
                {
                    Segment segment = chain.Segments[s];
                    if (segment.Column != column || segment.Row != row)
                        continue;

                    HitSegment(c, s);
                    _bulletActive = false;
                    return;
                }
            }

            foreach (Spider spider in _spiders)
            {
                if (!spider.Alive)
                    continue;

                if (!Entity.Overlap(new Vector2(_bulletX, _bulletY), new Vector2(4, 12), spider.Position, new Vector2(Cell * 1.6f)))
                    continue;

                spider.Alive = false;
                Award(SpiderScore(spider.Position));
                Queue(SoundId.Explosion);
                _bulletActive = false;
                return;
            }

            foreach (Flea flea in _fleas)
            {
                if (!flea.Alive)
                    continue;

                if (Math.Abs(_bulletX - flea.Column * Cell - Cell * 0.5f) > Cell ||
                    Math.Abs(_bulletY - flea.Y) > Cell)
                    continue;

                flea.Alive = false;
                Award(FleaScore);
                Queue(SoundId.Explosion);
                _bulletActive = false;
                return;
            }
        }

        private void HitSegment(int chainIndex, int segmentIndex)
        {
            Chain chain = _chains[chainIndex];
            Segment hit = chain.Segments[segmentIndex];

            Award(segmentIndex == 0 ? HeadScore : BodyScore);
            Queue(SoundId.EnemyDestroyed);

            if (_mushrooms[hit.Row, hit.Column] == 0)
            {
                _mushrooms[hit.Row, hit.Column] = MushroomHealth;
                _mushroomCount++;
            }

            var tail = new List<Segment>();
            for (int i = segmentIndex + 1; i < chain.Segments.Count; i++)
                tail.Add(chain.Segments[i]);

            chain.Segments.RemoveRange(segmentIndex, chain.Segments.Count - segmentIndex);

            if (chain.Segments.Count == 0)
                _chains.RemoveAt(chainIndex);

            if (tail.Count > 0)
            {
                var newChain = new Chain
                {
                    Direction = -chain.Direction,
                    Vertical = chain.Vertical,
                    MoveTimer = 0
                };
                newChain.Segments.AddRange(tail);
                _chains.Add(newChain);
            }
        }

        private void UpdateChains(double deltaSeconds)
        {
            double interval = 0.16 / SkillLevels.EnemySpeed(Skill) / (1.0 + 0.04 * (Level - 1));
            interval = Math.Max(0.05, interval);

            foreach (Chain chain in _chains)
            {
                chain.MoveTimer += deltaSeconds;
                while (chain.MoveTimer >= interval)
                {
                    chain.MoveTimer -= interval;
                    StepChain(chain);
                }
            }
        }

        private void StepChain(Chain chain)
        {
            if (chain.Length == 0)
                return;

            Segment head = chain.Segments[0];
            int newColumn = head.Column + chain.Direction;
            int newRow = head.Row;

            bool blocked = newColumn < 0 || newColumn >= Columns || _mushrooms[newRow, newColumn] > 0;

            if (blocked)
            {
                chain.Direction = -chain.Direction;
                newColumn = head.Column;
                newRow = head.Row + chain.Vertical;

                if (newRow < 0 || newRow >= Rows)
                {
                    chain.Vertical = -chain.Vertical;
                    newRow = head.Row + chain.Vertical;
                }

                if (newRow < 0 || newRow >= Rows)
                    return;
            }

            for (int i = chain.Segments.Count - 1; i > 0; i--)
            {
                chain.Segments[i].Column = chain.Segments[i - 1].Column;
                chain.Segments[i].Row = chain.Segments[i - 1].Row;
            }

            head.Column = newColumn;
            head.Row = newRow;
        }

        private void UpdateSpiders(double deltaSeconds)
        {
            _spiderTimer -= deltaSeconds;
            if (_spiderTimer <= 0 && SpiderCount == 0 && ChainCount > 0)
            {
                _spiderTimer = Rng.Range(7.0, 13.0) / SkillLevels.EnemySpeed(Skill);
                bool fromLeft = Rng.Chance(0.5);
                _spiders.Add(new Spider
                {
                    Position = new Vector2(fromLeft ? 4 : Width - Cell - 4, (float)Rng.Range(PlayerZoneTop + 20, Height - 60)),
                    Velocity = new Vector2(fromLeft ? 150f : -150f, Rng.Chance(0.5) ? 110f : -110f),
                    Life = 14.0
                });
                Queue(SoundId.EnemyShoot);
            }

            for (int i = _spiders.Count - 1; i >= 0; i--)
            {
                Spider spider = _spiders[i];
                if (!spider.Alive)
                {
                    _spiders.RemoveAt(i);
                    continue;
                }

                spider.Life -= deltaSeconds;
                if (spider.Life <= 0)
                {
                    _spiders.RemoveAt(i);
                    continue;
                }

                spider.Position += spider.Velocity * (float)(deltaSeconds * SkillLevels.EnemySpeed(Skill));

                if (spider.Position.X < 2)
                {
                    spider.Position.X = 2;
                    spider.Velocity.X = Math.Abs(spider.Velocity.X);
                }
                else if (spider.Position.X > Width - Cell - 2)
                {
                    spider.Position.X = Width - Cell - 2;
                    spider.Velocity.X = -Math.Abs(spider.Velocity.X);
                }

                if (spider.Position.Y < PlayerZoneTop + 10)
                {
                    spider.Position.Y = PlayerZoneTop + 10;
                    spider.Velocity.Y = Math.Abs(spider.Velocity.Y);
                }
                else if (spider.Position.Y > Height - Cell - 6)
                {
                    spider.Position.Y = Height - Cell - 6;
                    spider.Velocity.Y = -Math.Abs(spider.Velocity.Y);
                }
            }
        }

        private void UpdateFleas(double deltaSeconds)
        {
            _fleaTimer -= deltaSeconds;

            if (_fleaTimer <= 0 && _mushroomCount < 18 && FleaCount == 0 && ChainCount > 0)
            {
                _fleaTimer = Rng.Range(10.0, 18.0);
                _fleas.Add(new Flea { Column = Rng.Next(Columns), Y = -Cell, DropTimer = 0 });
            }

            for (int i = _fleas.Count - 1; i >= 0; i--)
            {
                Flea flea = _fleas[i];
                if (!flea.Alive)
                {
                    _fleas.RemoveAt(i);
                    continue;
                }

                float step = (float)(230 * SkillLevels.EnemySpeed(Skill) * deltaSeconds);
                flea.Y += step;
                flea.DropTimer += step;

                if (flea.DropTimer >= Cell * 3)
                {
                    flea.DropTimer = 0;
                    int row = (int)(flea.Y / Cell);
                    if (row >= 1 && row < Rows - 1 && _mushrooms[row, flea.Column] == 0)
                    {
                        _mushrooms[row, flea.Column] = MushroomHealth;
                        _mushroomCount++;
                    }
                }

                if (flea.Y > Height + Cell)
                    _fleas.RemoveAt(i);
            }
        }

        private void CheckPlayerContact()
        {
            if (PlayerInvulnerable || IsGameOver)
                return;

            var playerBox = new Vector2(_player.X, _player.Y);

            foreach (Chain chain in _chains)
            {
                foreach (Segment segment in chain.Segments)
                {
                    if (Entity.Overlap(playerBox, new Vector2(PlayerSize), new Vector2(segment.Column * Cell, segment.Row * Cell), new Vector2(Cell)))
                    {
                        HitPlayer();
                        return;
                    }
                }
            }

            foreach (Spider spider in _spiders)
            {
                if (spider.Alive && Entity.Overlap(playerBox, new Vector2(PlayerSize), spider.Position, new Vector2(Cell * 1.4f)))
                {
                    HitPlayer();
                    return;
                }
            }
        }

        private void HitPlayer()
        {
            LoseLife();
            _bulletActive = false;
            if (Lives <= 0)
            {
                EndGame();
                return;
            }

            _respawnTimer = 1.4;
            _player = new Vector2(Width * 0.5f - PlayerSize * 0.5f, Height - PlayerSize - 8);
            ShowMessage("CAUGHT", 1.2);
        }

        private void CheckWaveState()
        {
            if (IsGameOver || _respawnTimer > 0)
                return;

            if (ChainCount == 0 && _chains.Count == 0)
            {
                CompleteWave(500 + 150 * Level);
                _spiders.Clear();
                _fleas.Clear();
                SpawnChain(Level);
                _invulnerableTimer = 2.0;
            }
        }

        private int SpiderScore(Vector2 position)
        {
            float distance = Vector2.Distance(position, _player);
            if (distance > 260)
                return 300;
            if (distance > 130)
                return 600;
            return 900;
        }

        private void RebuildEntities()
        {
            _entities.Clear();

            if (_respawnTimer <= 0 && !IsGameOver)
            {
                bool flash = _invulnerableTimer > 0 && ((int)(Elapsed * 12) % 2 == 0);
                _entities.Add(new Entity(EntityKind.Player, _player, new Vector2(PlayerSize), 0, 0f, !flash));
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    byte health = _mushrooms[row, column];
                    if (health == 0)
                        continue;

                    _entities.Add(new Entity(EntityKind.Mushroom, new Vector2(column * Cell, row * Cell),
                        new Vector2(Cell), health, 0f));
                }
            }

            foreach (Chain chain in _chains)
            {
                for (int i = 0; i < chain.Segments.Count; i++)
                {
                    Segment segment = chain.Segments[i];
                    _entities.Add(new Entity(
                        i == 0 ? EntityKind.CentipedeHead : EntityKind.CentipedeBody,
                        new Vector2(segment.Column * Cell, segment.Row * Cell),
                        new Vector2(Cell),
                        i % 4));
                }
            }

            foreach (Spider spider in _spiders)
            {
                if (spider.Alive)
                    _entities.Add(new Entity(EntityKind.Spider, spider.Position, new Vector2(Cell * 1.5f)));
            }

            foreach (Flea flea in _fleas)
            {
                if (flea.Alive)
                    _entities.Add(new Entity(EntityKind.Flea, new Vector2(flea.Column * Cell, flea.Y), new Vector2(Cell)));
            }

            if (_bulletActive)
                _entities.Add(new Entity(EntityKind.Bullet, new Vector2(_bulletX, _bulletY), new Vector2(4, 12)));
        }

        // ---- Test support -------------------------------------------------------------

        internal void TestPlacePlayer(float x, float y) => _player = new Vector2(x, y);

        internal void TestSetMushroom(int column, int row, byte health)
        {
            if (column < 0 || column >= Columns || row < 0 || row >= Rows)
                return;

            if (_mushrooms[row, column] == 0 && health > 0)
                _mushroomCount++;
            else if (_mushrooms[row, column] > 0 && health == 0)
                _mushroomCount--;

            _mushrooms[row, column] = health;
        }

        internal void TestSpawnBullet(float x, float y)
        {
            _bulletActive = true;
            _bulletX = x;
            _bulletY = y;
        }

        internal int TestHeadColumn => _chains.Count == 0 ? -1 : _chains[0].Segments[0].Column;

        internal int TestHeadRow => _chains.Count == 0 ? -1 : _chains[0].Segments[0].Row;

        internal void TestSetChain(int column, int row, int length, int direction)
        {
            _chains.Clear();
            var chain = new Chain { Direction = direction, Vertical = 1 };
            for (int i = 0; i < length; i++)
                chain.Segments.Add(new Segment { Column = Math.Clamp(column - direction * i, 0, Columns - 1), Row = row });
            _chains.Add(chain);
        }

        internal void TestClearChains() => _chains.Clear();

        internal void TestSetInvulnerable(bool value) => _invulnerableTimer = value ? 5.0 : 0;

        internal void TestSpawnSpider(float x, float y, float vx, float vy)
        {
            _spiders.Add(new Spider { Position = new Vector2(x, y), Velocity = new Vector2(vx, vy), Life = 30 });
        }
    }
}
