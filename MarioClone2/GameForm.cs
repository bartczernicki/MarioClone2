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
    private readonly Bitmap _skyLayer;
    private readonly Bitmap _cloudLayer;
    private readonly Bitmap _mountainLayer;

    private LevelRuntime _level = null!;
    private Player _player = null!;
    private int _levelIndex;
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

        // Start from the first authored level.
        LoadLevel(0);

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

        if (_phase == GamePhase.Playing)
        {
            UpdateGame(dt);
        }

        Invalidate();
    }

    private void UpdateGame(float dt)
    {
        var left = IsDown(Keys.Left, Keys.A);
        var right = IsDown(Keys.Right, Keys.D);
        var jumpDown = IsDown(Keys.Space, Keys.Up, Keys.W);

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

        const float accel = 1600f;
        const float maxRunSpeed = 240f;
        const float groundFriction = 2400f;
        const float airDrag = 400f;

        if (move != 0)
        {
            _player.Facing = move;
            _player.Vx += move * accel * dt;
        }
        else
        {
            var friction = _player.OnGround ? groundFriction : airDrag;
            _player.Vx = MoveToward(_player.Vx, 0f, friction * dt);
        }

        _player.Vx = Math.Clamp(_player.Vx, -maxRunSpeed, maxRunSpeed);

        if (jumpDown && !_jumpWasDown && _player.OnGround)
        {
            _player.Vy = -560f;
            _player.OnGround = false;
        }

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

        if (_player.Y > _level.PixelHeight + 180f)
        {
            LoseLife();
            return;
        }

        UpdateEnemies(dt);
        CollectCoins();

        if (_player.X + _player.Width >= _level.FlagX)
        {
            _score += 1000;
            if (_levelIndex + 1 < _levelDefinitions.Count)
            {
                LoadLevel(_levelIndex + 1);
            }
            else
            {
                _phase = GamePhase.Won;
                _status = "You Win! Press Enter to play again.";
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

    private void UpdateEnemies(float dt)
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
            }
            else
            {
                LoseLife();
                return;
            }
        }
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
            var coinRect = new RectangleF(coin.X, coin.Y + bob, 16f, 16f);
            if (!playerRect.IntersectsWith(coinRect))
            {
                continue;
            }

            coin.Collected = true;
            _coinCount += 1;
            _score += 100;
        }
    }

    private void LoseLife()
    {
        _lives -= 1;
        if (_lives <= 0)
        {
            _phase = GamePhase.GameOver;
            _status = "Game Over. Press Enter to restart.";
            return;
        }

        LoadLevel(_levelIndex);
    }

    private void RestartGame()
    {
        _score = 0;
        _coinCount = 0;
        _lives = 3;
        _phase = GamePhase.Playing;
        _status = string.Empty;
        LoadLevel(0);
    }

    private void LoadLevel(int index)
    {
        _levelIndex = index;
        _level = LevelFactory.CreateRuntime(_levelDefinitions[index]);
        _player = new Player(_level.Spawn);
        _cameraX = 0f;
        _jumpWasDown = false;
        _phase = GamePhase.Playing;
        _status = string.Empty;
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
        if (tile.Type != TileType.Question || tile.Used)
        {
            return;
        }

        // Question blocks only award once.
        tile.Used = true;
        _coinCount += 1;
        _score += 100;
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
}
