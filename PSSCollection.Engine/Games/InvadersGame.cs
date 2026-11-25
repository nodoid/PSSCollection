using System;
using System.Collections.Generic;
using System.Numerics;

namespace PSSCollection.Engine.Games
{
    /// <summary>
    /// PSS's Oric Invaders: a formation of aliens steps across the screen and down towards
    /// four eroding bunkers, dropping bombs. Clear a wave and a bigger, faster one arrives.
    /// </summary>
    public sealed class InvadersGame : GameBase
    {
        public const float Width = 1000f;
        public const float Height = 700f;
        public const int Columns = 11;
        public const int MaxRows = 6;
        public const float AlienWidth = 44f;
        public const float AlienHeight = 30f;
        public const float AlienSpacingX = 58f;
        public const float AlienSpacingY = 46f;
        public const float FormationTop = 84f;
        public const float FormationLeft = 96f;
        public const float AlienStepX = 10f;
        public const float AlienDropY = 24f;
        public const float PlayerWidth = 52f;
        public const float PlayerHeight = 26f;
        public const float PlayerY = 636f;
        public const float PlayerSpeed = 430f;
        public const float BulletSpeed = 900f;
        public const float BombSpeed = 380f;
        public const float BunkerTop = 544f;
        public const float BunkerWidth = 96f;
        public const float BunkerHeight = 45f;
        public const int BunkerColumns = 8;
        public const int BunkerRows = 3;
        public const int BunkerCount = 4;
        public const double PlayerFireCooldown = 0.26;
        public const int BaseRows = 5;
        public const int UfoPointsLow = 50;
        public const int UfoPointsHigh = 300;

        private sealed class Bunker
        {
            public float X;
            public readonly bool[,] Blocks = new bool[BunkerRows, BunkerColumns];

            public void Fill()
            {
                for (int r = 0; r < BunkerRows; r++)
                    for (int c = 0; c < BunkerColumns; c++)
                        Blocks[r, c] = true;
            }

            public int BlockCount
            {
                get
                {
                    int count = 0;
                    for (int r = 0; r < BunkerRows; r++)
                        for (int c = 0; c < BunkerColumns; c++)
                            if (Blocks[r, c])
                                count++;
                    return count;
                }
            }
        }

        private readonly List<Entity> _entities = new List<Entity>();
        private readonly bool[,] _aliens = new bool[MaxRows, Columns];
        private readonly List<Vector2> _bombs = new List<Vector2>();
        private readonly List<Bunker> _bunkers = new List<Bunker>();

        private int _rows;
        private float _formationX;
        private float _formationY;
        private int _direction = 1;
        private double _stepTimer;
        private double _alienFireTimer;
        private float _playerX;
        private double _fireCooldown;
        private float _bulletX;
        private float _bulletY;
        private bool _bulletActive;
        private double _ufoTimer;
        private bool _ufoActive;
        private float _ufoX;
        private int _ufoPoints;
        private double _respawnTimer;
        private double _invulnerableTimer;

        public override GameId Id => GameId.Invaders;

        public override Vector2 PlayfieldSize => new Vector2(Width, Height);

        public override IReadOnlyList<Entity> Entities => _entities;

        public int AlienCount { get; private set; }

        public float FormationX => _formationX;

        public float FormationY => _formationY;

        public int Direction => _direction;

        public int CurrentRows => _rows;

        public bool BulletActive => _bulletActive;

        public bool UfoActive => _ufoActive;

        public float UfoX => _ufoX;

        public int UfoValue => _ufoPoints;

        public bool PlayerInvulnerable => _respawnTimer > 0 || _invulnerableTimer > 0;

        public Vector2 PlayerPosition => new Vector2(_playerX, PlayerY);

        public IReadOnlyList<Vector2> Bombs => _bombs;

        public int BunkerBlockCount
        {
            get
            {
                int total = 0;
                foreach (Bunker bunker in _bunkers)
                    total += bunker.BlockCount;
                return total;
            }
        }

        protected override void OnStart()
        {
            StartWave(1);
        }

        private void StartWave(int level)
        {
            _rows = Math.Min(BaseRows + (level - 1) / 3, MaxRows);
            for (int row = 0; row < MaxRows; row++)
                for (int column = 0; column < Columns; column++)
                    _aliens[row, column] = row < _rows;

            CountAliens();

            _formationX = 0f;
            _formationY = 0f;
            _direction = 1;
            _stepTimer = 0;
            _alienFireTimer = 1.6;
            _playerX = Width * 0.5f - PlayerWidth * 0.5f;
            _fireCooldown = 0;
            _bulletActive = false;
            _bombs.Clear();
            _respawnTimer = 0;
            _invulnerableTimer = 2.0;
            _ufoActive = false;
            _ufoX = -100f;
            _ufoTimer = Rng.Range(12.0, 22.0);
            _ufoPoints = 50;

            BuildBunkers();
            RebuildEntities();
        }

