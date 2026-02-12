using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MarioClone2;

internal sealed partial class GameForm
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        DrawBackground(g);
        DrawLevel(g);
        DrawEntities(g);
        DrawHud(g);

        if (_phase != GamePhase.Playing)
        {
            DrawOverlay(g);
        }
    }

    private void DrawBackground(Graphics g)
    {
        DrawLayerTiled(g, _skyLayer, 0.08f);
        DrawLayerTiled(g, _cloudLayer, 0.18f);
        DrawLayerTiled(g, _mountainLayer, 0.35f);
    }

    private void DrawLayerTiled(Graphics g, Bitmap layer, float speed)
    {
        var offset = (int)(-_cameraX * speed) % layer.Width;
        if (offset > 0)
        {
            offset -= layer.Width;
        }

        for (var x = offset; x < ClientSize.Width; x += layer.Width)
        {
            g.DrawImage(layer, x, 0, layer.Width, ClientSize.Height);
        }
    }

    private void DrawLevel(Graphics g)
    {
        var tileSize = GameConstants.TileSize;
        var minX = Math.Max(0, ToTile(_cameraX) - 1);
        var maxX = Math.Min(_level.Width - 1, ToTile(_cameraX + ClientSize.Width) + 2);

        for (var y = 0; y < _level.Height; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var tile = _level.Tiles[x, y];
                if (tile.Type == TileType.Empty)
                {
                    continue;
                }

                var screenX = (int)(x * tileSize - _cameraX);
                var screenY = y * tileSize;

                switch (tile.Type)
                {
                    case TileType.Ground:
                        g.DrawImage(_sprites.GroundTile, screenX, screenY, tileSize, tileSize);
                        break;
                    case TileType.Brick:
                        g.DrawImage(_sprites.BrickTile, screenX, screenY, tileSize, tileSize);
                        break;
                    case TileType.Question:
                        var questionTexture = tile.Used ? _sprites.QuestionUsedTile : _sprites.QuestionTile;
                        g.DrawImage(questionTexture, screenX, screenY, tileSize, tileSize);
                        break;
                    case TileType.Pipe:
                        g.DrawImage(_sprites.PipeTile, screenX, screenY, tileSize, tileSize);
                        break;
                }
            }
        }

        var poleX = (int)(_level.FlagX - _cameraX);
        var groundY = _level.GroundYAtFlag();
        using var polePen = new Pen(Color.FromArgb(230, 230, 230), 4f);
        g.DrawLine(polePen, poleX, 40, poleX, groundY);
        g.DrawImage(_sprites.Flag, poleX - 2, 52, 30, 24);
    }

    private void DrawEntities(Graphics g)
    {
        foreach (var coin in _level.Coins)
        {
            if (coin.Collected)
            {
                continue;
            }

            var sprite = MathF.Sin((_animTime * 10f) + coin.PulseOffset) > 0f
                ? _sprites.CoinA
                : _sprites.CoinB;

            var bob = MathF.Sin(_animTime * 8f + coin.PulseOffset) * 3f;
            var x = (int)(coin.X - _cameraX);
            var y = (int)(coin.Y + bob);

            g.DrawImage(sprite, x, y, 16, 16);
        }

        foreach (var enemy in _level.Enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            var sprite = MathF.Sin(_animTime * 8f) > 0f ? _sprites.GoombaA : _sprites.GoombaB;
            var x = (int)(enemy.X - _cameraX);
            var y = (int)enemy.Y;
            g.DrawImage(sprite, x, y, (int)enemy.Width, (int)enemy.Height);
        }

        var marioSprite = _sprites.GetPlayerFrame(_animTime, _player.Vx, _player.OnGround);
        var playerX = (int)(_player.X - _cameraX);
        var playerY = (int)_player.Y;

        if (_player.Facing >= 0)
        {
            g.DrawImage(marioSprite, playerX, playerY, (int)_player.Width, (int)_player.Height);
        }
        else
        {
            var state = g.Save();
            g.TranslateTransform(playerX + _player.Width, playerY);
            g.ScaleTransform(-1f, 1f);
            g.DrawImage(marioSprite, 0, 0, _player.Width, _player.Height);
            g.Restore(state);
        }
    }

    private void DrawHud(Graphics g)
    {
        using var shadowBrush = new SolidBrush(Color.FromArgb(25, 25, 25));
        using var textBrush = new SolidBrush(Color.WhiteSmoke);
        using var font = new Font("Consolas", 15f, FontStyle.Bold, GraphicsUnit.Pixel);

        var hud = $"WORLD {_levelIndex + 1}/{_levelDefinitions.Count}    LIVES {_lives}    COINS {_coinCount}    SCORE {_score}";
        g.DrawString(hud, font, shadowBrush, 13, 13);
        g.DrawString(hud, font, textBrush, 12, 12);
    }

    private void DrawOverlay(Graphics g)
    {
        using var dimBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
        g.FillRectangle(dimBrush, 0, 0, ClientSize.Width, ClientSize.Height);

        using var font = new Font("Consolas", 28f, FontStyle.Bold, GraphicsUnit.Pixel);
        var message = string.IsNullOrWhiteSpace(_status) ? "Paused" : _status;
        var size = g.MeasureString(message, font);
        var x = (ClientSize.Width - size.Width) * 0.5f;
        var y = (ClientSize.Height - size.Height) * 0.5f;

        using var textBrush = new SolidBrush(Color.White);
        using var shadowBrush = new SolidBrush(Color.Black);

        g.DrawString(message, font, shadowBrush, x + 2f, y + 2f);
        g.DrawString(message, font, textBrush, x, y);
    }

    private static Bitmap CreateSkyLayer(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, width, height),
            Color.FromArgb(122, 193, 255),
            Color.FromArgb(187, 232, 255),
            LinearGradientMode.Vertical);

        g.FillRectangle(brush, 0, 0, width, height);
        return bmp;
    }

    private static Bitmap CreateCloudLayer(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        using var cloudBrush = new SolidBrush(Color.FromArgb(185, 255, 255, 255));
        var rng = new Random(7);
        for (var i = 0; i < 25; i++)
        {
            var x = rng.Next(-120, width);
            var y = rng.Next(20, 200);
            var w = rng.Next(70, 140);
            var h = rng.Next(26, 48);

            g.FillEllipse(cloudBrush, x, y, w, h);
            g.FillEllipse(cloudBrush, x + (w * 0.28f), y - 14, w * 0.45f, h * 0.9f);
            g.FillEllipse(cloudBrush, x + (w * 0.55f), y - 6, w * 0.42f, h * 0.8f);
        }

        return bmp;
    }

    private static Bitmap CreateMountainLayer(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        using var farBrush = new SolidBrush(Color.FromArgb(220, 117, 180, 116));
        using var midBrush = new SolidBrush(Color.FromArgb(230, 79, 146, 92));

        for (var x = -180; x < width + 180; x += 240)
        {
            Point[] points =
            [
                new Point(x, height - 70),
                new Point(x + 100, height - 240),
                new Point(x + 220, height - 70)
            ];

            g.FillPolygon(farBrush, points);
        }

        for (var x = -120; x < width + 160; x += 200)
        {
            Point[] points =
            [
                new Point(x, height - 48),
                new Point(x + 80, height - 170),
                new Point(x + 180, height - 48)
            ];

            g.FillPolygon(midBrush, points);
        }

        using var grassBrush = new SolidBrush(Color.FromArgb(60, 138, 66));
        g.FillRectangle(grassBrush, 0, height - 48, width, 48);

        return bmp;
    }
}
