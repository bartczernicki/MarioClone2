using System.Drawing;

namespace MarioClone2;

internal sealed class LevelRuntime
{
    public string Name { get; }
    public TileCell[,] Tiles { get; }
    public List<Enemy> Enemies { get; }
    public List<CoinPickup> Coins { get; }
    public PointF Spawn { get; }
    public float FlagX { get; }

    public int Width => Tiles.GetLength(0);
    public int Height => Tiles.GetLength(1);
    public int PixelWidth => Width * GameConstants.TileSize;
    public int PixelHeight => Height * GameConstants.TileSize;

    public LevelRuntime(
        string name,
        TileCell[,] tiles,
        List<Enemy> enemies,
        List<CoinPickup> coins,
        PointF spawn,
        float flagX)
    {
        Name = name;
        Tiles = tiles;
        Enemies = enemies;
        Coins = coins;
        Spawn = spawn;
        FlagX = flagX;
    }

    public bool InBounds(int tx, int ty)
    {
        return tx >= 0 && ty >= 0 && tx < Width && ty < Height;
    }

    public bool IsSolid(int tx, int ty)
    {
        if (tx < 0 || tx >= Width)
        {
            return true;
        }

        if (ty < 0)
        {
            return false;
        }

        if (ty >= Height)
        {
            return true;
        }

        var type = Tiles[tx, ty].Type;
        return type is TileType.Ground or TileType.Brick or TileType.Question or TileType.Pipe;
    }

    public int GroundYAtFlag()
    {
        var tx = Math.Clamp((int)(FlagX / GameConstants.TileSize), 0, Width - 1);
        for (var y = 0; y < Height; y++)
        {
            if (IsSolid(tx, y))
            {
                return y * GameConstants.TileSize;
            }
        }

        return PixelHeight - GameConstants.TileSize;
    }
}

internal sealed class LevelDefinition
{
    public string Name { get; }
    public string[] Rows { get; }

    public LevelDefinition(string name, string[] rows)
    {
        Name = name;
        Rows = rows;
    }
}

internal sealed class LevelBuilder
{
    private readonly int _width;
    private readonly int _height;
    private readonly char[,] _cells;

    public LevelBuilder(int width, int height)
    {
        _width = width;
        _height = height;
        _cells = new char[width, height];
        FillRect(0, 0, width, height, '.');
    }

    public void FillRow(int y, char tile)
    {
        FillRect(0, y, _width, 1, tile);
    }

    public void FillRect(int x, int y, int width, int height, char tile)
    {
        for (var yy = y; yy < y + height; yy++)
        {
            if (yy < 0 || yy >= _height)
            {
                continue;
            }

            for (var xx = x; xx < x + width; xx++)
            {
                if (xx < 0 || xx >= _width)
                {
                    continue;
                }

                _cells[xx, yy] = tile;
            }
        }
    }

    public void ClearRect(int x, int y, int width, int height)
    {
        FillRect(x, y, width, height, '.');
    }

    public void Place(int x, int y, char tile)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height)
        {
            return;
        }

        _cells[x, y] = tile;
    }

    public LevelDefinition Build(string name)
    {
        var rows = new string[_height];
        for (var y = 0; y < _height; y++)
        {
            var chars = new char[_width];
            for (var x = 0; x < _width; x++)
            {
                chars[x] = _cells[x, y];
            }

            rows[y] = new string(chars);
        }

        return new LevelDefinition(name, rows);
    }
}