        private void BuildBunkers()
        {
            _bunkers.Clear();
            const float gap = (Width - BunkerCount * BunkerWidth) / (BunkerCount + 1);
            for (int i = 0; i < BunkerCount; i++)
            {
                var bunker = new Bunker { X = gap + i * (BunkerWidth + gap) };
                bunker.Fill();
                _bunkers.Add(bunker);
            }
        }

        private void CountAliens()
        {
            int count = 0;
            for (int row = 0; row < MaxRows; row++)
                for (int column = 0; column < Columns; column++)
                    if (_aliens[row, column])
                        count++;

            AlienCount = count;
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

            UpdateBullet(deltaSeconds);
            UpdateFormation(deltaSeconds);
            UpdateAlienFire(deltaSeconds);
            UpdateBombs(deltaSeconds);
            UpdateUfo(deltaSeconds);

            CheckWaveState();
            RebuildEntities();
        }

        private void MovePlayer(double deltaSeconds, InputState input)
        {
            float distance = (float)(PlayerSpeed * deltaSeconds);
            if (input.Left && !input.Right)
                _playerX -= distance;
            else if (input.Right && !input.Left)
                _playerX += distance;

            _playerX = Math.Clamp(_playerX, 0, Width - PlayerWidth);
        }

        private void HandleFiring(double deltaSeconds, InputState input)
        {
            _fireCooldown = Math.Max(0, _fireCooldown - deltaSeconds);
            if (!input.Fire || _bulletActive || _fireCooldown > 0)
                return;

            _bulletActive = true;
            _bulletX = _playerX + PlayerWidth * 0.5f - 3f;
            _bulletY = PlayerY - 14f;
            _fireCooldown = PlayerFireCooldown;
            Queue(SoundId.Shoot);
        }

        private void UpdateBullet(double deltaSeconds)
        {
            if (!_bulletActive)
                return;

            _bulletY -= (float)(BulletSpeed * deltaSeconds);

            if (_bulletY < -20f)
            {
                _bulletActive = false;
                return;
            }

            // Aliens first, highest score for the top row.
            for (int row = 0; row < MaxRows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    if (!_aliens[row, column])
                        continue;

                    var alien = new Entity(EntityKind.Alien, AlienPosition(row, column), new Vector2(AlienWidth, AlienHeight));
                    if (!Entity.Overlap(new Vector2(_bulletX, _bulletY), new Vector2(6, 16), alien.Position, alien.Size))
                        continue;

                    _aliens[row, column] = false;
                    CountAliens();
                    Award(PointsForRow(row));
                    Queue(SoundId.EnemyDestroyed);
                    _bulletActive = false;
                    CheckWaveState();
                    return;
                }
            }

            if (_ufoActive)
            {
                var ufo = new Entity(EntityKind.Ufo, new Vector2(_ufoX, 36f), new Vector2(64f, 28f));
                if (Entity.Overlap(new Vector2(_bulletX, _bulletY), new Vector2(6, 16), ufo.Position, ufo.Size))
                {
                    Award(_ufoPoints);
                    _ufoActive = false;
                    _bulletActive = false;
                    Queue(SoundId.Explosion);
                    ShowMessage("+" + Math.Round(_ufoPoints * ScoreMultiplier), 1.0);
                    return;
                }
            }

            if (DamageBunker(_bulletX + 3f, _bulletY))
                _bulletActive = false;
        }

        /// <summary>Erodes one bunker block, plus its neighbours, at the given point.</summary>
        private bool DamageBunker(float x, float y)
        {
            foreach (Bunker bunker in _bunkers)
            {
                if (x < bunker.X || x > bunker.X + BunkerWidth || y < BunkerTop || y > BunkerTop + BunkerHeight)
                    continue;

                int column = (int)((x - bunker.X) / (BunkerWidth / BunkerColumns));
                int row = (int)((y - BunkerTop) / (BunkerHeight / BunkerRows));
                if (column < 0 || column >= BunkerColumns || row < 0 || row >= BunkerRows)
                    return false;

                if (!bunker.Blocks[row, column])
                    return false;

                bunker.Blocks[row, column] = false;

                // Amateur "splash": take a neighbour out too, so bunkers erode visibly.
                if (column > 0 && Rng.Next(2) == 0)
                    bunker.Blocks[row, column - 1] = false;
                if (column < BunkerColumns - 1 && Rng.Next(2) == 0)
                    bunker.Blocks[row, column + 1] = false;
                if (row > 0 && Rng.Next(2) == 0)
                    bunker.Blocks[row - 1, column] = false;

                return true;
            }

            return false;
        }

