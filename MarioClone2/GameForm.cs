using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MarioClone2;

// Main window that owns gameplay state, simulation updates, and input handling.
internal sealed partial class GameForm : Form
{
    // Fixed-rate update timer (~60 FPS).
    private readonly System.Windows.Forms.Timer _timer;
    // High-resolution clock used to calculate frame delta time.
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // Tracks currently pressed keys for polling-style movement input.
    private readonly HashSet<Keys> _keysDown = new();
    private readonly List<LevelDefinition> _levelDefinitions = LevelFactory.Create();
    private readonly SpriteAtlas _sprites = new();
    private readonly GameAudio _audio;
    private readonly Bitmap _skyLayer;
    private readonly Bitmap _cloudLayer;
    private readonly Bitmap _mountainLayer;
    // Short-lived visual FX containers updated every frame.
    private readonly List<BrickDebrisPiece> _brickDebris = [];
    private readonly List<CoinPopEffect> _coinPops = [];
    private readonly Dictionary<int, int> _activeCheckpointByLevel = [];

    private LevelRuntime _level = null!;
    private Player _player = null!;
    private int _levelIndex;
    private int _unlockedLevelIndex;
    private float _cameraX;
    private double _lastTickSeconds;
    private float _animTime;
    private bool _jumpWasDown;
    private GamePhase _phase = GamePhase.Playing;

    private int _score;
    private int _coinCount;
    private int _lives = 3;
    private string _status = string.Empty;

    public GameForm()
    {
        Text = "Mario Clone 2 - Arrow Keys / WASD + Space";
        ClientSize = new Size(GameConstants.ViewWidth, GameConstants.ViewHeight);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        DoubleBuffered = true;
        KeyPreview = true;

        // Enable explicit double buffering flags to reduce flicker on redraw.
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);

        _skyLayer = CreateSkyLayer(1920, GameConstants.ViewHeight);
        _cloudLayer = CreateCloudLayer(1920, GameConstants.ViewHeight);
        _mountainLayer = CreateMountainLayer(1920, GameConstants.ViewHeight);
        _audio = new GameAudio();
        _audio.PlayMusic();

        ApplyLoadedSaveState(SaveStore.LoadOrDefault());
        LoadLevel(_levelIndex);

        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += OnTick;
        _timer.Start();

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _sprites.Dispose();
            _audio.Dispose();
            _skyLayer.Dispose();
            _cloudLayer.Dispose();
            _mountainLayer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = (float)(now - _lastTickSeconds);
        _lastTickSeconds = now;

        if (dt <= 0f)
        {
            return;
        }

        // Clamp long frames so physics/collision remain stable after stalls.
        if (dt > 0.033f)
        {
            dt = 0.033f;
        }

        _animTime += dt;
        UpdateTransientEffects(dt);

        if (_phase == GamePhase.Playing)
        {
            UpdateGame(dt);
        }

