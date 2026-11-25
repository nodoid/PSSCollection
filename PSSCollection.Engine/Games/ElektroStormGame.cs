using System;
using System.Collections.Generic;
using System.Numerics;

namespace PSSCollection.Engine.Games
{
    /// <summary>
    /// PSS's Elektro Storm - a Missile Command game. Enemy missiles fall on six cities; you
    /// aim a crosshair, fire counter-missiles from the three batteries and burst them in the
    /// air. Cities are your lives, ammo is limited and each wave falls faster than the last.
    /// </summary>
    public sealed class ElektroStormGame : GameBase
    {
        public const float Width = 1120f;
        public const float Height = 616f;
        public const float GroundY = 520f;

        public const int CityCount = 6;
        public const int SiloCount = 3;
        public const float CityWidth = 88f;
        public const float SiloWidth = 80f;
        public const int AmmoPerSilo = 10;

        public const float CrosshairSpeed = 430f;
        public const float MissileSpeed = 720f;
        public const float BlastRadius = 74f;
        public const double BlastSeconds = 0.78;
        public const int MissilePoints = 25;
        public const int AmmoBonus = 5;
        public const int CityBonus = 100;
        public const double WaveGap = 2.0;

        private static readonly float[] CityX = { 30f, 215f, 400f, 585f, 770f, 955f };
        private static readonly float[] SiloX = { 128f, 498f, 868f };

        private sealed class Incoming
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public int TargetIndex;
            public bool TargetsCity;
        }

        private sealed class Blast
        {
            public Vector2 Centre;
            public double Age;
            public float Radius;
        }

        private sealed class Shot
        {
            public Vector2 Position;
            public Vector2 Target;
            public Vector2 Velocity;
        }

        private readonly List<Entity> _entities = new List<Entity>();
        private readonly List<Incoming> _incoming = new List<Incoming>();
        private readonly List<Blast> _blasts = new List<Blast>();
        private readonly List<Shot> _shots = new List<Shot>();
        private readonly bool[] _cities = new bool[CityCount];
        private readonly int[] _ammo = new int[SiloCount];

        private Vector2 _crosshair;
        private int _toSpawn;
        private double _spawnTimer;
        private double _waveGap;
        private int _citiesLeft;

        public override GameId Id => GameId.ElektroStorm;

        public override Vector2 PlayfieldSize => new Vector2(Width, Height);

        public override IReadOnlyList<Entity> Entities => _entities;

        public Vector2 Crosshair => _crosshair;

        public int CitiesLeft => _citiesLeft;

        public int IncomingCount => _incoming.Count;

        public int BlastCount => _blasts.Count;

        public int PendingSpawns => _toSpawn;

        public int AmmoLeft
        {
            get
            {
                int total = 0;
                foreach (int ammo in _ammo)
                    total += ammo;
                return total;
            }
        }

        public bool IsBetweenWaves => _waveGap > 0;

        public double SpawnInterval => Math.Max(0.32, (1.5 - 0.05 * Level) / SkillLevels.EnemySpeed(Skill));

        public double MissileSpeedForLevel =>
            (58.0 + 4.5 * Level) * SkillLevels.EnemySpeed(Skill);

        protected override void OnStart()
        {
            for (int i = 0; i < CityCount; i++)
                _cities[i] = true;

            _citiesLeft = CityCount;
            Lives = CityCount;
            _crosshair = new Vector2(Width * 0.5f, GroundY - 150f);
            _waveGap = 0;
            StartWave();
            BuildEntities();
        }

        private void StartWave()
        {
            for (int i = 0; i < SiloCount; i++)
                _ammo[i] = AmmoPerSilo;

            _incoming.Clear();
            _blasts.Clear();
            _shots.Clear();
            _toSpawn = Math.Min(40, 6 + Level * 2 + SkillLevels.ExtraStartingEnemies(Skill) * 3);
            _spawnTimer = 0.6;
            _waveGap = 0;
            ShowMessage("WAVE " + Level, 1.4);
        }

        protected override void UpdateCore(double deltaSeconds, InputState input)
        {
            if (_waveGap > 0)
            {
                _waveGap -= deltaSeconds;
                if (_waveGap <= 0)
                    StartWave();
                BuildEntities();
                return;
            }

            MoveCrosshair(deltaSeconds, input);

            if (input.FirePressed)
                Fire(_crosshair);

            foreach (Shot shot in _shots.ToArray())
                AdvanceShot(shot, deltaSeconds);

            foreach (Incoming missile in _incoming.ToArray())
                AdvanceIncoming(missile, deltaSeconds);

            AdvanceBlasts(deltaSeconds);
            SpawnIncoming(deltaSeconds);

            if (_incoming.Count == 0 && _toSpawn == 0 && _blasts.Count == 0 && _shots.Count == 0)
            {
                Award(AmmoBonus * AmmoLeft + CityBonus * _citiesLeft);
                Level++;
                _waveGap = WaveGap;
                Queue(SoundId.WaveClear);
                ShowMessage("WAVE CLEAR", 1.6);
            }

            BuildEntities();
        }