        private void UpdateFormation(double deltaSeconds)
        {
            if (AlienCount == 0)
                return;

            double baseInterval = 0.055 + 0.55 * (AlienCount / (double)(_rows * Columns));
            double interval = baseInterval / SkillLevels.EnemySpeed(Skill) / (1.0 + 0.05 * (Level - 1));
            interval = Math.Max(0.035, interval);

            _stepTimer += deltaSeconds;
            while (_stepTimer >= interval)
            {
                _stepTimer -= interval;
                StepFormation();
                if (AlienCount == 0)
                    break;
            }
        }

        private void StepFormation()
        {
            float left = FormationLeft + _formationX;
            float right = left + (Columns - 1) * AlienSpacingX + AlienWidth;

            if (_direction > 0 && right + AlienStepX > Width - 8)
            {
                _direction = -1;
                _formationY += AlienDropY;
                return;
            }

            if (_direction < 0 && left - AlienStepX < 8)
            {
                _direction = 1;
                _formationY += AlienDropY;
                return;
            }

            _formationX += AlienStepX * _direction;
        }

        private void UpdateAlienFire(double deltaSeconds)
        {
            if (AlienCount == 0 || _bombs.Count > 6)
                return;

            _alienFireTimer -= deltaSeconds;
            if (_alienFireTimer > 0)
                return;

            double rate = SkillLevels.EnemyFireRate(Skill) * (1.0 + 0.08 * (Level - 1));
            _alienFireTimer = Rng.Range(0.5, 1.5) / Math.Max(0.2, rate);
            _alienFireTimer = Math.Max(0.22, _alienFireTimer);

            // Fire from the lowest alien in a random occupied column.
            int column = Rng.Next(Columns);
            int shooterRow = -1;
            for (int row = MaxRows - 1; row >= 0; row--)
            {
                if (_aliens[row, column])
                {
                    shooterRow = row;
                    break;
                }
            }

            if (shooterRow < 0)
                return;

            Vector2 origin = AlienPosition(shooterRow, column);
            _bombs.Add(new Vector2(origin.X + AlienWidth * 0.5f, origin.Y + AlienHeight));
            Queue(SoundId.EnemyShoot);
        }