        Invalidate();
    }

    private void UpdateGame(float dt)
    {
        if (_player.DamageCooldownSeconds > 0f)
        {
            _player.DamageCooldownSeconds = Math.Max(0f, _player.DamageCooldownSeconds - dt);
        }

        var left = IsDown(Keys.Left, Keys.A);
        var right = IsDown(Keys.Right, Keys.D);
        var jumpDown = IsDown(Keys.Space, Keys.Up, Keys.W);
        var jumpPressedThisFrame = jumpDown && !_jumpWasDown;
        var sprintDown = IsDown(Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey);

        // Resolve opposing keys into one horizontal movement direction.
        var move = 0;
        if (left)
        {
            move -= 1;
        }

        if (right)
        {
            move += 1;
        }

        _player.Sprinting = sprintDown && move != 0 && _player.OnGround;
        UpdateJumpWindows(dt, jumpPressedThisFrame);

        if (move != 0)
        {
            _player.Facing = move;
            var accel = _player.Sprinting
                ? GameConstants.SprintAcceleration
                : GameConstants.WalkAcceleration;
            _player.Vx += move * accel * dt;
        }
        else
        {
            var friction = _player.OnGround ? GameConstants.GroundFriction : GameConstants.AirDrag;
            _player.Vx = MoveToward(_player.Vx, 0f, friction * dt);
        }

        if (!_player.Sprinting && move != 0 && MathF.Abs(_player.Vx) > GameConstants.WalkMaxRunSpeed)
        {
            var targetSpeed = MathF.Sign(_player.Vx) * GameConstants.WalkMaxRunSpeed;
            var decay = _player.OnGround ? GameConstants.GroundFriction : GameConstants.AirDrag;
            _player.Vx = MoveToward(_player.Vx, targetSpeed, decay * dt);
        }

        _player.Vx = Math.Clamp(_player.Vx, -GameConstants.SprintMaxRunSpeed, GameConstants.SprintMaxRunSpeed);
        TryConsumeBufferedJump();

        // Jump-cut behavior: releasing jump early shortens the jump arc.
        if (!jumpDown && _jumpWasDown && _player.Vy < -220f)
        {
            _player.Vy = -220f;
        }

        _jumpWasDown = jumpDown;

        _player.Vy = Math.Min(_player.Vy + GameConstants.Gravity * dt, 920f);

        _player.X += _player.Vx * dt;
        ResolvePlayerHorizontal();

        _player.Y += _player.Vy * dt;
        ResolvePlayerVertical();
        CheckCheckpointActivation();

        if (CheckSpikeHazards())
        {
            return;
        }

        if (_player.Y > _level.PixelHeight + 180f)
        {
            LoseLife();
            return;
        }

        if (UpdateEnemies(dt))
        {
            return;
        }

        CollectPowerups();
        CollectCoins();

        if (_player.X + _player.Width >= _level.FlagX)
        {
            _score += 1000;
            if (_levelIndex + 1 < _levelDefinitions.Count)
            {
                _unlockedLevelIndex = Math.Max(_unlockedLevelIndex, _levelIndex + 1);
                LoadLevel(_levelIndex + 1);
                SaveProgress();
            }
            else
            {
                _unlockedLevelIndex = Math.Max(_unlockedLevelIndex, _levelDefinitions.Count - 1);
                _phase = GamePhase.Won;
                _status = "You Win! Press Enter to play again.";
                SaveProgress();
            }

            return;
        }

        // Keep the player near center while clamping camera to level bounds.
        var maxCamera = Math.Max(0, _level.PixelWidth - ClientSize.Width);
        _cameraX = Math.Clamp(
            _player.X + (_player.Width * 0.5f) - (ClientSize.Width * 0.5f),
            0f,
            maxCamera);
    }

    private void UpdateJumpWindows(float dt, bool jumpPressedThisFrame)
    {
        if (jumpPressedThisFrame)
        {
            _player.JumpBufferTimerSeconds = GameConstants.JumpBufferSeconds;
        }
        else if (_player.JumpBufferTimerSeconds > 0f)
        {
            _player.JumpBufferTimerSeconds = Math.Max(0f, _player.JumpBufferTimerSeconds - dt);
        }

        if (_player.OnGround)
        {
            _player.CoyoteTimerSeconds = GameConstants.CoyoteTimeSeconds;
        }
        else if (_player.CoyoteTimerSeconds > 0f)
        {
            _player.CoyoteTimerSeconds = Math.Max(0f, _player.CoyoteTimerSeconds - dt);
        }
    }

    private void TryConsumeBufferedJump()
    {
        if (_player.JumpBufferTimerSeconds <= 0f)
        {
            return;
        }

        if (!_player.OnGround && _player.CoyoteTimerSeconds <= 0f)
        {
            return;
        }

        _player.Vy = -560f;
        _player.OnGround = false;
        _player.JumpBufferTimerSeconds = 0f;
        _player.CoyoteTimerSeconds = 0f;
        _audio.PlayJump();
    }

    private bool UpdateEnemies(float dt)
    {
        foreach (var enemy in _level.Enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            enemy.Vy = Math.Min(enemy.Vy + GameConstants.Gravity * dt, 920f);

            enemy.X += enemy.Vx * dt;
            var hitWall = ResolveEnemyHorizontal(enemy);
            if (hitWall)
            {
                enemy.Vx *= -1f;
            }

            enemy.Y += enemy.Vy * dt;
            ResolveEnemyVertical(enemy);

            if (enemy.OnGround)
            {
                // Turn around at ledges by probing a tile just ahead of the enemy.
                var aheadX = enemy.Vx > 0f
                    ? enemy.X + enemy.Width + 2f
                    : enemy.X - 2f;
                var footY = enemy.Y + enemy.Height + 1f;
                var aheadTileX = ToTile(aheadX);
                var footTileY = ToTile(footY);
                if (!_level.IsSolid(aheadTileX, footTileY))
                {
                    enemy.Vx *= -1f;
                }
            }

            var playerRect = PlayerRect();
            var enemyRect = EnemyRect(enemy);
            if (!playerRect.IntersectsWith(enemyRect))
            {
                continue;
            }

            // Downward hits near the enemy head count as stomps.
            var stomped = _player.Vy > 50f && (playerRect.Bottom - enemyRect.Top) < 18f;
            if (stomped)
            {
                enemy.Alive = false;
                _score += 200;
                _player.Vy = -360f;
                _audio.PlaySquish();
            }
            else
            {
                if (TryHandlePlayerDamage())
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void CollectCoins()
    {
        var playerRect = PlayerRect();
        foreach (var coin in _level.Coins)
        {
            if (coin.Collected)
            {
                continue;
            }

            var bob = MathF.Sin(_animTime * 8f + coin.PulseOffset) * 3f;
            var centerX = coin.X + (GameConstants.CoinSourceSize * 0.5f);
            var centerY = coin.Y + bob + (GameConstants.CoinSourceSize * 0.5f);
            var coinRect = new RectangleF(
                centerX - (GameConstants.CoinRenderSize * 0.5f),
                centerY - (GameConstants.CoinRenderSize * 0.5f),
                GameConstants.CoinRenderSize,
                GameConstants.CoinRenderSize);
            if (!playerRect.IntersectsWith(coinRect))
            {
                continue;
            }

            coin.Collected = true;
            SpawnCoinPopEffect(centerX, centerY);
            _coinCount += 1;
            _score += 100;
        }
    }

    private void CollectPowerups()
    {
        var playerRect = PlayerRect();
        foreach (var powerup in _level.Powerups)
        {
            if (powerup.Collected)
            {
                continue;
            }

            var pickupRect = new RectangleF(powerup.X, powerup.Y, 20f, 20f);
            if (!playerRect.IntersectsWith(pickupRect))
            {
                continue;
            }

            powerup.Collected = true;
            if (_player.PowerState == PlayerPowerState.Small)
            {
                GrowPlayer();
                _score += 250;
            }
            else
            {
                _score += 150;
                _audio.PlayPowerup();
            }
        }
    }

    private void LoseLife()
    {
        _lives -= 1;
        if (_lives <= 0)
        {
            _lives = 0;
            _phase = GamePhase.GameOver;
            _status = "Game Over. Press Enter to restart.";
            SaveProgress();
            return;
        }

        LoadLevel(_levelIndex);
    }

    private void RestartGame()
    {
        _score = 0;
        _coinCount = 0;
        _lives = 3;
        _levelIndex = 0;
        _phase = GamePhase.Playing;
        _status = string.Empty;
        _activeCheckpointByLevel.Clear();
        LoadLevel(0);
        SaveProgress();
    }

    private void LoadLevel(int index)
    {
        _levelIndex = Math.Clamp(index, 0, _levelDefinitions.Count - 1);
        _level = LevelFactory.CreateRuntime(_levelDefinitions[_levelIndex]);

        if (_activeCheckpointByLevel.TryGetValue(_levelIndex, out var checkpointOrder))
        {
            foreach (var checkpoint in _level.Checkpoints)
            {
                checkpoint.Activated = checkpoint.OrderIndex <= checkpointOrder;
            }
        }

        _player = new Player(GetRespawnSpawnForLevel(_levelIndex, _level))
        {
            CoyoteTimerSeconds = GameConstants.CoyoteTimeSeconds
        };
        _cameraX = 0f;
        _jumpWasDown = false;
        _brickDebris.Clear();
        _coinPops.Clear();
        _phase = GamePhase.Playing;
        _status = string.Empty;
    }

    private void CheckCheckpointActivation()
    {
        if (_level.Checkpoints.Count == 0)
        {
            return;
        }

        var highestOrder = _activeCheckpointByLevel.TryGetValue(_levelIndex, out var activeOrder)
            ? activeOrder
            : 0;
        var playerRect = PlayerRect();
        var newlyActivatedOrder = 0;

        foreach (var checkpoint in _level.Checkpoints)
        {
            if (checkpoint.Activated)
            {
                continue;
            }

            var checkpointRect = new RectangleF(checkpoint.X - 8f, checkpoint.Y - 42f, 20f, 42f);
            if (!playerRect.IntersectsWith(checkpointRect))
            {
                continue;
            }

            newlyActivatedOrder = Math.Max(newlyActivatedOrder, checkpoint.OrderIndex);
        }

        if (newlyActivatedOrder <= highestOrder)
        {
            return;
        }

        _activeCheckpointByLevel[_levelIndex] = newlyActivatedOrder;
        foreach (var checkpoint in _level.Checkpoints)
        {
            checkpoint.Activated = checkpoint.OrderIndex <= newlyActivatedOrder;
        }

        _audio.PlayCheckpoint();
    }

    private void ResolvePlayerHorizontal()
    {
        if (_player.X < 0f)
        {
            _player.X = 0f;
            _player.Vx = 0f;
        }

        // Sweep across potentially overlapping solid tiles after horizontal motion.
        var bounds = PlayerRect();
        var minX = ToTile(bounds.Left);
        var maxX = ToTile(bounds.Right - 0.001f);
        var minY = ToTile(bounds.Top);
        var maxY = ToTile(bounds.Bottom - 0.001f);

        for (var ty = minY; ty <= maxY; ty++)
        {
            for (var tx = minX; tx <= maxX; tx++)
            {
                if (!_level.IsSolid(tx, ty))
                {
                    continue;
                }

                var tileRect = TileRect(tx, ty);
                if (!bounds.IntersectsWith(tileRect))
                {
                    continue;
                }

                if (_player.Vx > 0f)
                {
                    _player.X = tileRect.Left - _player.Width;
                }
                else if (_player.Vx < 0f)
                {
                    _player.X = tileRect.Right;
                }

                _player.Vx = 0f;
                bounds = PlayerRect();
            }
        }
    }

    private void ResolvePlayerVertical()
    {
        _player.OnGround = false;

        var bounds = PlayerRect();
        var minX = ToTile(bounds.Left + 1f);
        var maxX = ToTile(bounds.Right - 1f);
        var minY = ToTile(bounds.Top);
        var maxY = ToTile(bounds.Bottom - 0.001f);

        for (var ty = minY; ty <= maxY; ty++)
        {
            for (var tx = minX; tx <= maxX; tx++)
            {
                if (!_level.IsSolid(tx, ty))
                {
                    continue;
                }

                var tileRect = TileRect(tx, ty);
                if (!bounds.IntersectsWith(tileRect))
                {
                    continue;
                }

                if (_player.Vy > 0f)
                {
                    _player.Y = tileRect.Top - _player.Height;
                    _player.Vy = 0f;
                    _player.OnGround = true;
                }
                else if (_player.Vy < 0f)
                {
                    _player.Y = tileRect.Bottom;
                    _player.Vy = 0f;
                    // Hitting a block from below can consume question blocks.
                    OnBlockBumped(tx, ty);
                }

                bounds = PlayerRect();
            }
        }
    }

    private bool ResolveEnemyHorizontal(Enemy enemy)
    {
        var hitWall = false;
        var bounds = EnemyRect(enemy);
        var minX = ToTile(bounds.Left);
        var maxX = ToTile(bounds.Right - 0.001f);
        var minY = ToTile(bounds.Top);
        var maxY = ToTile(bounds.Bottom - 0.001f);

        for (var ty = minY; ty <= maxY; ty++)
        {
            for (var tx = minX; tx <= maxX; tx++)
            {
                if (!_level.IsSolid(tx, ty))
                {
                    continue;
                }

                var tileRect = TileRect(tx, ty);
                if (!bounds.IntersectsWith(tileRect))
                {
                    continue;
                }

                if (enemy.Vx > 0f)
                {
                    enemy.X = tileRect.Left - enemy.Width;
                    hitWall = true;
                }
                else if (enemy.Vx < 0f)
                {
                    enemy.X = tileRect.Right;
                    hitWall = true;
                }

                bounds = EnemyRect(enemy);
            }
        }

        return hitWall;
    }

    private void ResolveEnemyVertical(Enemy enemy)
    {
        enemy.OnGround = false;

        var bounds = EnemyRect(enemy);
        var minX = ToTile(bounds.Left + 1f);
        var maxX = ToTile(bounds.Right - 1f);
        var minY = ToTile(bounds.Top);
        var maxY = ToTile(bounds.Bottom - 0.001f);

        for (var ty = minY; ty <= maxY; ty++)
        {
            for (var tx = minX; tx <= maxX; tx++)
            {
                if (!_level.IsSolid(tx, ty))
                {
                    continue;
                }

                var tileRect = TileRect(tx, ty);
                if (!bounds.IntersectsWith(tileRect))
                {
                    continue;
                }

                if (enemy.Vy > 0f)
                {
                    enemy.Y = tileRect.Top - enemy.Height;
                    enemy.Vy = 0f;
                    enemy.OnGround = true;
                }
                else if (enemy.Vy < 0f)
                {
                    enemy.Y = tileRect.Bottom;
                    enemy.Vy = 0f;
                }

                bounds = EnemyRect(enemy);
            }
        }
    }

    private void OnBlockBumped(int tx, int ty)
    {
        if (!_level.InBounds(tx, ty))
        {
            return;
        }

        var tile = _level.Tiles[tx, ty];
        if (tile.Type == TileType.Brick)
        {
            tile.Type = TileType.Empty;
            SpawnBrickBreakEffect(tx, ty);
            _score += _player.PowerState == PlayerPowerState.Big ? 125 : 90;
            _audio.PlayBrickBreak();

            return;
        }

        if (tile.Type != TileType.Question || tile.Used)
        {
            return;
        }

        // Question blocks only award once.
        tile.Used = true;
        var tileRect = TileRect(tx, ty);
        SpawnCoinPopEffect(tileRect.Left + 8f, tileRect.Top - 8f);
        _coinCount += 1;
        _score += 100;
    }

    private void SpawnBrickBreakEffect(int tx, int ty)
    {
        var ts = GameConstants.TileSize;
        var tileX = tx * ts;
        var tileY = ty * ts;

        // Four chunk burst: upper shards launch faster than lower shards.
        _brickDebris.Add(new BrickDebrisPiece(tileX + 3f, tileY + 3f, -170f, -330f, 14f, 0f, 0f, 16f));
        _brickDebris.Add(new BrickDebrisPiece(tileX + 17f, tileY + 3f, 170f, -330f, 14f, 16f, 0f, 16f));
        _brickDebris.Add(new BrickDebrisPiece(tileX + 3f, tileY + 17f, -120f, -220f, -12f, 0f, 16f, 16f));
        _brickDebris.Add(new BrickDebrisPiece(tileX + 17f, tileY + 17f, 120f, -220f, 12f, 16f, 16f, 16f));
    }

    private void SpawnCoinPopEffect(float x, float y)
    {
        // Effects are spawned using center coordinates for easier scaling.
        _coinPops.Add(new CoinPopEffect(x, y));
    }

    private void UpdateTransientEffects(float dt)
    {
        // Integrate and cull brick fragments.
        for (var i = _brickDebris.Count - 1; i >= 0; i--)
        {
            var piece = _brickDebris[i];
            piece.Vy += GameConstants.Gravity * 0.72f * dt;
            piece.X += piece.Vx * dt;
            piece.Y += piece.Vy * dt;
            piece.Angle += piece.Spin * dt;
            piece.Age += dt;

            if (piece.Age >= piece.Life)
            {
                _brickDebris.RemoveAt(i);
            }
            else
            {
                _brickDebris[i] = piece;
            }
        }

        // Age out floating coin pop effects.
        for (var i = _coinPops.Count - 1; i >= 0; i--)
        {
            var pop = _coinPops[i];
            pop.Age += dt;
            if (pop.Age >= pop.Life)
            {
                _coinPops.RemoveAt(i);
            }
            else
            {
                _coinPops[i] = pop;
            }
        }
    }

    private bool CheckSpikeHazards()
    {
        var bounds = PlayerRect();
        var minX = ToTile(bounds.Left + 1f);
        var maxX = ToTile(bounds.Right - 1f);
        var minY = ToTile(bounds.Top);
        var maxY = ToTile(bounds.Bottom - 0.001f);

        for (var ty = minY; ty <= maxY; ty++)
        {
            for (var tx = minX; tx <= maxX; tx++)
            {
                if (!_level.InBounds(tx, ty))
                {
                    continue;
                }

                var tile = _level.Tiles[tx, ty];
                if (tile.Type != TileType.Spike)
                {
                    continue;
                }

                var tileRect = TileRect(tx, ty);
                if (!bounds.IntersectsWith(tileRect))
                {
                    continue;
                }

                return TryHandlePlayerDamage();
            }
        }

        return false;
    }

    private bool TryHandlePlayerDamage()
    {
        if (_player.DamageCooldownSeconds > 0f)
        {
            return false;
        }

        if (_player.PowerState == PlayerPowerState.Big)
        {
            ShrinkPlayer();
            _player.DamageCooldownSeconds = 1.1f;
            _player.Vy = -180f;
            return false;
        }

        LoseLife();
        return true;
    }

    private void GrowPlayer()
    {
        if (_player.PowerState == PlayerPowerState.Big)
        {
            return;
        }

        ApplyPowerState(PlayerPowerState.Big);
        _audio.PlayPowerup();
    }

    private void ShrinkPlayer()
    {
        if (_player.PowerState == PlayerPowerState.Small)
        {
            return;
        }

        ApplyPowerState(PlayerPowerState.Small);
        _audio.PlayShrink();
    }

    private void ApplyPowerState(PlayerPowerState state)
    {
        if (_player.PowerState == state)
        {
            return;
        }

        var feet = _player.Y + _player.Height;
        _player.PowerState = state;
        _player.Y = feet - _player.Height;
    }

    private void ApplyLoadedSaveState(SaveData save)
    {
        var maxLevel = Math.Max(0, _levelDefinitions.Count - 1);
        _unlockedLevelIndex = Math.Clamp(save.UnlockedLevelIndex, 0, maxLevel);

        var requestedLevel = Math.Clamp(save.CurrentLevelIndex, 0, maxLevel);
        _levelIndex = Math.Min(requestedLevel, _unlockedLevelIndex);

        _score = Math.Max(0, save.Score);
        _coinCount = Math.Max(0, save.CoinCount);
        _lives = Math.Clamp(save.Lives, 1, 99);
    }

    private void SaveProgress()
    {
        SaveStore.Save(new SaveData
        {
            UnlockedLevelIndex = _unlockedLevelIndex,
            CurrentLevelIndex = _levelIndex,
            Score = Math.Max(0, _score),
            CoinCount = Math.Max(0, _coinCount),
            Lives = Math.Max(0, _lives)
        });
    }

    private PointF GetRespawnSpawnForLevel(int levelIndex, LevelRuntime runtime)
    {
        if (!_activeCheckpointByLevel.TryGetValue(levelIndex, out var checkpointOrder))
        {
            return runtime.Spawn;
        }

        foreach (var checkpoint in runtime.Checkpoints)
        {
            if (checkpoint.OrderIndex == checkpointOrder)
            {
                return new PointF(checkpoint.X, checkpoint.Y);
            }
        }

        return runtime.Spawn;
    }

    private RectangleF PlayerRect()
    {
        return new RectangleF(_player.X, _player.Y, _player.Width, _player.Height);
    }

    private static RectangleF EnemyRect(Enemy enemy)
    {
        return new RectangleF(enemy.X, enemy.Y, enemy.Width, enemy.Height);
    }

    private static RectangleF TileRect(int tx, int ty)
    {
        var ts = GameConstants.TileSize;
        return new RectangleF(tx * ts, ty * ts, ts, ts);
    }

    private static int ToTile(float worldCoord)
    {
        return (int)MathF.Floor(worldCoord / GameConstants.TileSize);
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + MathF.Sign(target - current) * maxDelta;
    }

    private bool IsDown(params Keys[] keys)
    {
        foreach (var key in keys)
        {
            if (_keysDown.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _keysDown.Add(e.KeyCode);

        if (e.KeyCode == Keys.R && _phase == GamePhase.Playing)
        {
            // Quick retry without resetting score/lives.
            LoadLevel(_levelIndex);
            return;
        }

        if (e.KeyCode == Keys.Enter && _phase != GamePhase.Playing)
        {
            // Full run restart after win or game over.
            RestartGame();
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _keysDown.Remove(e.KeyCode);
    }

    private struct BrickDebrisPiece
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float Spin;
        public float Angle;
        public float SourceX;
        public float SourceY;
        public float SourceSize;
        public float Age;
        public float Life;

        public BrickDebrisPiece(
            float x,
            float y,
            float vx,
            float vy,
            float spin,
            float sourceX,
            float sourceY,
            float sourceSize)
        {
            X = x;
            Y = y;
            Vx = vx;
            Vy = vy;
            Spin = spin;
            Angle = 0f;
            SourceX = sourceX;
            SourceY = sourceY;
            SourceSize = sourceSize;
            Age = 0f;
            // Slightly over half a second keeps feedback snappy.
            Life = 0.62f;
        }
    }

    // Temporary rising coin used for pickup/question-block feedback.
    private struct CoinPopEffect
    {
        public float X;
        public float Y;
        public float Age;
        public float Life;

        public CoinPopEffect(float x, float y)
        {
            X = x;
            Y = y;
            Age = 0f;
            Life = 0.58f;
        }
    }
}