        private void MoveCrosshair(double deltaSeconds, InputState input)
        {
            float dx = 0;
            float dy = 0;
            if (input.Left) dx -= 1;
            if (input.Right) dx += 1;
            if (input.Up) dy -= 1;
            if (input.Down) dy += 1;

            if (dx == 0 && dy == 0)
                return;

            _crosshair += new Vector2(dx, dy) * (float)(CrosshairSpeed * deltaSeconds);
            ClampCrosshair();
        }

        private void ClampCrosshair()
        {
            _crosshair.X = Math.Clamp(_crosshair.X, 8f, Width - 8f);
            _crosshair.Y = Math.Clamp(_crosshair.Y, 14f, GroundY - 24f);
        }

        /// <summary>Places the crosshair, for a touch screen or the mouse.</summary>
        public void AimAt(float x, float y)
        {
            _crosshair = new Vector2(x, y);
            ClampCrosshair();
        }

        /// <summary>Aims and fires in one go - a tap on a touch screen.</summary>
        public void Target(float x, float y)
        {
            AimAt(x, y);
            Fire(_crosshair);
        }

        private void Fire(Vector2 target)
        {
            int silo = -1;
            float best = float.MaxValue;
            for (int i = 0; i < SiloCount; i++)
            {
                if (_ammo[i] <= 0)
                    continue;

                float distance = Math.Abs(SiloX[i] + SiloWidth * 0.5f - target.X);
                if (distance < best)
                {
                    best = distance;
                    silo = i;
                }
            }

            if (silo < 0)
                return;

            _ammo[silo]--;
            var origin = new Vector2(SiloX[silo] + SiloWidth * 0.5f, GroundY - 6f);
            Vector2 delta = target - origin;
            float length = Math.Max(1f, delta.Length());

            _shots.Add(new Shot
            {
                Position = origin,
                Target = target,
                Velocity = delta / length * MissileSpeed
            });

            Queue(SoundId.Shoot);
        }

        private void AdvanceShot(Shot shot, double deltaSeconds)
        {
            shot.Position += shot.Velocity * (float)deltaSeconds;

            bool arrived = Vector2.Distance(shot.Position, shot.Target) < 10f;
            if (!arrived && shot.Position.Y < 0)
                arrived = true;

            if (!arrived)
                return;

            _shots.Remove(shot);
            _blasts.Add(new Blast { Centre = shot.Target, Radius = 0 });
            Queue(SoundId.Explosion);
        }

        private void AdvanceIncoming(Incoming missile, double deltaSeconds)
        {
            missile.Position += missile.Velocity * (float)deltaSeconds;

            if (missile.Position.Y < GroundY)
                return;

            _incoming.Remove(missile);
            _blasts.Add(new Blast { Centre = new Vector2(missile.Position.X, GroundY), Radius = 0 });
            Queue(SoundId.Explosion);
            Impact(missile);
        }

        private void Impact(Incoming missile)
        {
            if (missile.TargetsCity)
            {
                if (missile.TargetIndex < 0 || missile.TargetIndex >= CityCount || !_cities[missile.TargetIndex])
                    return;

                _cities[missile.TargetIndex] = false;
                _citiesLeft--;
                LoseLife();
                ShowMessage("CITY LOST", 1.4);

                if (_citiesLeft <= 0)
                    EndGame();
            }
            else if (missile.TargetIndex >= 0 && missile.TargetIndex < SiloCount)
            {
                _ammo[missile.TargetIndex] = 0;
            }
        }

        private void AdvanceBlasts(double deltaSeconds)
        {
            foreach (Blast blast in _blasts.ToArray())
            {
                blast.Age += deltaSeconds;
                float t = (float)(blast.Age / BlastSeconds);
                blast.Radius = BlastRadius * MathF.Sin(Math.Clamp(t, 0f, 1f) * MathF.PI);

                foreach (Incoming missile in _incoming.ToArray())
                {
                    if (Vector2.Distance(missile.Position, blast.Centre) > blast.Radius)
                        continue;

                    _incoming.Remove(missile);
                    Award(MissilePoints);
                    Queue(SoundId.EnemyDestroyed);
                }

                if (blast.Age >= BlastSeconds)
                    _blasts.Remove(blast);
            }
        }