        private void UpdateBombs(double deltaSeconds)
        {
            float step = (float)(BombSpeed * SkillLevels.EnemySpeed(Skill) * deltaSeconds);

            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                Vector2 bomb = _bombs[i];
                bomb.Y += step;

                if (bomb.Y > Height + 16)
                {
                    _bombs.RemoveAt(i);
                    continue;
                }

                if (!PlayerInvulnerable &&
                    Entity.Overlap(bomb, new Vector2(6, 14), new Vector2(_playerX, PlayerY), new Vector2(PlayerWidth, PlayerHeight)))
                {
                    // HitPlayer clears the bomb list, so the loop must stop rather than index it again.
                    _bombs.RemoveAt(i);
                    HitPlayer();
                    break;
                }

                if (DamageBunker(bomb.X + 3f, bomb.Y + 14f))
                {
                    _bombs.RemoveAt(i);
                    continue;
                }

                _bombs[i] = bomb;
            }
        }

        private void UpdateUfo(double deltaSeconds)
        {
            if (_ufoActive)
            {
                _ufoX += (float)(130 * deltaSeconds);
                if (_ufoX > Width + 40)
                    _ufoActive = false;
                return;
            }

            _ufoTimer -= deltaSeconds;
            if (_ufoTimer > 0)
                return;

            _ufoActive = true;
            _ufoX = -70f;
            _ufoTimer = Rng.Range(16.0, 28.0);

            int choice = Rng.Next(4);
            _ufoPoints = choice switch
            {
                0 => 50,
                1 => 100,
                2 => 150,
                _ => 300
            };
        }

        private void HitPlayer()
        {
            if (PlayerInvulnerable)
                return;

            LoseLife();
            _bulletActive = false;
            _bombs.Clear();

            if (Lives <= 0)
            {
                EndGame();
                return;
            }

            _respawnTimer = 1.4;
            _playerX = Width * 0.5f - PlayerWidth * 0.5f;
            ShowMessage("SHIELDS DOWN", 1.2);
        }

        private void CheckWaveState()
        {
            if (IsGameOver || _respawnTimer > 0)
                return;

            float lowest = 0;
            for (int row = 0; row < MaxRows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    if (!_aliens[row, column])
                        continue;
                    lowest = Math.Max(lowest, AlienPosition(row, column).Y + AlienHeight);
                }
            }

            if (lowest >= PlayerY - 6)
            {
                Lives = 0;
                EndGame();
                return;
            }

            if (AlienCount == 0)
            {
                CompleteWave(150 + 50 * Level);
                StartWave(Level);
                _invulnerableTimer = 2.0;
            }
        }

        private static int PointsForRow(int row)
        {
            switch (row)
            {
                case 0: return 30;
                case 1: return 20;
                case 2: return 20;
                case 3: return 15;
                default: return 10;
            }
        }

        public Vector2 AlienPosition(int row, int column) => new Vector2(
            FormationLeft + _formationX + column * AlienSpacingX,
            FormationTop + _formationY + row * AlienSpacingY);

        private void RebuildEntities()
        {
            _entities.Clear();

            if (_respawnTimer <= 0 && !IsGameOver)
            {
                bool flash = _invulnerableTimer > 0 && ((int)(Elapsed * 12) % 2 == 0);
                _entities.Add(new Entity(EntityKind.Player, new Vector2(_playerX, PlayerY),
                    new Vector2(PlayerWidth, PlayerHeight), 0, 0f, !flash));
            }

            for (int row = 0; row < MaxRows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    if (_aliens[row, column])
                        _entities.Add(new Entity(EntityKind.Alien, AlienPosition(row, column),
                            new Vector2(AlienWidth, AlienHeight), row % 3));
                }
            }

            foreach (Bunker bunker in _bunkers)
            {
                float blockWidth = BunkerWidth / BunkerColumns;
                float blockHeight = BunkerHeight / BunkerRows;
                for (int row = 0; row < BunkerRows; row++)
                {
                    for (int column = 0; column < BunkerColumns; column++)
                    {
                        if (!bunker.Blocks[row, column])
                            continue;

                        _entities.Add(new Entity(EntityKind.Shield,
                            new Vector2(bunker.X + column * blockWidth, BunkerTop + row * blockHeight),
                            new Vector2(blockWidth, blockHeight), 0));
                    }
                }
            }

            if (_bulletActive)
                _entities.Add(new Entity(EntityKind.Bullet, new Vector2(_bulletX, _bulletY), new Vector2(6, 16)));

            foreach (Vector2 bomb in _bombs)
                _entities.Add(new Entity(EntityKind.Bomb, bomb, new Vector2(6, 14)));

            if (_ufoActive)
                _entities.Add(new Entity(EntityKind.Ufo, new Vector2(_ufoX, 36f), new Vector2(64f, 28f)));
        }

        // ---- Test support -------------------------------------------------------------

        internal void TestPlacePlayer(float x) => _playerX = Math.Clamp(x, 0, Width - PlayerWidth);

        internal void TestSetFormation(float x, float y)
        {
            _formationX = x;
            _formationY = y;
        }

        internal void TestSetAlien(int row, int column, bool alive)
        {
            if (row < 0 || row >= MaxRows || column < 0 || column >= Columns)
                return;

            _aliens[row, column] = alive;
            _rows = Math.Max(_rows, row + 1);
            CountAliens();
        }

        internal void TestClearAliens()
        {
            for (int row = 0; row < MaxRows; row++)
                for (int column = 0; column < Columns; column++)
                    _aliens[row, column] = false;
            AlienCount = 0;
        }

        internal void TestSpawnBullet(float x, float y)
        {
            _bulletActive = true;
            _bulletX = x;
            _bulletY = y;
        }

        internal void TestSpawnBomb(float x, float y) => _bombs.Add(new Vector2(x, y));

        internal void TestSpawnUfo(float x, int points)
        {
            _ufoActive = true;
            _ufoX = x;
            _ufoPoints = points;
        }

        internal void TestSetInvulnerable(bool value)
        {
            _invulnerableTimer = value ? 5.0 : 0;
            if (!value)
                _respawnTimer = 0;
        }

        internal void TestClearBombs() => _bombs.Clear();

        internal void TestClearRespawn() => _respawnTimer = 0;

        internal double TestStepInterval
        {
            get
            {
                double baseInterval = 0.055 + 0.55 * (AlienCount / (double)(_rows * Columns));
                double interval = baseInterval / SkillLevels.EnemySpeed(Skill) / (1.0 + 0.05 * (Level - 1));
                return Math.Max(0.035, interval);
            }
        }
    }
}