        private void SpawnIncoming(double deltaSeconds)
        {
            if (_toSpawn <= 0)
                return;

            _spawnTimer -= deltaSeconds;
            if (_spawnTimer > 0)
                return;

            _spawnTimer = SpawnInterval;

            bool targetCity = Rng.Next(2) == 0;
            int index;
            float targetX;

            if (targetCity)
            {
                var alive = new List<int>();
                for (int i = 0; i < CityCount; i++)
                    if (_cities[i])
                        alive.Add(i);

                if (alive.Count == 0)
                    return;

                index = alive[Rng.Next(alive.Count)];
                targetX = CityX[index] + CityWidth * 0.5f;
            }
            else
            {
                index = Rng.Next(SiloCount);
                targetX = SiloX[index] + SiloWidth * 0.5f;
            }

            float startX = (float)Rng.Range(30.0, Width - 30.0);
            var start = new Vector2(startX, 0f);
            var target = new Vector2(targetX, GroundY);
            Vector2 delta = target - start;
            float length = Math.Max(1f, delta.Length());
            float speed = (float)MissileSpeedForLevel;

            _incoming.Add(new Incoming
            {
                Position = start,
                Velocity = delta / length * speed,
                TargetIndex = index,
                TargetsCity = targetCity
            });

            _toSpawn--;
            Queue(SoundId.EnemyShoot);
        }

        private void BuildEntities()
        {
            _entities.Clear();

            for (int i = 0; i < CityCount; i++)
            {
                _entities.Add(new Entity(EntityKind.City,
                    new Vector2(CityX[i], GroundY + 6),
                    new Vector2(CityWidth, 46), _cities[i] ? 0 : 1));
            }

            for (int i = 0; i < SiloCount; i++)
            {
                _entities.Add(new Entity(EntityKind.Silo,
                    new Vector2(SiloX[i], GroundY + 16),
                    new Vector2(SiloWidth, 30), _ammo[i] > 0 ? _ammo[i] : 0));
            }

            foreach (Incoming missile in _incoming)
            {
                float rotation = MathF.Atan2(missile.Velocity.Y, missile.Velocity.X);
                _entities.Add(new Entity(EntityKind.Enemy, missile.Position - new Vector2(5, 5),
                    new Vector2(10, 10), 0, rotation));
            }

            foreach (Shot shot in _shots)
            {
                float rotation = MathF.Atan2(shot.Velocity.Y, shot.Velocity.X);
                _entities.Add(new Entity(EntityKind.Bullet, shot.Position - new Vector2(4, 4),
                    new Vector2(8, 8), 1, rotation));
            }

            foreach (Blast blast in _blasts)
            {
                float size = Math.Max(6f, blast.Radius * 2);
                _entities.Add(new Entity(EntityKind.Explosion,
                    blast.Centre - new Vector2(size * 0.5f, size * 0.5f),
                    new Vector2(size, size), 1));
            }

            _entities.Add(new Entity(EntityKind.Crosshair,
                _crosshair - new Vector2(18, 18), new Vector2(36, 36)));
        }

        // ---- Test support -------------------------------------------------------------

        internal void TestClearIncoming()
        {
            _incoming.Clear();
            _toSpawn = 0;
        }

        internal void TestSpawnMissile(float startX, float targetX, bool city, int index)
        {
            var start = new Vector2(startX, 0f);
            var target = new Vector2(targetX, GroundY);
            Vector2 delta = target - start;
            float length = Math.Max(1f, delta.Length());

            _incoming.Add(new Incoming
            {
                Position = start,
                Velocity = delta / length * (float)MissileSpeedForLevel,
                TargetsCity = city,
                TargetIndex = index
            });

            BuildEntities();
        }

        internal void TestFireAt(float x, float y)
        {
            AimAt(x, y);
            Fire(new Vector2(x, y));
        }

        internal void TestSetAmmo(int silo, int ammo) => _ammo[silo] = ammo;

        internal void TestDestroyCity(int index)
        {
            if (!_cities[index])
                return;

            _cities[index] = false;
            _citiesLeft--;
            LoseLife();
            if (_citiesLeft <= 0)
                EndGame();
        }

        internal void TestSetCrosshair(float x, float y) => AimAt(x, y);

        internal float CityCentreX(int index) => CityX[index] + CityWidth * 0.5f;

        internal float SiloCentreX(int index) => SiloX[index] + SiloWidth * 0.5f;
    }
}
